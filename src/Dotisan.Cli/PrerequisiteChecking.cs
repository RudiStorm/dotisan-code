using Dotisan.Core;

namespace Dotisan.Cli;

internal sealed record MissingPrerequisite(string Name, string InstallCommand, string VerifyCommand);

internal sealed record PrerequisiteCheckResult(IReadOnlyList<MissingPrerequisite> Missing)
{
    public bool IsMissing(string name) => Missing.Any(prerequisite => prerequisite.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

internal static class DefaultPrerequisiteChecker
{
    public static async Task<PrerequisiteCheckResult> CheckAsync(
        PackageManager packageManager,
        IDotisanServices services,
        CancellationToken cancellationToken)
    {
        var checks = new[]
        {
            new ToolCheck(".NET SDK", "dotnet", ["--version"], "Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0", "dotnet --version"),
            new ToolCheck("dotnet-ef", "dotnet", ["ef", "--version"], "dotnet tool install --global dotnet-ef", "dotnet ef --version"),
            CreatePackageManagerCheck(packageManager)
        };
        var missing = new List<MissingPrerequisite>();

        foreach (var check in checks)
        {
            var result = await services.RunAsync(
                check.FileName,
                check.Arguments,
                services.WorkingDirectory,
                new SilentConsole(),
                cancellationToken);
            if (!result.Success)
                missing.Add(new MissingPrerequisite(check.Name, check.InstallCommand, check.VerifyCommand));
        }

        return new PrerequisiteCheckResult(missing);
    }

    public static string PackageManagerCommand(PackageManager packageManager)
    {
        var name = packageManager == PackageManager.Npm ? "npm" : "pnpm";
        return OperatingSystem.IsWindows() ? $"{name}.cmd" : name;
    }

    private static ToolCheck CreatePackageManagerCheck(PackageManager packageManager) => packageManager switch
    {
        PackageManager.Npm => new("npm", PackageManagerCommand(packageManager), ["--version"], "Install Node.js LTS from https://nodejs.org/; npm is included.", "npm --version"),
        _ => new("pnpm", PackageManagerCommand(packageManager), ["--version"], "Install Node.js LTS from https://nodejs.org/, then run: npm install --global pnpm", "pnpm --version")
    };

    private sealed record ToolCheck(
        string Name,
        string FileName,
        IReadOnlyList<string> Arguments,
        string InstallCommand,
        string VerifyCommand);

    private sealed class SilentConsole : IConsole
    {
        public void WriteLine(string message)
        {
        }

        public void WriteError(string message)
        {
        }
    }
}
