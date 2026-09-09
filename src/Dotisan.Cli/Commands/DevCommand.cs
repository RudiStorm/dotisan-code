using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed class DevCommand : WorkspaceCommand
{
    public override string Name => "dev";
    public override string Description => "Run the API and Vue development servers together.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument is not "--lean" and not "--observability" and not "--mailpit" and not "--environment")) return Fail(context.Console, "Usage: dotisan dev [--lean] [--observability] [--mailpit] [--environment <name>].", DotisanExitCode.UsageError);
        var environment = "Development";
        for (var index = 0; index < arguments.Count; index++) if (arguments[index] == "--environment") { if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index])) return Fail(context.Console, "--environment requires a value.", DotisanExitCode.UsageError); environment = arguments[index]; }
        if (!TryGetServices(context, out var services)) return Fail(context.Console, "Workspace services are unavailable.");
        if (services.ApiProjectPath is null) return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");
        var processes = new List<IDotisanProcess>(); var databaseStartAttempted = false; var databaseStarted = false; var mailpitStartAttempted = false; var mailpitStarted = false; var dashboardStartAttempted = false; var dashboardStarted = false;
        try
        {
            var proxyMigration = await MigrateLegacyViteProxyAsync(services.FrontendDirectory, context.Console, cancellationToken); if (!proxyMigration.Success) return Fail(context.Console, proxyMigration.ErrorMessage ?? "Could not prepare the Vue development proxy.");
            if (services.Database != DatabaseProvider.SQLite)
            {
                databaseStartAttempted = true; var databaseResult = await services.RunAsync("docker", ["compose", "up", "-d", "--wait", "--wait-timeout", "120", "database"], services.WorkingDirectory, context.Console, cancellationToken);
                if (!databaseResult.Success) { if (cancellationToken.IsCancellationRequested) { context.Console.WriteLine("Stopping development services..."); return DotisanExitCode.Success; } return Fail(context.Console, $"Could not start the {services.Database} database service with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {databaseResult.ErrorMessage}"); } databaseStarted = true;
            }
            var mailpitExplicit = arguments.Contains("--mailpit", StringComparer.OrdinalIgnoreCase);
            if (services.MailProvider == MailProvider.Mailpit && (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) || mailpitExplicit)) { mailpitStartAttempted = true; var mailpitResult = await services.RunAsync("docker", ["compose", "up", "-d", "--wait", "--wait-timeout", "120", "mailpit"], services.WorkingDirectory, context.Console, cancellationToken); if (!mailpitResult.Success) return Fail(context.Console, $"Could not start Mailpit with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {mailpitResult.ErrorMessage}"); mailpitStarted = true; }
            if (arguments.Contains("--observability", StringComparer.OrdinalIgnoreCase)) { dashboardStartAttempted = true; var dashboardResult = await services.RunAsync("docker", ["compose", "up", "-d", "dashboard"], services.WorkingDirectory, context.Console, cancellationToken); if (!dashboardResult.Success) return Fail(context.Console, $"Could not start the Aspire Dashboard with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {dashboardResult.ErrorMessage}"); dashboardStarted = true; context.Console.WriteLine("Aspire Dashboard: http://localhost:18888"); }
            var apiArguments = new List<string> { "watch", "--project", services.ApiProjectPath, "run", "--", "--environment", environment, "--urls", $"http://localhost:{services.ApiPort}" }; if (arguments.Contains("--observability", StringComparer.OrdinalIgnoreCase)) apiArguments.Add("--dotisan-observability"); processes.Add(await services.StartAsync("dotnet", apiArguments, services.WorkingDirectory, context.Console, cancellationToken));
            if (!arguments.Contains("--lean", StringComparer.OrdinalIgnoreCase)) { if (services.FrontendDirectory is null) return Fail(context.Console, "Could not find the Vue frontend. Use --lean to run the API only."); var packageManager = services is DefaultDotisanServices defaultServices ? defaultServices.PackageManager : "pnpm"; processes.Add(await services.StartAsync(packageManager, ["run", "dev", "--", "--port", services.WebPort.ToString(System.Globalization.CultureInfo.InvariantCulture)], services.FrontendDirectory, context.Console, cancellationToken)); }
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); var completedTask = await Task.WhenAny(processes.Select(process => process.Completion).Append(cancellationTask)); if (completedTask == cancellationTask || cancellationToken.IsCancellationRequested) { context.Console.WriteLine("Stopping development services..."); await Task.WhenAll(processes.Select(process => process.StopAsync())); } return DotisanExitCode.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { context.Console.WriteLine("Stopping development services..."); return DotisanExitCode.Success; }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception) { return Fail(context.Console, $"Could not start development services: {exception.Message}"); }
        finally
        {
            foreach (var process in processes) await process.DisposeAsync();
            if (databaseStarted || (databaseStartAttempted && cancellationToken.IsCancellationRequested)) { var stopResult = await services.RunAsync("docker", ["compose", "stop", "database"], services.WorkingDirectory, context.Console, CancellationToken.None); if (!stopResult.Success) context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Docker database service."); }
            if (mailpitStarted || (mailpitStartAttempted && cancellationToken.IsCancellationRequested)) { var stopResult = await services.RunAsync("docker", ["compose", "stop", "mailpit"], services.WorkingDirectory, context.Console, CancellationToken.None); if (!stopResult.Success) context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Mailpit service."); }
            if (dashboardStarted || (dashboardStartAttempted && cancellationToken.IsCancellationRequested)) { var stopResult = await services.RunAsync("docker", ["compose", "stop", "dashboard"], services.WorkingDirectory, context.Console, CancellationToken.None); if (!stopResult.Success) context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Aspire Dashboard service."); }
        }
    }

    private static async Task<DotisanOperationResult> MigrateLegacyViteProxyAsync(string? frontendDirectory, IConsole console, CancellationToken cancellationToken)
    {
        if (frontendDirectory is null) return DotisanOperationResult.Succeeded();
        var viteConfigPath = Path.Combine(frontendDirectory, "vite.config.ts"); if (!File.Exists(viteConfigPath)) return DotisanOperationResult.Succeeded();
        try { var content = await File.ReadAllTextAsync(viteConfigPath, cancellationToken); const string legacyTarget = "https://localhost:5001"; const string developmentTarget = "http://localhost:5000"; if (!content.Contains(legacyTarget, StringComparison.Ordinal)) return DotisanOperationResult.Succeeded(); await File.WriteAllTextAsync(viteConfigPath, content.Replace(legacyTarget, developmentTarget, StringComparison.Ordinal), cancellationToken); console.WriteLine("Updated the legacy Dotisan Vite proxy to http://localhost:5000."); return DotisanOperationResult.Succeeded(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return DotisanOperationResult.Failed($"Could not update {viteConfigPath}: {exception.Message}"); }
    }
}
