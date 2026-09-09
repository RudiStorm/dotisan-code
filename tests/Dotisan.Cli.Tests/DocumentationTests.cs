namespace Dotisan.Cli.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Migration_guidance_never_claims_that_new_creates_or_applies_schema()
    {
        var root = FindRepositoryRoot();
        var sources = new[]
        {
            Path.Combine(root, "README.md"),
            Path.Combine(root, "project.md"),
            Path.Combine(root, "docs", "quickstart.md"),
            Path.Combine(root, "docs", "v086-acceptance.md")
        };

        foreach (var path in sources)
        {
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("creates and applies the initial migration", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("creates and applies the initial Identity schema", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dotisan.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
