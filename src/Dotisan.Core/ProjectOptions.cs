using System.Text.RegularExpressions;

namespace Dotisan.Core;

public enum DatabaseProvider
{
    SQLite,
    SqlServer,
    PostgreSQL,
    MySQL
}

public enum RegistrationPolicy
{
    Disabled,
    Public,
    InviteOnly
}

public enum PackageManager
{
    Pnpm,
    Npm
}

public enum ProjectProfile
{
    Minimal,
    Identity,
    Saas,
    Maximal,
    Custom
}

public enum JobProvider
{
    None,
    Wolverine
}

public enum MailProvider
{
    Console,
    Mailpit,
    Smtp
}

public sealed record ProjectOptions(
    string Name,
    string OutputDirectory,
    DatabaseProvider Database,
    bool AuthenticationEnabled,
    RegistrationPolicy Registration,
    bool MultiTenancyEnabled,
    PackageManager PackageManager)
{
    public ProjectProfile Profile { get; init; } = ProjectProfile.Minimal;
    public JobProvider JobProvider { get; init; } = JobProvider.None;
    public MailProvider MailProvider { get; init; } = MailProvider.Console;
    public bool NotificationsEnabled { get; init; }
    public bool StorageEnabled { get; init; }
    public bool CachingEnabled { get; init; }
    public bool ImportsExportsEnabled { get; init; }
    public bool WebhooksEnabled { get; init; }
    public bool JobsEnabled { get; init; }
    private static readonly Regex ValidName = new("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ProjectOptions Quick(string name, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(name) || !ValidName.IsMatch(name))
        {
            throw new ArgumentException("Project names must start with a letter and contain only letters, numbers, hyphens, or underscores.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        }

        return new ProjectOptions(
            name,
            outputDirectory,
            DatabaseProvider.SQLite,
            AuthenticationEnabled: false,
            Registration: RegistrationPolicy.Disabled,
            MultiTenancyEnabled: false,
            PackageManager.Pnpm);
    }

    public ProjectOptions WithProfile(ProjectProfile profile)
    {
        var authentication = profile is ProjectProfile.Identity or ProjectProfile.Saas or ProjectProfile.Maximal;
        var tenancy = profile is ProjectProfile.Saas or ProjectProfile.Maximal;
        var integrations = profile is ProjectProfile.Maximal;
        var jobs = profile is not ProjectProfile.Minimal;

        return this with
        {
            Profile = profile,
            AuthenticationEnabled = authentication,
            Registration = authentication ? RegistrationPolicy.Public : RegistrationPolicy.Disabled,
            MultiTenancyEnabled = tenancy,
            NotificationsEnabled = integrations,
            StorageEnabled = integrations,
            CachingEnabled = integrations,
            ImportsExportsEnabled = integrations,
            WebhooksEnabled = integrations,
            JobsEnabled = jobs,
            JobProvider = jobs ? JobProvider.Wolverine : JobProvider.None
        };
    }
}
