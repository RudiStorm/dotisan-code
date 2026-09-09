using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class MakeResourceCommand : WorkspaceCommand
{
    public override string Name => "make:resource";
    public override string Description => "Create a model and vertical CRUD endpoint slice.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            return Fail(context.Console, "Usage: dotisan make:resource <ResourceName>", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var result = await services.ScaffoldResourceAsync(arguments[0], cancellationToken);
        if (!result.Success)
            return Fail(context.Console, result.ErrorMessage ?? "Resource scaffolding failed.");
        context.Console.WriteLine($"Created {arguments[0]} resource files.");
        return DotisanExitCode.Success;
    }
}
