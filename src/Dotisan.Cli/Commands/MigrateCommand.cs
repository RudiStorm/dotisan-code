using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class MigrateCommand : WorkspaceCommand
{
    public override string Name => "migrate";
    public override string Description => "Apply committed EF Core migrations.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var efArguments = new List<string> { "ef" };
        if (arguments.SequenceEqual(["status"], StringComparer.OrdinalIgnoreCase) || arguments.SequenceEqual(["--dry-run"], StringComparer.OrdinalIgnoreCase))
            efArguments.AddRange(["migrations", "list"]);
        else if (arguments.Count == 0 || arguments.SequenceEqual(["--production"], StringComparer.OrdinalIgnoreCase))
            efArguments.AddRange(["database", "update"]);
        else
            return Fail(context.Console, "Usage: dotisan migrate [status|--dry-run|--production]. Use 'dotnet ef' directly for migration authoring and rollback.", DotisanExitCode.UsageError);

        if (services.ApiProjectPath is null)
            return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");
        efArguments.AddRange(["--project", services.ApiProjectPath]);

        var result = await services.RunAsync("dotnet", efArguments, services.WorkingDirectory, context.Console, cancellationToken);
        if (result.Success)
            return DotisanExitCode.Success;
        context.Console.WriteError(result.ErrorMessage ?? "EF Core migration command failed.");
        context.Console.WriteError("If dotnet-ef is not installed, run 'dotnet tool install --global dotnet-ef' and retry.");
        return DotisanExitCode.GenerationError;
    }
}
