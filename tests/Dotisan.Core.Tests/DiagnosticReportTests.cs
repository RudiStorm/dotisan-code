using Dotisan.Core.Diagnostics;

namespace Dotisan.Core.Tests;

public sealed class DiagnosticReportTests
{
    [Fact]
    public void Report_detects_blocking_results_and_preserves_result_order()
    {
        var report = new DiagnosticReport(
        [
            new("workspace", DiagnosticSeverity.Pass, "Workspace found."),
            new("migrations", DiagnosticSeverity.Blocking, "Migrations are missing.")
        ]);

        Assert.True(report.HasBlockingResults);
        Assert.Equal(["workspace", "migrations"], report.Results.Select(result => result.Name));
    }

    [Fact]
    public void Report_without_blocking_results_is_safe_to_continue()
    {
        var report = new DiagnosticReport(
        [new("workspace", DiagnosticSeverity.Pass, "Workspace found.")]);

        Assert.False(report.HasBlockingResults);
    }
}
