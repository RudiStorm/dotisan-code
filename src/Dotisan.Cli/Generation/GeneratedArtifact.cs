namespace Dotisan.Cli.Generation;

public sealed record GeneratedArtifact(string RelativePath, string Content);

public interface IGeneratedArtifactPublisher
{
    Task PublishAsync(
        string root,
        IReadOnlyList<GeneratedArtifact> artifacts,
        CancellationToken cancellationToken);
}

public sealed class AtomicArtifactPublisher : IGeneratedArtifactPublisher
{
    public async Task PublishAsync(
        string root,
        IReadOnlyList<GeneratedArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(artifacts);

        var fullRoot = Path.GetFullPath(root);
        var stagingRoot = Path.Combine(fullRoot, ".dotisan", "staging", Guid.NewGuid().ToString("N"));
        var staged = new List<(string Source, string Target)>();

        try
        {
            Directory.CreateDirectory(stagingRoot);

            foreach (var artifact in artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = NormalizeRelativePath(artifact.RelativePath);
                var target = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
                if (!IsUnderRoot(fullRoot, target))
                    throw new ArgumentException($"Generated artifact path escapes the project root: {artifact.RelativePath}.", nameof(artifacts));

                var source = Path.Combine(stagingRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(source)!);
                await File.WriteAllTextAsync(source, artifact.Content, cancellationToken);
                staged.Add((source, target));
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (source, target) in staged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(source, target, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            throw new ArgumentException("Generated artifact paths must be relative.", nameof(path));
        return normalized;
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
