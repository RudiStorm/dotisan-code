using System.Text.Json;
using Dotisan.Cli.Diagnostics;
using Dotisan.Cli.Generation;
using Dotisan.Core;
using Dotisan.Core.Diagnostics;
using Dotisan.Core.Jobs;
using Dotisan.Generators;

namespace Dotisan.Cli;

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
        console.WriteLine("Development schema setup is automatic; dotisan dev creates the initial migration when needed and applies it:");
        console.WriteLine("  dotisan dev");
        console.WriteLine($"For production, author and review a migration with: dotnet ef migrations add {migrationName} --project {apiProjectPath}");
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


