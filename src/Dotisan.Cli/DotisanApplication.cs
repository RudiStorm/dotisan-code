using Dotisan.Core;
using Dotisan.Generators;
using Dotisan.Cli.Generation;

namespace Dotisan.Cli;

public sealed class DotisanApplication
{
    public const string Version = "0.6.0";

    private readonly DotisanCommandRegistry registry;
    private readonly CommandContext context;

    public DotisanApplication(
        DotisanCommandRegistry registry,
        IConsole console,
        IPrompts prompts,
        IProjectGenerator projectGenerator,
        IDotisanServices? services = null)
    {
        this.registry = registry;
        context = new CommandContext(console, prompts, projectGenerator, services);
    }

    public static DotisanApplication CreateDefault(IConsole? console = null, IDotisanServices? services = null)
    {
        var output = console ?? new SystemConsole();
        var registry = new DotisanCommandRegistry();
        var prompts = new DefaultPrompts(output);
        var application = new DotisanApplication(registry, output, prompts, new GoldenTemplateGenerator(), services ?? new DefaultDotisanServices());
        application.RegisterBuiltIns();
        return application;
    }

    public async Task<DotisanExitCode> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            return await ExecuteAsync("help", [], cancellationToken);
        }

        if (args is ["--version"] or ["-v"])
        {
            context.Console.WriteLine($"dotisan {Version}");
            return DotisanExitCode.Success;
        }

        var commandName = args[0];
        if (!registry.TryGet(commandName, out var command) || command is null)
        {
            context.Console.WriteError($"Unknown command '{commandName}'. Run 'dotisan help' to see available commands.");
            return DotisanExitCode.UsageError;
        }

        return await command.ExecuteAsync(context, args[1..], cancellationToken);
    }

    public void Register(IDotisanCommand command) => registry.Register(command);

    private Task<DotisanExitCode> ExecuteAsync(string commandName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return registry.TryGet(commandName, out var command) && command is not null
            ? command.ExecuteAsync(context, arguments, cancellationToken)
            : Task.FromResult(DotisanExitCode.UsageError);
    }

    private void RegisterBuiltIns()
    {
        registry.Register(new HelpCommand(registry));
        registry.Register(new NewCommand());

        registry.Register(new MakeResourceCommand());
        registry.Register(new MakeEndpointCommand());
        registry.Register(new MakeCrudCommand());
        registry.Register(new MigrateCommand());
        registry.Register(new DevCommand());
        registry.Register(new BuildCommand());
        registry.Register(new RunCommand());
        registry.Register(new GenerateCommand());
        registry.Register(new JobsCommand());
        registry.Register(new ScheduleCommand());
        foreach (var command in new[] { "add", "remove", "doctor" })
            registry.Register(new NotImplementedCommand(command));
    }
}
