using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class RunCommand : WorkspaceCommand
{
    public override string Name => "run";
    public override string Description => "Run the API using dotnet run.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count > 0) return Fail(context.Console, "Usage: dotisan run.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services)) return Fail(context.Console, "Workspace services are unavailable.");
        if (services.ApiProjectPath is null) return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");
        var result = await services.RunAsync("dotnet", ["run", "--project", services.ApiProjectPath], services.WorkingDirectory, context.Console, cancellationToken);
        return result.Success ? DotisanExitCode.Success : Fail(context.Console, result.ErrorMessage ?? "API run failed.");
    }
}
