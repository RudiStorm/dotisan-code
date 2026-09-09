using Dotisan.Cli.Generation;
using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class GenerateCommand : WorkspaceCommand
{
    public override string Name => "generate";
    public override string Description => "Generate frontend contract files from the compiled OpenAPI document.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var check = false;
        var noOpenApi = false;
        var noBuild = false;
        foreach (var argument in arguments)
        {
            if (argument.Equals("--check", StringComparison.OrdinalIgnoreCase)) check = true;
            else if (argument.Equals("--no-openapi", StringComparison.OrdinalIgnoreCase)) noOpenApi = true;
            else if (argument.Equals("--no-build", StringComparison.OrdinalIgnoreCase)) noBuild = true;
            else return Fail(context.Console, "Usage: dotisan generate [--check] [--no-openapi] [--no-build].", DotisanExitCode.UsageError);
        }

        if (!TryGetServices(context, out var services)) return Fail(context.Console, "Workspace services are unavailable.");
        if (services.SolutionPath is null) return Fail(context.Console, "Could not find a solution. Run this command from a generated Dotisan project.");
        if (!noBuild)
        {
            var build = await services.RunAsync("dotnet", ["build", services.SolutionPath], services.WorkingDirectory, context.Console, cancellationToken);
            if (!build.Success) return Fail(context.Console, build.ErrorMessage ?? "The API build failed; contract generation was not run.");
        }

        var result = await ContractGenerationService.GenerateAsync(services.WorkingDirectory, check, cancellationToken, noOpenApi, services, context.Console);
        if (!result.Success) return Fail(context.Console, result.ErrorMessage ?? "Contract generation failed.");
        context.Console.WriteLine(check ? "Generated contract files are up to date." : "Generated frontend contract files.");
        return DotisanExitCode.Success;
    }
}
