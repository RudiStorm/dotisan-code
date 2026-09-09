using Dotisan.Core;
using Dotisan.Generators;

namespace Dotisan.Cli;

internal sealed class MakeCrudCommand : WorkspaceCommand
{
    public override string Name => "make:crud";
    public override string Description => "Create editable Vue CRUD files for a resource.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            return Fail(context.Console, "Usage: dotisan make:crud <ResourceName>", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var result = await CrudScaffolder.ScaffoldAsync(services.WorkingDirectory, arguments[0], cancellationToken);
        if (!result.Success)
            return Fail(context.Console, result.ErrorMessage ?? "Frontend CRUD scaffolding failed.");

        context.Console.WriteLine($"Created editable Vue CRUD files for {arguments[0]}.");
        foreach (var file in result.CreatedFiles)
            context.Console.WriteLine($"  {Path.GetRelativePath(services.WorkingDirectory, file)}");
        return DotisanExitCode.Success;
    }
}
