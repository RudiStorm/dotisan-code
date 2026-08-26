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

public sealed record ProjectOptions(
    string Name,
    string OutputDirectory,
    DatabaseProvider Database,
    bool AuthenticationEnabled,
    RegistrationPolicy Registration,
    bool MultiTenancyEnabled,
    PackageManager PackageManager)
{
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
}
