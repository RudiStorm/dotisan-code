using Dotisan.Core;

namespace Dotisan.Cli;

public sealed class SystemConsole : IConsole
{
    public void WriteLine(string message) => Console.WriteLine(message);

    public void WriteError(string message) => Console.Error.WriteLine(message);
}

public sealed class DefaultPrompts : IPrompts
{
    private readonly IConsole console;
    private readonly Func<bool> isInputRedirected;
    private readonly Func<string?> readLine;

    public DefaultPrompts(IConsole? console = null, Func<bool>? isInputRedirected = null, Func<string?>? readLine = null)
    {
        this.console = console ?? new SystemConsole();
        this.isInputRedirected = isInputRedirected ?? (() => Console.IsInputRedirected);
        this.readLine = readLine ?? Console.ReadLine;
    }

    public ProjectOptions AskForProject(string name, string outputDirectory)
    {
        if (isInputRedirected())
        {
            return ProjectOptions.Quick(name, outputDirectory);
        }

        console.WriteLine("Setup mode:");
        console.WriteLine("> Quick");
        console.WriteLine("  Advanced");
        console.WriteLine("Press Enter for Quick.");
        var mode = readLine();
        if (!string.IsNullOrWhiteSpace(mode) && mode.StartsWith("a", StringComparison.OrdinalIgnoreCase))
        {
            console.WriteLine("Advanced setup is reserved for a future release; continuing with Quick defaults.");
        }

        var database = ReadChoice("Database", "SQLite", ["SQLite", "SQL Server", "PostgreSQL", "MySQL"]);
        var authenticationEnabled = ReadYesNo("Authentication", false);
        var registration = authenticationEnabled
            ? ReadRegistration()
            : RegistrationPolicy.Disabled;
        var multiTenancyEnabled = ReadYesNo("Multi-tenancy", false);
        var packageManager = ReadChoice("Package manager", "pnpm", ["pnpm", "npm"]) == "npm"
            ? PackageManager.Npm
            : PackageManager.Pnpm;

        return ProjectOptions.Quick(name, outputDirectory) with
        {
            Database = database switch
            {
                "SQL Server" => DatabaseProvider.SqlServer,
                "PostgreSQL" => DatabaseProvider.PostgreSQL,
                "MySQL" => DatabaseProvider.MySQL,
                _ => DatabaseProvider.SQLite
            },
            AuthenticationEnabled = authenticationEnabled,
            Registration = registration,
            MultiTenancyEnabled = multiTenancyEnabled,
            PackageManager = packageManager
        };
    }

    private string ReadChoice(string label, string defaultValue, IReadOnlyList<string> choices)
    {
        console.WriteLine($"{label}: {string.Join(" / ", choices)} (default: {defaultValue})");
        var value = readLine();
        return choices.FirstOrDefault(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase)) ?? defaultValue;
    }

    private bool ReadYesNo(string label, bool defaultValue)
    {
        console.WriteLine($"{label}: {(defaultValue ? "Yes" : "No")} (default)");
        var value = readLine();
        return value?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true
            || value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || (string.IsNullOrWhiteSpace(value) && defaultValue);
    }

    private RegistrationPolicy ReadRegistration()
    {
        var value = ReadChoice("Registration", "Public", ["Public", "Invite only", "Disabled"]);
        return value switch
        {
            "Invite only" => RegistrationPolicy.InviteOnly,
            "Disabled" => RegistrationPolicy.Disabled,
            _ => RegistrationPolicy.Public
        };
    }
}
