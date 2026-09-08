namespace Dotisan.Generators;

public sealed record TemplateContext(IReadOnlyDictionary<string, string> Tokens)
{
    public static TemplateContext Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));
}

public static class TemplateRenderer
{
    public static string Render(TemplateResource resource, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(context);

        var content = resource.Content;
        foreach (var token in context.Tokens)
            content = content.Replace($"__{token.Key}__", token.Value, StringComparison.Ordinal);
        return content;
    }
}
