using Dotisan.Core;
using Dotisan.Core.Diagnostics;

namespace Dotisan.Cli.Diagnostics;

public static class DoctorService
{
    public static DiagnosticReport Inspect(IDotisanServices services, bool production)
    {
        var results = new List<DiagnosticResult>();
        var root = services.WorkingDirectory;
        var apiProject = services.ApiProjectPath;

        AddFileCheck(results, "Workspace", services.SolutionPath, "Create or open a generated Dotisan project.");
        AddFileCheck(results, "API project", apiProject, "Run this command from a generated Dotisan project.");
        AddFileCheck(results, "Dotisan configuration", Path.Combine(root, "dotisan.config"), "Regenerate the project or restore dotisan.config.");

        if (services.FrontendDirectory is null)
        {
            results.Add(new DiagnosticResult("Frontend", DiagnosticSeverity.Warning, "Vue frontend was not found.", "Use --lean for API-only development or restore the generated frontend."));
        }
        else
        {
            AddFileCheck(results, "Frontend package", Path.Combine(services.FrontendDirectory, "package.json"), "Run frontend generation from a generated project.");
        }

        var apiDirectory = apiProject is null ? null : Path.GetDirectoryName(apiProject);
        var hasMigrations = apiDirectory is not null
            && Directory.Exists(Path.Combine(apiDirectory, "Migrations"))
            && Directory.EnumerateFiles(Path.Combine(apiDirectory, "Migrations"), "*.cs", SearchOption.TopDirectoryOnly).Any();
        results.Add(hasMigrations
            ? new DiagnosticResult("EF Core migrations", DiagnosticSeverity.Pass, "At least one migration is present.")
            : new DiagnosticResult("EF Core migrations", production ? DiagnosticSeverity.Blocking : DiagnosticSeverity.Warning, "No EF Core migrations were found.", "Run 'dotnet ef migrations add <Name>' and review the result before applying it."));

        if (production)
        {
            AddFileCheck(results, "Production Dockerfile", Path.Combine(root, "Dockerfile"), "Add a production Dockerfile to the generated application.");
            AddFileCheck(results, "Health endpoint", apiDirectory is null ? null : Path.Combine(apiDirectory, "Features", "Health", "HealthEndpoints.cs"), "Restore the generated health endpoint before deployment.");
            AddContentCheck(results, "HTTPS enforcement", apiDirectory is null ? null : Path.Combine(apiDirectory, "Program.cs"), "UseHttpsRedirection", "Enable HTTPS redirection in the generated API pipeline.");
            AddContentCheck(results, "Authentication rate limiting", apiDirectory is null ? null : Path.Combine(apiDirectory, "Program.cs"), "AddRateLimiter", "Configure rate limiting for authentication endpoints before deployment.");
        }

        if (services.Database != DatabaseProvider.SQLite)
        {
            AddFileCheck(results, "Database Compose definition", Path.Combine(root, "compose.yaml"), "Keep Docker running and restore the generated provider compose.yaml.");
        }

        if (services.MailProvider == MailProvider.Mailpit)
        {
            var composePath = Path.Combine(root, "compose.yaml");
            if (!File.Exists(composePath))
                results.Add(new DiagnosticResult("Mailpit Compose definition", DiagnosticSeverity.Blocking, "Mailpit is selected but compose.yaml is missing.", "Regenerate the project with --mail-provider mailpit."));
            else if (!File.ReadAllText(composePath).Contains("mailpit:", StringComparison.OrdinalIgnoreCase))
                results.Add(new DiagnosticResult("Mailpit Compose definition", DiagnosticSeverity.Blocking, "Mailpit is selected but the Compose service is missing.", "Add the generated Mailpit service or regenerate the project."));
            else
                results.Add(new DiagnosticResult("Mailpit Compose definition", DiagnosticSeverity.Pass, "Mailpit service is configured for local development."));
        }

        return new DiagnosticReport(results);
    }

    private static void AddFileCheck(List<DiagnosticResult> results, string name, string? path, string recommendation)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            results.Add(new DiagnosticResult(name, DiagnosticSeverity.Pass, $"Found {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}."));
        else
            results.Add(new DiagnosticResult(name, DiagnosticSeverity.Blocking, "Required project file was not found.", recommendation));
    }

    private static void AddContentCheck(List<DiagnosticResult> results, string name, string? path, string requiredText, string recommendation)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && File.ReadAllText(path).Contains(requiredText, StringComparison.Ordinal))
            results.Add(new DiagnosticResult(name, DiagnosticSeverity.Pass, $"Found {requiredText} in {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}."));
        else
            results.Add(new DiagnosticResult(name, DiagnosticSeverity.Blocking, $"Required production safeguard '{requiredText}' was not found.", recommendation));
    }
}
