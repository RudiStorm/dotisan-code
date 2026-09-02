namespace Dotisan.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Pass,
    Warning,
    Blocking
}

public sealed record DiagnosticResult(
    string Name,
    DiagnosticSeverity Severity,
    string Message,
    string? Recommendation = null);

public sealed record DiagnosticReport(IReadOnlyList<DiagnosticResult> Results)
{
    public bool HasBlockingResults => Results.Any(result => result.Severity == DiagnosticSeverity.Blocking);
}
