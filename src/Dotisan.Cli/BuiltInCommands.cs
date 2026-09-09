using System.Text.Json;
using Dotisan.Cli.Diagnostics;
using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.Core.Diagnostics;
using Dotisan.Core.Jobs;
using Dotisan.Generators;

namespace Dotisan.Cli;

internal sealed class HelpCommand(DotisanCommandRegistry registry) : IDotisanCommand
{
    public string Name => "help";
    public string Description => "Show available commands.";

    public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        context.Console.WriteLine("Dotisan - batteries-included ASP.NET Core + Vue scaffolding");
        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Usage: dotisan <command> [arguments]");
        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Commands:");
        context.Console.WriteLine("  dotisan new <ProjectName>    Create a new Dotisan application");
        context.Console.WriteLine("      Profiles: --profile minimal|identity|saas|maximal|custom");
        context.Console.WriteLine("  dotisan make resource <Name>  Scaffold a resource (make:resource is a compatibility alias)");
        context.Console.WriteLine("  dotisan make endpoint <Name>  Scaffold an endpoint");
        context.Console.WriteLine("  dotisan make crud <Name>      Scaffold CRUD UI and API files");
        context.Console.WriteLine("  dotisan add integration <Name>    Add an integration recipe");
        context.Console.WriteLine("  dotisan remove integration <Name> Remove an integration recipe");
        foreach (var command in registry.Commands.Where(command => command.Name is not "help" and not "new"))
        {
            context.Console.WriteLine($"  {command.Name,-25} {command.Description}");
        }

        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Options: --version, --help");
        return Task.FromResult(DotisanExitCode.Success);
    }
}

internal sealed class NotImplementedCommand(string name) : IDotisanCommand
{
    public string Name => name;
    public string Description => $"{name} (planned; not implemented yet)";

    public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        context.Console.WriteError($"{Name} is not implemented yet in Dotisan {DotisanApplication.Version}.");
        context.Console.WriteError("The generated application remains a standard .NET project; use its native tooling while this command is being built.");
        if (Name == "migrate")
        {
            context.Console.WriteError("For now, use 'dotnet ef migrations add <Name>' and 'dotnet ef database update' from the API project.");
        }

        return Task.FromResult(DotisanExitCode.NotImplemented);
    }
}

internal abstract class WorkspaceCommand : IDotisanCommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    protected static DotisanExitCode Fail(IConsole console, string message, DotisanExitCode code = DotisanExitCode.GenerationError)
    {
        console.WriteError(message);
        return code;
    }

    protected static bool TryGetServices(CommandContext context, out IDotisanServices services)
    {
        services = context.Services!;
        return services is not null;
    }
}

