using Dotisan.Core;

namespace Dotisan.Generators;

public sealed class GoldenTemplateGenerator : IProjectGenerator
{
    public async Task<GenerationResult> GenerateAsync(ProjectOptions options, CancellationToken cancellationToken)
    {
        string outputDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(options.OutputDirectory);
            if (Directory.Exists(outputDirectory) && Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            {
                return GenerationResult.Failed($"Output directory '{outputDirectory}' already exists and is not empty.");
            }

            Directory.CreateDirectory(outputDirectory);
            var identifier = ToIdentifier(options.Name);
            foreach (var file in TemplateFiles.Create(options, identifier))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(outputDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var directory = Path.GetDirectoryName(path);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(path, file.Content, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return GenerationResult.Failed($"Could not create the project: {exception.Message}");
        }

        return GenerationResult.Succeeded(outputDirectory);
    }

    private static string ToIdentifier(string projectName)
    {
        var identifier = new string(projectName.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
        return char.IsLetter(identifier[0]) ? identifier : $"App_{identifier}";
    }
}
