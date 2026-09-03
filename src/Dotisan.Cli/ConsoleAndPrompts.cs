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
    private readonly Func<ConsoleKeyInfo> readKey;
    private readonly bool useKeyReader;

    public DefaultPrompts(IConsole? console = null, Func<bool>? isInputRedirected = null, Func<string?>? readLine = null, Func<ConsoleKeyInfo>? readKey = null)
    {
        this.console = console ?? new SystemConsole();
        this.isInputRedirected = isInputRedirected ?? (() => Console.IsInputRedirected);
        this.readLine = readLine ?? Console.ReadLine;
        this.readKey = readKey ?? (() => Console.ReadKey(intercept: true));
        useKeyReader = readKey is not null || (isInputRedirected is null && readLine is null);
    }

    public ProjectOptions? AskForProject(string name, string outputDirectory)
    {
        if (isInputRedirected())
        {
            return ProjectOptions.Quick(name, outputDirectory);
        }

        if (useKeyReader)
            return AskWithKeyboard(name, outputDirectory);

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
        var mailProvider = MailProvider.Console;
        if (authenticationEnabled)
        {
            mailProvider = ReadChoice("Mail provider", "console", ["console", "mailpit", "smtp"]) switch
            {
                "mailpit" => MailProvider.Mailpit,
                "smtp" => MailProvider.Smtp,
                _ => MailProvider.Console
            };
        }

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
            ,MailProvider = mailProvider
        };
    }

    private ProjectOptions? AskWithKeyboard(string name, string outputDirectory)
    {
        ShowBanner();
        var database = DatabaseProvider.SQLite;
        var authenticationEnabled = false;
        var registration = RegistrationPolicy.Disabled;
        var multiTenancyEnabled = false;
        var packageManager = PackageManager.Pnpm;
        var mailProvider = MailProvider.Console;
        var step = 0;

        while (true)
        {
            var steps = authenticationEnabled
                ? new[] { "Database", "Authentication", "Registration", "Multi-tenancy", "Package manager", "Mail provider" }
                : new[] { "Database", "Authentication", "Multi-tenancy", "Package manager" };
            if (step >= steps.Length)
            {
                var reviewChoice = ShowReview(name, outputDirectory, database, authenticationEnabled, registration, multiTenancyEnabled, packageManager, mailProvider);
                if (reviewChoice == TuiChoice.CancelValue)
                    return Cancel();
                if (reviewChoice == TuiChoice.BackValue)
                {
                    step = steps.Length - 1;
                    continue;
                }

                return ProjectOptions.Quick(name, outputDirectory) with
                {
                    Database = database,
                    AuthenticationEnabled = authenticationEnabled,
                    Registration = registration,
                    MultiTenancyEnabled = multiTenancyEnabled,
                    PackageManager = packageManager,
                    MailProvider = mailProvider
                };
            }

            var label = steps[step];
            string[] choices = label switch
            {
                "Database" => ["SQLite", "SQL Server", "PostgreSQL", "MySQL"],
                "Authentication" or "Multi-tenancy" => ["No", "Yes"],
                "Registration" => ["Public", "Invite only", "Disabled"],
                "Package manager" => ["pnpm", "npm"],
                _ => ["console", "mailpit", "smtp"]
            };
            var defaultIndex = label switch
            {
                "Database" => (int)database,
                "Authentication" => authenticationEnabled ? 1 : 0,
                "Registration" => registration switch { RegistrationPolicy.InviteOnly => 1, RegistrationPolicy.Disabled => 2, _ => 0 },
                "Multi-tenancy" => multiTenancyEnabled ? 1 : 0,
                "Package manager" => packageManager == PackageManager.Npm ? 1 : 0,
                _ => (int)mailProvider
            };

            var result = ReadTuiChoice(label, choices, Math.Clamp(defaultIndex, 0, choices.Length - 1), step == 0);
            if (result.Value == TuiChoice.CancelValue)
                return Cancel();
            if (result.Value == TuiChoice.BackValue)
            {
                if (step == 0)
                    return Cancel();
                step--;
                continue;
            }

            switch (label)
            {
                case "Database":
                    database = result.Value switch { 1 => DatabaseProvider.SqlServer, 2 => DatabaseProvider.PostgreSQL, 3 => DatabaseProvider.MySQL, _ => DatabaseProvider.SQLite };
                    break;
                case "Authentication":
                    authenticationEnabled = result.Value == 1;
                    if (!authenticationEnabled)
                    {
                        registration = RegistrationPolicy.Disabled;
                        mailProvider = MailProvider.Console;
                    }
                    break;
                case "Registration":
                    registration = result.Value switch { 1 => RegistrationPolicy.InviteOnly, 2 => RegistrationPolicy.Disabled, _ => RegistrationPolicy.Public };
                    break;
                case "Multi-tenancy":
                    multiTenancyEnabled = result.Value == 1;
                    break;
                case "Package manager":
                    packageManager = result.Value == 1 ? PackageManager.Npm : PackageManager.Pnpm;
                    break;
                case "Mail provider":
                    mailProvider = result.Value switch { 1 => MailProvider.Mailpit, 2 => MailProvider.Smtp, _ => MailProvider.Console };
                    break;
            }

            step++;
        }
    }

    private int ShowReview(string name, string outputDirectory, DatabaseProvider database, bool authenticationEnabled, RegistrationPolicy registration, bool multiTenancyEnabled, PackageManager packageManager, MailProvider mailProvider)
    {
        ShowBanner();
        console.WriteLine("Review your settings\n");
        console.WriteLine($"  Project       {name}");
        console.WriteLine($"  Output        {outputDirectory}");
        console.WriteLine($"  Database      {database}");
        console.WriteLine($"  Authentication {(authenticationEnabled ? "Enabled" : "Disabled")}");
        if (authenticationEnabled)
        {
            console.WriteLine($"  Registration  {registration}");
            console.WriteLine($"  Mail provider {mailProvider}");
        }
        console.WriteLine($"  Multi-tenancy {(multiTenancyEnabled ? "Enabled" : "Disabled")}");
        console.WriteLine($"  Package       {packageManager}\n");
        var choice = ReadTuiChoice("What would you like to do?", ["Create project", "Back", "Cancel"], 0, false);
        return choice.Value switch
        {
            1 => TuiChoice.BackValue,
            2 => TuiChoice.CancelValue,
            _ => 0
        };
    }

    private TuiChoice ReadTuiChoice(string label, string[] choices, int selected, bool firstStep)
    {
        while (true)
        {
            ShowBanner();
            console.WriteLine($"{label}\n");
            for (var index = 0; index < choices.Length; index++)
                console.WriteLine($"  {(index == selected ? "❯" : " ")} {choices[index]}{(index == selected ? "  (default)" : string.Empty)}");
            console.WriteLine("\n  ↑↓ Navigate  Enter Select  ← Back  Esc Cancel");
            var key = readKey();
            if (key.Key == ConsoleKey.UpArrow)
                selected = (selected + choices.Length - 1) % choices.Length;
            else if (key.Key == ConsoleKey.DownArrow)
                selected = (selected + 1) % choices.Length;
            else if (key.Key == ConsoleKey.Enter)
                return new TuiChoice(selected);
            else if (key.Key == ConsoleKey.LeftArrow)
                return new TuiChoice(firstStep ? TuiChoice.CancelValue : TuiChoice.BackValue);
            else if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                return new TuiChoice(TuiChoice.CancelValue);
        }
    }

    private ProjectOptions? Cancel()
    {
        console.WriteLine("\nCancelled. No files were created.");
        return null;
    }

    private void ShowBanner() => console.WriteLine("\u001b[2J\u001b[H  ____        _   _                 \n |  _ \\  ___ | |_(_)___  __ _ _ __  \n | | | |/ _ \\| __| / __|/ _` | '_ \\ \n | |_| | (_) | |_| \\__ \\ (_| | | | |\n |____/ \\___/ \\__|_|___/\\__,_|_| |_|\n\n  Build boldly. Ship cleanly.\n");

    private readonly record struct TuiChoice(int Value)
    {
        public const int BackValue = -1;
        public const int CancelValue = -2;
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
