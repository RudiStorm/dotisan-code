using System.Reflection;
using Dotisan.Cli.Diagnostics;
using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.Generators;

namespace Dotisan.Cli;

public sealed class DotisanApplication
{
    public static string Version => typeof(DotisanApplication).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

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

    public static DotisanApplication CreateDefault(IConsole? console = null, IDotisanServices? services = null, IPrompts? prompts = null, IProjectGenerator? projectGenerator = null)
    {
        var output = console ?? new SystemConsole();
        var registry = new DotisanCommandRegistry();
        var resolvedPrompts = prompts ?? new DefaultPrompts(output);
        var application = new DotisanApplication(registry, output, resolvedPrompts, projectGenerator ?? new GoldenTemplateGenerator(), services ?? new DefaultDotisanServices());
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
        var commandArguments = args[1..];
        var legacyAlias = false;
        if (commandName.Equals("make", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("add", StringComparison.OrdinalIgnoreCase)
            || commandName.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (commandArguments.Length == 0)
            {
                context.Console.WriteError($"Usage: dotisan {commandName} <subcommand> [options]. Run 'dotisan {commandName} --help'.");
                return DotisanExitCode.UsageError;
            }

            commandName = $"{commandName}:{commandArguments[0]}";
            commandArguments = commandArguments[1..];
        }
        else if (commandName.Contains(':', StringComparison.Ordinal))
        {
            legacyAlias = true;
        }
        if (!registry.TryGet(commandName, out var command) || command is null)
        {
            context.Console.WriteError($"Unknown command '{commandName}'. Run 'dotisan help' to see available commands.");
            return DotisanExitCode.UsageError;
        }

        if (legacyAlias)
            context.Console.WriteLine($"Warning: '{args[0]}' is deprecated; use 'dotisan {args[0].Replace(':', ' ')}'.");

        if (commandArguments is ["--help"] or ["-h"])
        {
            context.Console.WriteLine($"Usage: dotisan {command.Name.Replace(':', ' ')}");
            context.Console.WriteLine(command.Description);
            return DotisanExitCode.Success;
        }

        return await command.ExecuteAsync(context, commandArguments, cancellationToken);
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
        registry.Register(new DoctorCommand());
        registry.Register(new MailCommand());
        registry.Register(new AddIntegrationCommand());
        registry.Register(new RemoveIntegrationCommand());
    }
}
