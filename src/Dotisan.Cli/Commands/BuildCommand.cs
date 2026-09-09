using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class BuildCommand : WorkspaceCommand
{
    public override string Name => "build";
    public override string Description => "Build the API and Vue frontend.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var frontend = !arguments.Contains("--no-frontend", StringComparer.OrdinalIgnoreCase);
        if (arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && !argument.Equals("--no-frontend", StringComparison.OrdinalIgnoreCase)))
            return Fail(context.Console, "Usage: dotisan build [--no-frontend].", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");
        if (services.SolutionPath is null)
            return Fail(context.Console, "Could not find a solution. Run this command from a generated Dotisan project.");

        var api = await services.RunAsync("dotnet", ["build", services.SolutionPath], services.WorkingDirectory, context.Console, cancellationToken);
        if (!api.Success)
            return Fail(context.Console, api.ErrorMessage ?? "API build failed.");
        if (!frontend)
            return DotisanExitCode.Success;

        var packageManager = services is DefaultDotisanServices defaultServices ? defaultServices.PackageManager : "pnpm";
        if (services.FrontendDirectory is null)
            return Fail(context.Console, "Could not find the Vue frontend. Use --no-frontend to build the API only.");
        var web = await services.RunAsync(packageManager, ["run", "build"], services.FrontendDirectory, context.Console, cancellationToken);
        return web.Success ? DotisanExitCode.Success : Fail(context.Console, web.ErrorMessage ?? "Frontend build failed.");
    }
}
