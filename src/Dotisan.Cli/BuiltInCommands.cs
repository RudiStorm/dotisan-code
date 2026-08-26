using Dotisan.Core;

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

internal sealed class NewCommand : IDotisanCommand
{
    public string Name => "new";
    public string Description => "Create a new Dotisan application.";

    public async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0 || string.IsNullOrWhiteSpace(arguments[0]))
        {
            context.Console.WriteError("A project name is required. Usage: dotisan new <ProjectName> [--output <directory>] [--yes]");
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
                case "--no-restore":
                    break;
                default:
                    return UsageError(context, $"Unknown option '{arguments[index]}'.");
            }
        }

        ProjectOptions options;
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
                }
                : context.Prompts.AskForProject(name, outputDirectory);
        }
        catch (ArgumentException exception)
        {
            return UsageError(context, exception.Message);
        }

        var result = await context.ProjectGenerator.GenerateAsync(options, cancellationToken);
        if (!result.Success)
        {
            context.Console.WriteError(result.ErrorMessage ?? "Project generation failed.");
            return DotisanExitCode.GenerationError;
        }

        context.Console.WriteLine($"Created {options.Name} in {result.OutputDirectory}.");
        context.Console.WriteLine($"Next: cd {Path.GetRelativePath(Directory.GetCurrentDirectory(), result.OutputDirectory)}");
        context.Console.WriteLine("Then run: dotnet build");
        return DotisanExitCode.Success;
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
}
