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

internal sealed class MakeResourceCommand : WorkspaceCommand
{
    public override string Name => "make:resource";
    public override string Description => "Create a model and vertical CRUD endpoint slice.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            return Fail(context.Console, "Usage: dotisan make:resource <ResourceName>", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var result = await services.ScaffoldResourceAsync(arguments[0], cancellationToken);
        if (!result.Success)
            return Fail(context.Console, result.ErrorMessage ?? "Resource scaffolding failed.");
        context.Console.WriteLine($"Created {arguments[0]} resource files.");
        return DotisanExitCode.Success;
    }
}

internal sealed class MakeEndpointCommand : WorkspaceCommand
{
    public override string Name => "make:endpoint";
    public override string Description => "Create a single-file vertical endpoint.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count != 1 || string.IsNullOrWhiteSpace(arguments[0]))
            return Fail(context.Console, "Usage: dotisan make:endpoint <EndpointName>", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var result = await services.ScaffoldEndpointAsync(arguments[0], cancellationToken);
        if (!result.Success)
            return Fail(context.Console, result.ErrorMessage ?? "Endpoint scaffolding failed.");
        context.Console.WriteLine($"Created {arguments[0]} endpoint.");
        return DotisanExitCode.Success;
    }
}

internal sealed class MigrateCommand : WorkspaceCommand
{
    public override string Name => "migrate";
    public override string Description => "Apply committed EF Core migrations.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");

        var efArguments = new List<string> { "ef" };
        if (arguments.SequenceEqual(["status"], StringComparer.OrdinalIgnoreCase))
            efArguments.AddRange(["migrations", "list"]);
        else if (arguments.Count == 0 || arguments.SequenceEqual(["--production"], StringComparer.OrdinalIgnoreCase))
            efArguments.AddRange(["database", "update"]);
        else
            return Fail(context.Console, "Usage: dotisan migrate [status|--production]. Use 'dotnet ef' directly for migration authoring and rollback.", DotisanExitCode.UsageError);

        if (services.ApiProjectPath is null)
            return Fail(context.Console, "Could not find an API project. Run this command from a generated Dotisan project.");
        efArguments.AddRange(["--project", services.ApiProjectPath]);

        var result = await services.RunAsync("dotnet", efArguments, services.WorkingDirectory, context.Console, cancellationToken);
        if (result.Success)
            return DotisanExitCode.Success;
        context.Console.WriteError(result.ErrorMessage ?? "EF Core migration command failed.");
        context.Console.WriteError("If dotnet-ef is not installed, run 'dotnet tool install --global dotnet-ef' and retry.");
        return DotisanExitCode.GenerationError;
    }
}

internal sealed class BuildCommand : WorkspaceCommand
{
    public override string Name => "build";
    public override string Description => "Build the API and Vue frontend.";

    public override async Task<DotisanExitCode> ExecuteAsync(CommandContext context, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var frontend = !arguments.Contains("--no-frontend", StringComparer.OrdinalIgnoreCase);
        if (arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && !argument.Equals("--no-frontend", StringComparison.OrdinalIgnoreCase)))
            return Fail(context.Console, "Usage: dotisan build [--no-frontend].", DotisanExitCode.UsageError);
        if (!TryGetServices(context, out var services))
            return Fail(context.Console, "Workspace services are unavailable.");
        if (services.SolutionPath is null)
            return Fail(context.Console, "Could not find a solution. Run this command from a generated Dotisan project.");

        var api = await services.RunAsync("dotnet", ["build", services.SolutionPath], services.WorkingDirectory, context.Console, cancellationToken);
        if (!api.Success)
            return Fail(context.Console, api.ErrorMessage ?? "API build failed.");
        if (!frontend)
            return DotisanExitCode.Success;

        var packageManager = services is DefaultDotisanServices defaultServices ? defaultServices.PackageManager : "pnpm";
        if (services.FrontendDirectory is null)
            return Fail(context.Console, "Could not find the Vue frontend. Use --no-frontend to build the API only.");
        var web = await services.RunAsync(packageManager, ["run", "build"], services.FrontendDirectory, context.Console, cancellationToken);
        return web.Success ? DotisanExitCode.Success : Fail(context.Console, web.ErrorMessage ?? "Frontend build failed.");
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
        if (arguments.Any(argument => argument.StartsWith("--", StringComparison.Ordinal) && argument is not "--lean" and not "--observability" and not "--environment"))
            return Fail(context.Console, "Usage: dotisan dev [--lean] [--observability] [--environment <name>].", DotisanExitCode.UsageError);

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
        try
        {
            processes.Add(await services.StartAsync("dotnet", ["watch", "--project", services.ApiProjectPath, "run", "--", "--environment", environment], services.WorkingDirectory, context.Console, cancellationToken));
            if (!arguments.Contains("--lean", StringComparer.OrdinalIgnoreCase))
            {
                if (services.FrontendDirectory is null)
                    return Fail(context.Console, "Could not find the Vue frontend. Use --lean to run the API only.");
                var packageManager = services is DefaultDotisanServices defaultServices ? defaultServices.PackageManager : "pnpm";
                processes.Add(await services.StartAsync(packageManager, ["run", "dev"], services.FrontendDirectory, context.Console, cancellationToken));
            }

            await Task.WhenAny(processes.Select(process => process.Completion));
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
        var restore = true;

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
                    restore = false;
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
        if (context.Services is not null)
        {
            var prerequisiteResult = await DefaultPrerequisiteChecker.CheckAsync(options.PackageManager, context.Services, cancellationToken);
            if (prerequisiteResult.Missing.Count == 0)
            {
                var packageManagerName = options.PackageManager == PackageManager.Npm ? "npm" : "pnpm";
                context.Console.WriteLine($"Prerequisite check passed: .NET SDK, dotnet-ef, and {packageManagerName} are installed.");
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
                }
            }
        }
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
