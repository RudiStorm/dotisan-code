using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class MakeEndpointCommand : WorkspaceCommand
{
    public override string Name => "make:endpoint";
    public override string Description => "Create a single-file vertical endpoint.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            return Fail(context.Console, "Usage: dotisan make:endpoint <EndpointName>", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var result = await services.ScaffoldEndpointAsync(arguments[0], cancellationToken);
        if (!result.Success)
            return Fail(context.Console, result.ErrorMessage ?? "Endpoint scaffolding failed.");
        context.Console.WriteLine($"Created {arguments[0]} endpoint.");
        return DotisanExitCode.Success;
    }
}
