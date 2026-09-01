using System.Security.Cryptography;
using System.Text;

namespace Dotisan.TypeScript;

public sealed record GeneratedFileManifest(IReadOnlyList<GeneratedTypeScriptFile> Files, string Sha256)
{
    public static GeneratedFileManifest Create(IReadOnlyList<GeneratedTypeScriptFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var ordered = files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        var content = string.Join("\n", ordered.Select(file => file.Path + "\n" + file.Content));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new GeneratedFileManifest(ordered, hash);
    }
}
