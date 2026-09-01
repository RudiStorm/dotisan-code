using Dotisan.Core;
using Dotisan.Generators;
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
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(projectRoot);
        var manifestPath = Path.Combine(root, "dotisan.contract.json");
        if (!File.Exists(manifestPath))
        {
            return ContractGenerationResult.Failed(
                $"Could not find '{manifestPath}'. Build the generated API first so its contract manifest can be exported.");
        }

        ContractManifest manifest;
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = ContractManifest.FromJson(json);
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

        var generatedDirectory = Path.Combine(frontendDirectory, "src", "generated");
        foreach (var file in TypeScriptContractGenerator.GenerateAll(manifest))
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
            await GeneratedContractWriter.WriteAsync(frontendDirectory, manifest, cancellationToken);
        }

        return ContractGenerationResult.Succeeded();
    }
}
