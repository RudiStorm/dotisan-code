using Dotisan.Generators;

namespace Dotisan.Generators.Tests;

public sealed class TemplateCatalogTests
{
    [Fact]
    public void Select_normalizes_paths_and_renderer_replaces_tokens()
    {
        var resource = TemplateCatalog.Select("\\static\\node-version.template");
        var rendered = TemplateRenderer.Render(resource, new TemplateContext(new Dictionary<string, string>
        {
            ["NODE_VERSION"] = "22"
        }));

        Assert.Equal("static/node-version.template", resource.Path);
        Assert.Equal("22\n", rendered);
    }

    [Fact]
    public void Renderer_leaves_unknown_tokens_for_review()
    {
        var resource = new TemplateResource("example", "__KNOWN__ __UNKNOWN__");
        var rendered = TemplateRenderer.Render(resource, new TemplateContext(new Dictionary<string, string>
        {
            ["KNOWN"] = "value"
        }));

        Assert.Equal("value __UNKNOWN__", rendered);
    }
}
