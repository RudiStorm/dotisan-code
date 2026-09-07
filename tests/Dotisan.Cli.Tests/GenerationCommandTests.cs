using Dotisan.Core;
using Dotisan.Cli.Generation;

namespace Dotisan.Cli.Tests;

public sealed class GenerationCommandTests
{
    [Fact]
    public async Task Atomic_publisher_does_not_replace_existing_files_when_staging_fails()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotisan-publisher-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var existing = Path.Combine(root, "src", "models.ts");
            Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
            await File.WriteAllTextAsync(existing, "original\n");

            var publisher = new AtomicArtifactPublisher();
            await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(root,
                [new GeneratedArtifact("src/models.ts", "replacement\n"), new GeneratedArtifact("../escape.txt", "bad")],
                CancellationToken.None));

            Assert.Equal("original\n", await File.ReadAllTextAsync(existing));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "escape.txt")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Generate_writes_models_from_the_project_contract_manifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotisan-generate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var frontend = Path.Combine(root, "src", "App.Web");
            Directory.CreateDirectory(frontend);
            await File.WriteAllTextAsync(Path.Combine(frontend, "package.json"), "{}\n");
            var manifest = new ContractManifest(
                1,
                [],
                [new ContractModel(
                    "Profile",
                    "global::Profile",
                    [new ContractProperty("name", new ContractTypeDescriptor(ContractTypeKind.String), false, false)],
                    [])]);
            await File.WriteAllTextAsync(Path.Combine(root, "dotisan.contract.json"), manifest.ToJson());

            var result = await ContractGenerationService.GenerateAsync(root, check: false, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("export interface Profile {" + Environment.NewLine + "  name: string;" + Environment.NewLine + "}" + Environment.NewLine,
                await File.ReadAllTextAsync(Path.Combine(frontend, "src", "dotisan", "models.ts")));
            Assert.True(File.Exists(Path.Combine(root, "openapi.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Generate_check_reports_stale_generated_output()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotisan-generate-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src", "App.Web", "src", "dotisan"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "App.Web", "package.json"), "{}\n");
            await File.WriteAllTextAsync(Path.Combine(root, "dotisan.contract.json"), new ContractManifest(1, [], []).ToJson());
            await File.WriteAllTextAsync(Path.Combine(root, "src", "App.Web", "src", "dotisan", "models.ts"), "stale\n");

            var result = await ContractGenerationService.GenerateAsync(root, check: true, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("models.ts", result.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
