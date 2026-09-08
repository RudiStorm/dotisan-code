using System.Reflection;

namespace Dotisan.Generators;

public sealed record TemplateResource(string Path, string Content);

public static class TemplateCatalog
{
    private static readonly Assembly Assembly = typeof(TemplateCatalog).Assembly;

    public static TemplateResource Select(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        var suffix = $"Templates.{normalized.Replace('/', '.')}";
        var resourceName = Assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        using var stream = resourceName is null ? null : Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded template '{normalized}' was not found.");
        using var reader = new StreamReader(stream);
        return new TemplateResource(normalized, reader.ReadToEnd());
    }
}
