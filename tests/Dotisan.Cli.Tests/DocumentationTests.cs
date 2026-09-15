namespace Dotisan.Cli.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Migration_guidance_distinguishes_development_from_production_schema_setup()
    {
        var root = FindRepositoryRoot();
        var sources = new[]
        {
            Path.Combine(root, "README.md"),
            Path.Combine(root, "project.md"),
            Path.Combine(root, "docs", "quickstart.md")
        };

        foreach (var path in sources)
        {
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("dotisan new does not create or apply", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Storage_readiness_is_explicitly_demoted_until_metadata_and_ownership_exist()
    {
        var root = FindRepositoryRoot();
        var matrix = File.ReadAllText(Path.Combine(root, "docs", "integrations-readiness.md"));
        var overview = File.ReadAllText(Path.Combine(root, "docs", "integration-readiness.md"));
        Assert.Contains("Local storage", matrix, StringComparison.Ordinal);
        Assert.Contains("example-only", matrix, StringComparison.Ordinal);
        Assert.Contains("Local and S3-compatible storage are example-only", overview, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dotisan.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
