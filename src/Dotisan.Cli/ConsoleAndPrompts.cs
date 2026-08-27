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

        var mode = ReadChoice("Setup mode", "Quick", ["Quick", "Advanced"]);
        if (mode.Equals("Advanced", StringComparison.OrdinalIgnoreCase))
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
        console.WriteLine($"{label} (default: {defaultValue})");
        for (var index = 0; index < choices.Count; index++)
        {
            console.WriteLine($"  {index + 1}) {choices[index]}");
        }

        var value = readLine()?.Trim();
        if (int.TryParse(value, out var choiceNumber) && choiceNumber >= 1 && choiceNumber <= choices.Count)
            return choices[choiceNumber - 1];

        return choices.FirstOrDefault(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase)) ?? defaultValue;
    }

    private bool ReadYesNo(string label, bool defaultValue)
    {
        var value = ReadChoice(label, defaultValue ? "Yes" : "No", ["No", "Yes"]);
        return value.Equals("Yes", StringComparison.OrdinalIgnoreCase);
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
