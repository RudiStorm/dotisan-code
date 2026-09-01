using Dotisan.Core;
using Dotisan.TypeScript;

namespace Dotisan.Generators;

public static class GeneratedContractWriter
{
    public static async Task WriteAsync(
        string frontendDirectory,
        ContractManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendDirectory);
        ArgumentNullException.ThrowIfNull(manifest);

        var generatedDirectory = Path.Combine(frontendDirectory, "src", "dotisan");
        Directory.CreateDirectory(generatedDirectory);

        foreach (var file in TypeScriptContractGenerator.GenerateAll(manifest))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(generatedDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, file.Content, cancellationToken);
        }

        var placeholder = Path.Combine(generatedDirectory, ".gitkeep");
        if (File.Exists(placeholder))
        {
            File.Delete(placeholder);
        }
    }
}
