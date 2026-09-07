using System.Text.Json;
using Dotisan.Core;
using Dotisan.Generators;
using Dotisan.OpenApi;
using Dotisan.TypeScript;

namespace Dotisan.Cli.Generation;

public sealed record ContractGenerationResult(bool Success, string? ErrorMessage = null)
{
    public static ContractGenerationResult Succeeded() => new(true);

    public static ContractGenerationResult Failed(string message) => new(false, message);
}

public static class ContractGenerationService
{
    public static async Task<ContractGenerationResult> GenerateAsync(
        string projectRoot,
        bool check,
        CancellationToken cancellationToken,
        bool noOpenApi = false,
        IDotisanServices? services = null,
        IConsole? console = null)
    {
        var root = Path.GetFullPath(projectRoot);
        var manifestPath = Path.Combine(root, "dotisan.contract.json");
        var exportPath = Path.Combine(root, ".dotisan", "contract.manifest.json");

        ContractManifest manifest;
        try
        {
            string json;
            if (services is not null)
            {
                if (services.ApiProjectPath is null)
                    return ContractGenerationResult.Failed("Could not find an API project. Run this command from a generated Dotisan project.");

                var openApiPath = await FindBuildOpenApiDocumentAsync(services.ApiProjectPath, cancellationToken);
                if (openApiPath is not null)
                {
                    json = await File.ReadAllTextAsync(openApiPath, cancellationToken);
                    manifest = OpenApiContractReader.Read(json);
                    var derivedJson = manifest.ToJson();
                    if (check && (!File.Exists(manifestPath) || !string.Equals(await File.ReadAllTextAsync(manifestPath, cancellationToken), derivedJson, StringComparison.Ordinal)))
                        return ContractGenerationResult.Failed($"The derived contract manifest is stale: {manifestPath}. Run 'dotisan generate'.");
                    goto ContractLoaded;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
                var export = await services.RunAsync(
                    "dotnet",
                    ["run", "--project", services.ApiProjectPath, "--no-build", "--", "--dotisan-export-contract", exportPath],
                    root,
                    console ?? new NullConsole(),
                    cancellationToken);
                if (!export.Success)
                    return ContractGenerationResult.Failed(export.ErrorMessage ?? "The generated API could not export its contract manifest.");

                json = await File.ReadAllTextAsync(exportPath, cancellationToken);
                manifest = ContractManifest.FromJson(json);
                if (check && (!File.Exists(manifestPath) || !string.Equals(await File.ReadAllTextAsync(manifestPath, cancellationToken), manifest.ToJson(), StringComparison.Ordinal)))
                    return ContractGenerationResult.Failed($"The compiled contract manifest is stale: {manifestPath}. Run 'dotisan generate'.");
                goto ContractLoaded;
            }
            else
            {
                if (!File.Exists(manifestPath))
                    return ContractGenerationResult.Failed($"Could not find '{manifestPath}'. Build the generated API first so its contract manifest can be exported.");
                json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            }

            manifest = ContractManifest.FromJson(json);
        ContractLoaded:
            ;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException or NotSupportedException)
        {
            return ContractGenerationResult.Failed($"The contract manifest is invalid: {exception.Message}");
        }
        catch (IOException exception)
        {
            return ContractGenerationResult.Failed($"Could not read the contract manifest: {exception.Message}");
        }

        var frontendDirectory = Directory.Exists(Path.Combine(root, "src"))
            ? Directory.EnumerateDirectories(Path.Combine(root, "src"), "*.Web", SearchOption.AllDirectories)
                .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "package.json")))
            : null;
        if (frontendDirectory is null)
        {
            return ContractGenerationResult.Failed("Could not find the generated Vue frontend. Run this command from a generated Dotisan project.");
        }

        var generatedDirectory = Path.Combine(frontendDirectory, "src", "dotisan");
        var generatedFiles = TypeScriptContractGenerator.GenerateAll(manifest).ToArray();
        foreach (var file in generatedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(generatedDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (check)
            {
                if (!File.Exists(path) || !string.Equals(await File.ReadAllTextAsync(path, cancellationToken), file.Content, StringComparison.Ordinal))
                {
                    return ContractGenerationResult.Failed($"Generated output is stale: {path}. Run 'dotisan generate'.");
                }
            }
        }

        if (!check)
        {
            var artifacts = generatedFiles
                .Select(file => new GeneratedArtifact(
                    Path.GetRelativePath(root, Path.Combine(generatedDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar))),
                    file.Content))
                .ToList();
            if (services is not null)
                artifacts.Add(new GeneratedArtifact("dotisan.contract.json", manifest.ToJson()));
            if (!noOpenApi)
            {
                var title = Path.GetFileName(root);
                artifacts.Add(new GeneratedArtifact("openapi.json", OpenApiDocumentGenerator.Generate(manifest, title, "v1").Json));
            }

            await new AtomicArtifactPublisher().PublishAsync(root, artifacts, cancellationToken);
        }
        else if (!noOpenApi)
        {
            var expectedOpenApi = OpenApiDocumentGenerator.Generate(manifest, Path.GetFileName(root), "v1").Json;
            var openApiPath = Path.Combine(root, "openapi.json");
            if (!File.Exists(openApiPath) || !string.Equals(await File.ReadAllTextAsync(openApiPath, cancellationToken), expectedOpenApi, StringComparison.Ordinal))
            {
                return ContractGenerationResult.Failed($"Generated output is stale: {openApiPath}. Run 'dotisan generate'.");
            }
        }

        TryDeleteExport(exportPath);
        return ContractGenerationResult.Succeeded();
    }

    private static async Task<string?> FindBuildOpenApiDocumentAsync(string apiProjectPath, CancellationToken cancellationToken)
    {
        var projectDirectory = Path.GetDirectoryName(apiProjectPath);
        var obj = projectDirectory is null ? null : Path.Combine(projectDirectory, "obj");
        if (obj is null || !Directory.Exists(obj))
            return null;
        foreach (var path in Directory.EnumerateFiles(obj, "*.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\ref\\", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(path);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("openapi", out _)
                    && document.RootElement.TryGetProperty("paths", out _))
                    return path;
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static void TryDeleteExport(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed class NullConsole : IConsole
    {
        public void WriteLine(string message) { }
        public void WriteError(string message) { }
    }
}
