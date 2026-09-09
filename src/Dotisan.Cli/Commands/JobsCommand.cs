using System.Text.Json;
using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class JobsCommand : WorkspaceCommand
{
    public override string Name => "jobs";
    public override string Description => "Inspect generated background job configuration.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!arguments.SequenceEqual(["status"], StringComparer.OrdinalIgnoreCase)) return Task.FromResult(Fail(context.Console, "Usage: dotisan jobs status.", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.ApiProjectPath is null) return Task.FromResult(Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root."));
        var jobsFile = Path.Combine(Path.GetDirectoryName(services.ApiProjectPath)!, "Jobs", "JobRegistration.cs");
        if (!File.Exists(jobsFile)) return Task.FromResult(Fail(context.Console, "This project does not contain generated jobs support. Regenerate it with a current Dotisan CLI."));
        context.Console.WriteLine(ReadJobsEnabled(services.WorkingDirectory) ? "Jobs are enabled." : "Jobs are disabled by configuration.");
        context.Console.WriteLine($"Persistence provider: {services.Database}.");
        context.Console.WriteLine($"Registration: {Path.GetRelativePath(services.WorkingDirectory, jobsFile)}");
        return Task.FromResult(DotisanExitCode.Success);
    }

    private static bool ReadJobsEnabled(string root)
    {
        var path = Path.Combine(root, "src");
        var appSettings = Directory.Exists(path) ? Directory.GetFiles(root, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault() : null;
        if (appSettings is null) return true;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(appSettings));
            return !document.RootElement.TryGetProperty("Dotisan", out var dotisan) || !dotisan.TryGetProperty("Jobs", out var jobs) || !jobs.TryGetProperty("Enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
        }
        catch (JsonException) { return true; }
    }
}
