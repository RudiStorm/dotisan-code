using System.Text.Json;
using Dotisan.Cli.Diagnostics;
using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.Core.Diagnostics;
using Dotisan.Core.Jobs;
using Dotisan.Generators;

namespace Dotisan.Cli;

internal sealed class HelpCommand(DotisanCommandRegistry registry) : IDotisanCommand
{
    public string Name => "help";
    public string Description => "Show available commands.";

    public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        context.Console.WriteLine("Dotisan - batteries-included ASP.NET Core + Vue scaffolding");
        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Usage: dotisan <command> [arguments]");
        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Commands:");
        context.Console.WriteLine("  dotisan new <ProjectName>    Create a new Dotisan application");
        context.Console.WriteLine("      Profiles: --profile minimal|identity|saas|maximal|custom");
        context.Console.WriteLine("  dotisan make resource <Name>  Scaffold a resource (make:resource is a compatibility alias)");
        context.Console.WriteLine("  dotisan make endpoint <Name>  Scaffold an endpoint");
        context.Console.WriteLine("  dotisan make crud <Name>      Scaffold CRUD UI and API files");
        context.Console.WriteLine("  dotisan add integration <Name>    Add an integration recipe");
        context.Console.WriteLine("  dotisan remove integration <Name> Remove an integration recipe");
        foreach (var command in registry.Commands.Where(command => command.Name is not "help" and not "new"))
        {
            context.Console.WriteLine($"  {command.Name,-25} {command.Description}");
        }

        context.Console.WriteLine(string.Empty);
        context.Console.WriteLine("Options: --version, --help");
        return Task.FromResult(DotisanExitCode.Success);
    }
}

internal sealed class NotImplementedCommand(string name) : IDotisanCommand
{
    public string Name => name;
    public string Description => $"{name} (planned; not implemented yet)";

    public Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        context.Console.WriteError($"{Name} is not implemented yet in Dotisan {DotisanApplication.Version}.");
        context.Console.WriteError("The generated application remains a standard .NET project; use its native tooling while this command is being built.");
        if (Name == "migrate")
        {
            context.Console.WriteError("For now, use 'dotnet ef migrations add <Name>' and 'dotnet ef database update' from the API project.");
        }

        return Task.FromResult(DotisanExitCode.NotImplemented);
    }
}

internal abstract class WorkspaceCommand : IDotisanCommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken);

    protected static DotisanExitCode Fail(IConsole console, string message, DotisanExitCode code = DotisanExitCode.GenerationError)
    {
        console.WriteError(message);
        return code;
    }

    protected static bool TryGetServices(CommandContext context, out IDotisanServices services)
    {
        services = context.Services!;
        return services is not null;
    }
}

internal sealed class AddIntegrationCommand : WorkspaceCommand
{
    public override string Name => "add:integration";
    public override string Description => "Add an explicit integration recipe to the workspace.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var dryRun = arguments.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var names = arguments.Where(argument => !argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length != 1)
            return Fail(context.Console, "Usage: dotisan add:integration <aspire|sendgrid|mailgun|signoz|notifications|storage|caching|imports-exports|webhooks> [--dry-run].", DotisanExitCode.UsageError);
        if (!IntegrationRecipes.TryGetValue(names[0], out var recipe))
            return Fail(context.Console, $"Unknown integration '{names[0]}'. Supported integrations: {string.Join(", ", IntegrationRecipes.Keys)}.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services) || services.SolutionPath is null)
            return Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root.");

        var integrationDirectory = Path.Combine(services.WorkingDirectory, ".dotisan", "integrations");
        var path = Path.Combine(integrationDirectory, names[0].ToLowerInvariant() + ".md");
        if (dryRun)
        {
            context.Console.WriteLine($"Would create {Path.GetRelativePath(services.WorkingDirectory, path)}");
            context.Console.WriteLine(recipe);
            return DotisanExitCode.Success;
        }

        Directory.CreateDirectory(integrationDirectory);
        await File.WriteAllTextAsync(path, recipe, cancellationToken);
        context.Console.WriteLine($"Created {Path.GetRelativePath(services.WorkingDirectory, path)}. Apply the documented package and configuration changes explicitly.");
        return DotisanExitCode.Success;
    }

    private static readonly IReadOnlyDictionary<string, string> IntegrationRecipes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["aspire"] = "# Aspire Dashboard\n\nUse `dotisan dev --observability`. Configure OTLP through `OTEL_EXPORTER_OTLP_ENDPOINT`.\n",
        ["sendgrid"] = "# SendGrid\n\nAdd a provider adapter implementing `IEmailProvider`. Configure `SendGrid:ApiKey` and `SendGrid:From` through deployment secrets.\n",
        ["mailgun"] = "# Mailgun\n\nAdd a provider adapter implementing `IEmailProvider`. Configure `Mailgun:ApiKey`, `Mailgun:Domain`, and `Mailgun:From` through deployment secrets.\n",
        ["signoz"] = "# SigNoz\n\nSet `OTEL_EXPORTER_OTLP_ENDPOINT` to the SigNoz collector endpoint and enable export with `OpenTelemetry:Enabled=true`.\n",
        ["notifications"] = "# Notifications\n\nGenerate with `dotisan new <Name> --notifications yes`. Readiness: development-adapter. Add retention, pagination, and delivery monitoring before production use.\n",
        ["storage"] = "# File storage\n\nGenerate with `dotisan new <Name> --storage yes`. Readiness: example-only. Add durable metadata/ownership and implement a reviewed provider for multi-node production.\n",
        ["caching"] = "# Caching\n\nGenerate with `dotisan new <Name> --caching yes`. Readiness: development-adapter. Configure a distributed provider for multi-instance production.\n",
        ["imports-exports"] = "# Imports and exports\n\nGenerate with `dotisan new <Name> --imports-exports yes`. Readiness: development-adapter. Generated source includes bounded CSV/JSON uploads, EF-backed status, and a queued handler; add durable payload storage, domain processing, retention, and monitoring before production use.\n",
        ["webhooks"] = "# Webhooks\n\nGenerate with `dotisan new <Name> --webhooks yes`. Readiness: development-adapter. Generated source includes HMAC signing, URL validation, timeout, durable background dispatch when jobs are enabled, retries, delivery history, fresh replay signatures, and endpoint authorization; add destination policy, secret rotation, and observability before production use.\n"
    };
}

internal sealed class RemoveIntegrationCommand : WorkspaceCommand
{
    public override string Name => "remove:integration";
    public override string Description => "Remove an explicit integration recipe from the workspace.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var force = arguments.Contains("--force", StringComparer.OrdinalIgnoreCase);
        var names = arguments.Where(argument => !argument.Equals("--force", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length != 1)
            return Fail(context.Console, "Usage: dotisan remove:integration <name> --force.", DotisanExitCode.UsageError);
        if (!force)
            return Fail(context.Console, "Refusing to remove an integration without --force. Review the generated source changes first.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services) || services.SolutionPath is null)
            return Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root.");

        var path = Path.Combine(services.WorkingDirectory, ".dotisan", "integrations", names[0].ToLowerInvariant() + ".md");
        if (!File.Exists(path))
            return Fail(context.Console, $"Integration recipe '{names[0]}' was not found.", DotisanExitCode.UsageError);
        File.Delete(path);
        context.Console.WriteLine($"Removed {Path.GetRelativePath(services.WorkingDirectory, path)}. Review any package, configuration, and source changes separately.");
        await Task.CompletedTask;
        return DotisanExitCode.Success;
    }
}

internal sealed class DoctorCommand : WorkspaceCommand
{
    public override string Name => "doctor";
    public override string Description => "Check workspace and production readiness.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument is not "--production"))
            return Task.FromResult(Fail(context.Console, "Usage: dotisan doctor [--production].", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services))
            return Task.FromResult(Fail(context.Console, "Workspace services are unavailable."));

        var report = DoctorService.Inspect(services, arguments.Contains("--production", StringComparer.OrdinalIgnoreCase));
        foreach (var result in report.Results)
        {
            var label = result.Severity switch
            {
                DiagnosticSeverity.Pass => "PASS",
                DiagnosticSeverity.Warning => "WARNING",
                _ => "BLOCKING"
            };
            context.Console.WriteLine($"[{label}] {result.Name}: {result.Message}");
            if (result.Severity != DiagnosticSeverity.Pass && result.Recommendation is not null)
                context.Console.WriteLine($"         {result.Recommendation}");
        }

        return Task.FromResult(report.HasBlockingResults ? DotisanExitCode.GenerationError : DotisanExitCode.Success);
    }
}

internal sealed class MailCommand : WorkspaceCommand
{
    public override string Name => "mail";
    public override string Description => "Show or open the local Mailpit inbox.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument is not "--open"))
            return Task.FromResult(Fail(context.Console, "Usage: dotisan mail [--open].", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.MailProvider != MailProvider.Mailpit)
            return Task.FromResult(Fail(context.Console, "Mailpit is not selected. Generate the project with --mail-provider mailpit."));

        const string url = "http://localhost:8025";
        if (!arguments.Contains("--open", StringComparer.OrdinalIgnoreCase))
        {
            context.Console.WriteLine($"Mailpit inbox: {url}");
            return Task.FromResult(DotisanExitCode.Success);
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            context.Console.WriteLine($"Opened Mailpit inbox: {url}");
            return Task.FromResult(DotisanExitCode.Success);
        }
        catch (InvalidOperationException exception)
        {
            return Task.FromResult(Fail(context.Console, $"Could not open Mailpit inbox: {exception.Message}"));
        }
    }
}

internal sealed class JobsCommand : WorkspaceCommand
{
    public override string Name => "jobs";
    public override string Description => "Inspect generated background job configuration.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!arguments.SequenceEqual(["status"], StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(Fail(context.Console, "Usage: dotisan jobs status.", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.ApiProjectPath is null)
            return Task.FromResult(Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root."));

        var jobsFile = Path.Combine(Path.GetDirectoryName(services.ApiProjectPath)!, "Jobs", "JobRegistration.cs");
        if (!File.Exists(jobsFile))
            return Task.FromResult(Fail(context.Console, "This project does not contain generated jobs support. Regenerate it with a current Dotisan CLI."));

        var enabled = ReadJobsEnabled(services.WorkingDirectory);
        context.Console.WriteLine(enabled ? "Jobs are enabled." : "Jobs are disabled by configuration.");
        context.Console.WriteLine($"Persistence provider: {services.Database}.");
        context.Console.WriteLine($"Registration: {Path.GetRelativePath(services.WorkingDirectory, jobsFile)}");
        return Task.FromResult(DotisanExitCode.Success);
    }

    private static bool ReadJobsEnabled(string root)
    {
        var path = Path.Combine(root, "src");
        var appSettings = Directory.Exists(path)
            ? Directory.GetFiles(root, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault()
            : null;
        if (appSettings is null)
            return true;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(appSettings));
            return !document.RootElement.TryGetProperty("Dotisan", out var dotisan)
                || !dotisan.TryGetProperty("Jobs", out var jobs)
                || !jobs.TryGetProperty("Enabled", out var enabled)
                || enabled.ValueKind != JsonValueKind.False;
        }
        catch (JsonException)
        {
            return true;
        }
    }
}

internal sealed class ScheduleCommand : WorkspaceCommand
{
    public override string Name => "schedule";
    public override string Description => "Inspect generated recurring schedules.";

    public override Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!arguments.SequenceEqual(["list"], StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(Fail(context.Console, "Usage: dotisan schedule list.", DotisanExitCode.UsageError));
        if (!TryGetServices(context, out var services) || services.ApiProjectPath is null)
            return Task.FromResult(Fail(context.Console, "Could not find a generated Dotisan project. Run this command from the project root."));

        var jobsFile = Path.Combine(Path.GetDirectoryName(services.ApiProjectPath)!, "Jobs", "JobRegistration.cs");
        if (!File.Exists(jobsFile))
            return Task.FromResult(Fail(context.Console, "This project does not contain generated scheduling support. Regenerate it with a current Dotisan CLI."));

        var schedules = JobScheduleReader.Read(jobsFile);
        if (schedules.Count == 0)
            context.Console.WriteLine("No recurring schedules are declared.");
        else
            foreach (var schedule in schedules)
                context.Console.WriteLine($"{schedule.Name}: {schedule.MessageType} every {schedule.Interval} ({(schedule.Enabled ? "enabled" : "disabled")})");
        context.Console.WriteLine($"Edit: {Path.GetRelativePath(services.WorkingDirectory, jobsFile)}");
        return Task.FromResult(DotisanExitCode.Success);
    }
}

internal sealed class RunCommand : WorkspaceCommand
{
    public override string Name => "run";
    public override string Description => "Run the API using dotnet run.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count > 0)
            return Fail(context.Console, "Usage: dotisan run.", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");
        if (services.ApiProjectPath is null)
            return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");
        var result = await services.RunAsync("dotnet", ["run", "--project", services.ApiProjectPath], services.WorkingDirectory, context.Console, cancellationToken);
        return result.Success ? DotisanExitCode.Success : Fail(context.Console, result.ErrorMessage ?? "API run failed.");
    }
}

internal sealed class DevCommand : WorkspaceCommand
{
    public override string Name => "dev";
    public override string Description => "Run the API and Vue development servers together.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument is not "--lean" and not "--observability" and not "--mailpit" and not "--environment"))
            return Fail(context.Console, "Usage: dotisan dev [--lean] [--observability] [--mailpit] [--environment <name>].", DotisanExitCode.UsageError);

        var environment = "Development";
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "--environment")
            {
                if (++index >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index]))
                    return Fail(context.Console, "--environment requires a value.", DotisanExitCode.UsageError);
                environment = arguments[index];
            }
        }

        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");
        if (services.ApiProjectPath is null)
            return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");

        var processes = new List<IDotisanProcess>();
        var databaseStartAttempted = false;
        var databaseStarted = false;
        var mailpitStartAttempted = false;
        var mailpitStarted = false;
        var dashboardStartAttempted = false;
        var dashboardStarted = false;
        try
        {
            var proxyMigration = await MigrateLegacyViteProxyAsync(services.FrontendDirectory, context.Console, cancellationToken);
            if (!proxyMigration.Success)
                return Fail(context.Console, proxyMigration.ErrorMessage ?? "Could not prepare the Vue development proxy.");

            if (services.Database != DatabaseProvider.SQLite)
            {
                databaseStartAttempted = true;
                var databaseResult = await services.RunAsync(
                    "docker",
                    ["compose", "up", "-d", "--wait", "--wait-timeout", "120", "database"],
                    services.WorkingDirectory,
                    context.Console,
                    cancellationToken);
                if (!databaseResult.Success)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        context.Console.WriteLine("Stopping development services...");
                        return DotisanExitCode.Success;
                    }

                    return Fail(
                        context.Console,
                        $"Could not start the {services.Database} database service with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {databaseResult.ErrorMessage}");
                }

                databaseStarted = true;
            }

            var mailpitExplicit = arguments.Contains("--mailpit", StringComparer.OrdinalIgnoreCase);
            if (services.MailProvider == MailProvider.Mailpit && (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) || mailpitExplicit))
            {
                mailpitStartAttempted = true;
                var mailpitResult = await services.RunAsync("docker", ["compose", "up", "-d", "--wait", "--wait-timeout", "120", "mailpit"], services.WorkingDirectory, context.Console, cancellationToken);
                if (!mailpitResult.Success)
                    return Fail(context.Console, $"Could not start Mailpit with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {mailpitResult.ErrorMessage}");
                mailpitStarted = true;
            }

            if (arguments.Contains("--observability", StringComparer.OrdinalIgnoreCase))
            {
                dashboardStartAttempted = true;
                var dashboardResult = await services.RunAsync("docker", ["compose", "up", "-d", "dashboard"], services.WorkingDirectory, context.Console, cancellationToken);
                if (!dashboardResult.Success)
                    return Fail(context.Console, $"Could not start the Aspire Dashboard with Docker Compose. Ensure Docker Desktop is installed and running, then retry. {dashboardResult.ErrorMessage}");
                dashboardStarted = true;
                context.Console.WriteLine("Aspire Dashboard: http://localhost:18888");
            }

            var apiArguments = new List<string> { "watch", "--project", services.ApiProjectPath, "run", "--", "--environment", environment, "--urls", $"http://localhost:{services.ApiPort}" };
            if (arguments.Contains("--observability", StringComparer.OrdinalIgnoreCase))
                apiArguments.Add("--dotisan-observability");
            processes.Add(await services.StartAsync("dotnet", apiArguments, services.WorkingDirectory, context.Console, cancellationToken));
            if (!arguments.Contains("--lean", StringComparer.OrdinalIgnoreCase))
            {
                if (services.FrontendDirectory is null)
                    return Fail(context.Console, "Could not find the Vue frontend. Use --lean to run the API only.");
                var packageManager = services is DefaultDotisanServices defaultServices ? defaultServices.PackageManager : "pnpm";
                processes.Add(await services.StartAsync(packageManager, ["run", "dev", "--", "--port", services.WebPort.ToString(System.Globalization.CultureInfo.InvariantCulture)], services.FrontendDirectory, context.Console, cancellationToken));
            }

            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completedTask = await Task.WhenAny(processes.Select(process => process.Completion).Append(cancellationTask));
            if (completedTask == cancellationTask || cancellationToken.IsCancellationRequested)
            {
                context.Console.WriteLine("Stopping development services...");
                await Task.WhenAll(processes.Select(process => process.StopAsync()));
            }

            return DotisanExitCode.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            context.Console.WriteLine("Stopping development services...");
            return DotisanExitCode.Success;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Fail(context.Console, $"Could not start development services: {exception.Message}");
        }
        finally
        {
            foreach (var process in processes)
                await process.DisposeAsync();

            if (databaseStarted || (databaseStartAttempted && cancellationToken.IsCancellationRequested))
            {
                var stopResult = await services.RunAsync(
                    "docker",
                    ["compose", "stop", "database"],
                    services.WorkingDirectory,
                    context.Console,
                    CancellationToken.None);
                if (!stopResult.Success)
                    context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Docker database service.");
            }
            if (mailpitStarted || (mailpitStartAttempted && cancellationToken.IsCancellationRequested))
            {
                var stopResult = await services.RunAsync("docker", ["compose", "stop", "mailpit"], services.WorkingDirectory, context.Console, CancellationToken.None);
                if (!stopResult.Success)
                    context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Mailpit service.");
            }
            if (dashboardStarted || (dashboardStartAttempted && cancellationToken.IsCancellationRequested))
            {
                var stopResult = await services.RunAsync("docker", ["compose", "stop", "dashboard"], services.WorkingDirectory, context.Console, CancellationToken.None);
                if (!stopResult.Success)
                    context.Console.WriteError(stopResult.ErrorMessage ?? "Could not stop the Aspire Dashboard service.");
            }
        }
    }

    private static async Task<DotisanOperationResult> MigrateLegacyViteProxyAsync(
        string? frontendDirectory,
        IConsole console,
        CancellationToken cancellationToken)
    {
        if (frontendDirectory is null)
            return DotisanOperationResult.Succeeded();

        var viteConfigPath = Path.Combine(frontendDirectory, "vite.config.ts");
        if (!File.Exists(viteConfigPath))
            return DotisanOperationResult.Succeeded();

        try
        {
            var content = await File.ReadAllTextAsync(viteConfigPath, cancellationToken);
            const string legacyTarget = "https://localhost:5001";
            const string developmentTarget = "http://localhost:5000";
            if (!content.Contains(legacyTarget, StringComparison.Ordinal))
                return DotisanOperationResult.Succeeded();

            await File.WriteAllTextAsync(viteConfigPath, content.Replace(legacyTarget, developmentTarget, StringComparison.Ordinal), cancellationToken);
            console.WriteLine("Updated the legacy Dotisan Vite proxy to http://localhost:5000.");
            return DotisanOperationResult.Succeeded();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DotisanOperationResult.Failed($"Could not update {viteConfigPath}: {exception.Message}");
        }
    }
}

internal sealed class NewCommand : IDotisanCommand
{
    public string Name => "new";
    public string Description => "Create a new Dotisan application.";

    public async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0 || string.IsNullOrWhiteSpace(arguments[0]))
        {
            context.Console.WriteError("A project name is required. Usage: dotisan new <ProjectName> [--profile minimal|identity|saas|maximal|custom] [--output <directory>] [--yes]");
            return DotisanExitCode.UsageError;
        }

        var name = arguments[0];
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), name);
        var yes = false;
        var database = DatabaseProvider.SQLite;
        var authenticationEnabled = false;
        var registration = RegistrationPolicy.Disabled;
        var multiTenancyEnabled = false;
        var packageManager = PackageManager.Pnpm;
        var mailProvider = MailProvider.Console;
        var notificationsEnabled = false;
        var storageEnabled = false;
        var cachingEnabled = false;
        var importsExportsEnabled = false;
        var webhooksEnabled = false;
        var observabilityEnabled = false;
        var jobsEnabled = false;
        var restore = true;
        string? profile = null;

        for (var index = 1; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--yes":
                case "--quick":
                case "--non-interactive":
                    yes = true;
                    break;
                case "--output":
                    if (!TryReadValue(arguments, ref index, out outputDirectory))
                    {
                        return UsageError(context, "--output requires a directory.");
                    }

                    break;
                case "--database":
                    if (!TryReadValue(arguments, ref index, out var databaseValue) || !TryParseDatabase(databaseValue, out database))
                    {
                        return UsageError(context, "--database must be sqlite, sqlserver, postgresql, or mysql.");
                    }

                    break;
                case "--auth":
                    if (!TryReadValue(arguments, ref index, out var authValue) || !TryParseYesNo(authValue, out authenticationEnabled))
                    {
                        return UsageError(context, "--auth must be yes or no.");
                    }

                    break;
                case "--registration":
                    if (!TryReadValue(arguments, ref index, out var registrationValue) || !TryParseRegistration(registrationValue, out registration))
                    {
                        return UsageError(context, "--registration must be public, invite-only, or disabled.");
                    }

                    break;
                case "--tenant":
                    if (!TryReadValue(arguments, ref index, out var tenantValue) || !TryParseYesNo(tenantValue, out multiTenancyEnabled))
                    {
                        return UsageError(context, "--tenant must be yes or no.");
                    }

                    break;
                case "--package-manager":
                    if (!TryReadValue(arguments, ref index, out var packageValue) || !TryParsePackageManager(packageValue, out packageManager))
                    {
                        return UsageError(context, "--package-manager must be pnpm or npm.");
                    }

                    break;
                case "--profile":
                    if (!TryReadValue(arguments, ref index, out profile) || profile is not ("minimal" or "identity" or "saas" or "maximal" or "custom"))
                        return UsageError(context, "--profile must be minimal, identity, saas, maximal, or custom.");
                    if (profile is not "custom")
                    {
                        authenticationEnabled = profile is "identity" or "saas" or "maximal";
                        registration = authenticationEnabled ? RegistrationPolicy.Public : RegistrationPolicy.Disabled;
                        multiTenancyEnabled = profile is "saas" or "maximal";
                        notificationsEnabled = storageEnabled = cachingEnabled = importsExportsEnabled = webhooksEnabled = profile == "maximal";
                        jobsEnabled = profile != "minimal";
                    }
                    break;
                case "--jobs":
                    if (!TryReadValue(arguments, ref index, out var jobsValue) || jobsValue is not ("none" or "wolverine"))
                        return UsageError(context, "--jobs must be none or wolverine.");
                    jobsEnabled = jobsValue == "wolverine";
                    break;
                case "--mail-provider":
                    if (!TryReadValue(arguments, ref index, out var mailValue) || !TryParseMailProvider(mailValue, out mailProvider))
                        return UsageError(context, "--mail-provider must be console, mailpit, or smtp.");
                    break;
                case "--notifications":
                    if (!TryReadValue(arguments, ref index, out var notificationsValue) || !TryParseYesNo(notificationsValue, out notificationsEnabled))
                        return UsageError(context, "--notifications must be yes or no.");
                    break;
                case "--storage":
                    if (!TryReadValue(arguments, ref index, out var storageValue) || !TryParseYesNo(storageValue, out storageEnabled))
                        return UsageError(context, "--storage must be yes or no.");
                    break;
                case "--caching":
                    if (!TryReadValue(arguments, ref index, out var cachingValue) || !TryParseYesNo(cachingValue, out cachingEnabled))
                        return UsageError(context, "--caching must be yes or no.");
                    break;
                case "--imports-exports":
                    if (!TryReadValue(arguments, ref index, out var importsExportsValue) || !TryParseYesNo(importsExportsValue, out importsExportsEnabled))
                        return UsageError(context, "--imports-exports must be yes or no.");
                    break;
                case "--webhooks":
                    if (!TryReadValue(arguments, ref index, out var webhooksValue) || !TryParseYesNo(webhooksValue, out webhooksEnabled))
                        return UsageError(context, "--webhooks must be yes or no.");
                    break;
                case "--observability":
                    if (!TryReadValue(arguments, ref index, out var observabilityValue) || !TryParseYesNo(observabilityValue, out observabilityEnabled))
                        return UsageError(context, "--observability must be yes or no.");
                    break;
                case "--no-restore":
                    restore = false;
                    break;
                default:
                    return UsageError(context, $"Unknown option '{arguments[index]}'.");
            }
        }

        if (profile is not null and not "custom")
        {
            var expectedAuth = profile is "identity" or "saas" or "maximal";
            var expectedTenancy = profile is "saas" or "maximal";
            var expectedIntegrations = profile == "maximal";
            var expectedJobs = profile != "minimal";
            if (authenticationEnabled != expectedAuth
                || (authenticationEnabled ? registration : RegistrationPolicy.Disabled) != (expectedAuth ? RegistrationPolicy.Public : RegistrationPolicy.Disabled)
                || multiTenancyEnabled != expectedTenancy
                || notificationsEnabled != expectedIntegrations
                || storageEnabled != expectedIntegrations
                || cachingEnabled != expectedIntegrations
                || importsExportsEnabled != expectedIntegrations
                || webhooksEnabled != expectedIntegrations
                || observabilityEnabled
                || jobsEnabled != expectedJobs)
            {
                return UsageError(context, "Profile options conflict with explicit feature flags. Use --profile custom for manual overrides.");
            }
        }

        ProjectOptions? options;
        try
        {
            options = yes
                ? ProjectOptions.Quick(name, outputDirectory) with
                {
                    Database = database,
                    AuthenticationEnabled = authenticationEnabled,
                    Registration = registration,
                    MultiTenancyEnabled = multiTenancyEnabled,
                    PackageManager = packageManager
                    ,
                    MailProvider = mailProvider
                    ,
                    NotificationsEnabled = notificationsEnabled
                    ,
                    StorageEnabled = storageEnabled
                    ,
                    CachingEnabled = cachingEnabled
                    ,
                    ImportsExportsEnabled = importsExportsEnabled
                    ,
                    WebhooksEnabled = webhooksEnabled
                    ,
                    ObservabilityEnabled = observabilityEnabled
                    ,
                    JobsEnabled = jobsEnabled
                    ,
                    Profile = profile switch
                    {
                        "identity" => ProjectProfile.Identity,
                        "saas" => ProjectProfile.Saas,
                        "maximal" => ProjectProfile.Maximal,
                        "custom" => ProjectProfile.Custom,
                        _ => (database != DatabaseProvider.SQLite
                            || authenticationEnabled
                            || registration != RegistrationPolicy.Disabled
                            || multiTenancyEnabled
                            || notificationsEnabled
                            || storageEnabled
                            || cachingEnabled
                            || importsExportsEnabled
                            || webhooksEnabled
                            || jobsEnabled)
                            ? ProjectProfile.Custom
                            : ProjectProfile.Minimal
                    }
                    ,
                    JobProvider = jobsEnabled ? JobProvider.Wolverine : JobProvider.None
                }
                : context.Prompts.AskForProject(name, outputDirectory);
        }
        catch (ArgumentException exception)
        {
            return UsageError(context, exception.Message);
        }

        if (options is null)
            return DotisanExitCode.Canceled;

        if ((options.ImportsExportsEnabled || options.WebhooksEnabled) && !options.JobsEnabled)
        {
            context.Console.WriteError("Imports/exports and webhooks require --jobs wolverine because their work is dispatched asynchronously.");
            return DotisanExitCode.UsageError;
        }

        var result = await context.ProjectGenerator.GenerateAsync(options, cancellationToken);
        if (!result.Success)
        {
            context.Console.WriteError(result.ErrorMessage ?? "Project generation failed.");
            return DotisanExitCode.GenerationError;
        }

        context.Console.WriteLine($"Created {options.Name} in {result.OutputDirectory}.");
        var apiProjectPath = Path.Combine("src", $"{options.Name}.Api");
        if (context.Services is not null)
        {
            var prerequisiteResult = await DefaultPrerequisiteChecker.CheckAsync(options.PackageManager, options.Database, context.Services, cancellationToken);
            if (prerequisiteResult.Missing.Count == 0)
            {
                var packageManagerName = options.PackageManager == PackageManager.Npm ? "npm" : "pnpm";
                var databasePrerequisite = options.Database == DatabaseProvider.SQLite
                    ? string.Empty
                    : " Docker and the Docker daemon are ready.";
                context.Console.WriteLine($"Prerequisite check passed: .NET SDK, dotnet-ef, and {packageManagerName} are installed.{databasePrerequisite}");
            }
            else
            {
                context.Console.WriteError($"Prerequisite check found {prerequisiteResult.Missing.Count} missing tool(s):");
                foreach (var missing in prerequisiteResult.Missing)
                {
                    context.Console.WriteError($"Missing prerequisite: {missing.Name}");
                    context.Console.WriteError($"  Install with: {missing.InstallCommand}");
                    context.Console.WriteError($"  Verify with: {missing.VerifyCommand}");
                }
            }

            if (restore)
            {
                var solutionPath = Path.Combine(result.OutputDirectory, $"{options.Name}.sln");
                if (!prerequisiteResult.IsMissing(".NET SDK"))
                {
                    var restoreResult = await context.Services.RunAsync("dotnet", ["restore", solutionPath], result.OutputDirectory, context.Console, cancellationToken);
                    if (!restoreResult.Success)
                    {
                        context.Console.WriteError(restoreResult.ErrorMessage ?? "The generated project was created, but dotnet restore failed.");
                        context.Console.WriteError("You can retry with 'dotnet restore' from the generated project directory.");
                        return DotisanExitCode.GenerationError;
                    }

                }

                var packageManagerName = options.PackageManager == PackageManager.Npm ? "npm" : "pnpm";
                var frontendDirectory = Path.Combine(result.OutputDirectory, "src", $"{options.Name}.Web");
                if (prerequisiteResult.IsMissing(packageManagerName))
                {
                    context.Console.WriteError($"The project was created, but frontend dependency installation was skipped because {packageManagerName} is missing.");
                }
                else
                {
                    var packageManagerCommand = DefaultPrerequisiteChecker.PackageManagerCommand(options.PackageManager);
                    context.Console.WriteLine($"Installing frontend dependencies with {packageManagerName}...");
                    var installResult = await context.Services.RunAsync(packageManagerCommand, ["install"], frontendDirectory, context.Console, cancellationToken);
                    if (!installResult.Success)
                    {
                        context.Console.WriteError("The project was created, but frontend dependency installation failed.");
                        if (!string.IsNullOrWhiteSpace(installResult.ErrorMessage))
                            context.Console.WriteError(installResult.ErrorMessage);
                        context.Console.WriteError($"You can retry with '{packageManagerName} install' from the generated frontend directory.");
                        return DotisanExitCode.GenerationError;
                    }

                    var lockfileName = options.PackageManager == PackageManager.Npm
                        ? "package-lock.json"
                        : "pnpm-lock.yaml";
                    var lockfilePath = Path.Combine(frontendDirectory, lockfileName);
                    if (!File.Exists(lockfilePath))
                    {
                        context.Console.WriteError($"The project was created, but {packageManagerName} did not produce the required {lockfileName}.");
                        context.Console.WriteError($"Create {lockfileName} with '{packageManagerName} install' before building or deploying the generated application.");
                        return DotisanExitCode.GenerationError;
                    }
                }

            }
        }

        context.Console.WriteLine($"Next: cd {Path.GetRelativePath(Directory.GetCurrentDirectory(), result.OutputDirectory)}");
        WriteMigrationGuidance(context.Console, options, apiProjectPath);
        context.Console.WriteLine("Then run: dotisan dev");
        return DotisanExitCode.Success;
    }

    private static void WriteMigrationGuidance(IConsole console, ProjectOptions options, string apiProjectPath)
    {
        var migrationName = options.AuthenticationEnabled ? "InitialIdentity" : "InitialCreate";
        var providerName = options.Database switch
        {
            DatabaseProvider.SQLite => "SQLite",
            DatabaseProvider.SqlServer => "SQL Server",
            DatabaseProvider.PostgreSQL => "PostgreSQL",
            DatabaseProvider.MySQL => "MySQL",
            _ => options.Database.ToString()
        };

        console.WriteLine($"Database provider: {providerName}.");
        console.WriteLine("Schema steps are explicit; dotisan new does not create or apply migrations:");
        console.WriteLine($"  dotnet ef migrations add {migrationName} --project {apiProjectPath}");
        if (options.Database is DatabaseProvider.SqlServer or DatabaseProvider.PostgreSQL or DatabaseProvider.MySQL)
            console.WriteLine("  dotisan dev  # starts the local database service");
        console.WriteLine("  dotisan migrate  # applies reviewed, committed migrations");
    }

    private static DotisanExitCode UsageError(CommandContext context, string message)
    {
        context.Console.WriteError(message);
        return DotisanExitCode.UsageError;
    }

    private static bool TryReadValue(IReadOnlyList<string> arguments, ref int index, out string value)
    {
        if (++index >= arguments.Count)
        {
            value = string.Empty;
            return false;
        }

        value = arguments[index];
        return !string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal);
    }

    private static bool TryParseDatabase(string value, out DatabaseProvider database)
    {
        var normalized = value.ToLowerInvariant();
        database = normalized switch
        {
            "sqlite" => DatabaseProvider.SQLite,
            "sqlserver" => DatabaseProvider.SqlServer,
            "postgresql" or "postgres" => DatabaseProvider.PostgreSQL,
            "mysql" => DatabaseProvider.MySQL,
            _ => default
        };
        return normalized is "sqlite" or "sqlserver" or "postgresql" or "postgres" or "mysql";
    }

    private static bool TryParseYesNo(string value, out bool result)
    {
        var normalized = value.ToLowerInvariant();
        result = normalized switch
        {
            "yes" or "true" => true,
            "no" or "false" => false,
            _ => false
        };
        return normalized is "yes" or "true" or "no" or "false";
    }

    private static bool TryParseRegistration(string value, out RegistrationPolicy registration)
    {
        var normalized = value.ToLowerInvariant();
        registration = normalized switch
        {
            "public" => RegistrationPolicy.Public,
            "invite-only" or "invite" => RegistrationPolicy.InviteOnly,
            "disabled" or "none" => RegistrationPolicy.Disabled,
            _ => default
        };
        return normalized is "public" or "invite-only" or "invite" or "disabled" or "none";
    }

    private static bool TryParsePackageManager(string value, out PackageManager packageManager)
    {
        var normalized = value.ToLowerInvariant();
        packageManager = normalized switch
        {
            "pnpm" => PackageManager.Pnpm,
            "npm" => PackageManager.Npm,
            _ => default
        };
        return normalized is "pnpm" or "npm";
    }

    private static bool TryParseMailProvider(string value, out MailProvider provider)
    {
        var normalized = value.ToLowerInvariant();
        provider = normalized switch
        {
            "console" => MailProvider.Console,
            "mailpit" => MailProvider.Mailpit,
            "smtp" => MailProvider.Smtp,
            _ => default
        };
        return normalized is "console" or "mailpit" or "smtp";
    }
}
