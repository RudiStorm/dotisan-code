using Dotisan.Cli.Diagnostics;
using Dotisan.Core;
using Dotisan.Core.Diagnostics;

namespace Dotisan.Cli;

internal sealed class DoctorCommand : WorkspaceCommand
{
    public override string Name => "doctor";
    public override string Description => "Check workspace and production readiness.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument is not "--production")) return Task.FromResult(Fail(context.Console, "Usage: dotisan doctor [--production].", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services)) return Task.FromResult(Fail(context.Console, "Workspace services are unavailable."));
        var report = DoctorService.Inspect(services, arguments.Contains("--production", StringComparer.OrdinalIgnoreCase));
        foreach (var result in report.Results)
        {
            var label = result.Severity switch { DiagnosticSeverity.Pass => "PASS", DiagnosticSeverity.Warning => "WARNING", _ => "BLOCKING" };
            context.Console.WriteLine($"[{label}] {result.Name}: {result.Message}");
            if (result.Severity != DiagnosticSeverity.Pass && result.Recommendation is not null) context.Console.WriteLine($"         {result.Recommendation}");
        }
        return Task.FromResult(report.HasBlockingResults ? DotisanExitCode.GenerationError : DotisanExitCode.Success);
    }
}
