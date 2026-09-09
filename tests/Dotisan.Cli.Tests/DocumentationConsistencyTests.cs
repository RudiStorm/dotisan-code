namespace Dotisan.Cli.Tests;

public sealed class DocumentationConsistencyTests
{
    [Fact]
    public void Migration_guidance_does_not_claim_automatic_creation_or_application()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            "README.md",
            "project.md",
            Path.Combine("docs", "quickstart.md"),
            Path.Combine("docs", "jobs-and-scheduling.md"),
            Path.Combine("src", "Dotisan.Generators", "TemplateFiles.cs")
        };
        var retiredClaims = new[] { "creates and applies the initial migration", "creates and applies the initial Identity schema", "creates and applies the initial Identity migration automatically" };

        foreach (var relativePath in files)
        {
            var content = File.ReadAllText(Path.Combine(root, relativePath));
            foreach (var claim in retiredClaims)
                Assert.DoesNotContain(claim, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Shell_completions_include_canonical_nested_commands()
    {
        var root = FindRepositoryRoot();
        foreach (var shell in new[] { "dotisan.bash", "dotisan.ps1", "dotisan.zsh" })
        {
            var content = File.ReadAllText(Path.Combine(root, "docs", "completions", shell));
            Assert.Contains("make", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("add", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remove", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dotisan.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be located.");
    }
}
