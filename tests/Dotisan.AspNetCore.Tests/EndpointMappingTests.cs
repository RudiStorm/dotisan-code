using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Dotisan.AspNetCore;

namespace Dotisan.AspNetCore.Tests;

public sealed class EndpointMappingTests
{
    [Fact]
    public void Endpoint_definition_keeps_mapping_explicit()
    {
        var definition = new DotisanEndpointDefinition(
            "health",
            static endpoints => endpoints.MapGet("/health", () => Results.Ok()));

        Assert.Equal("health", definition.Id);
        Assert.NotNull(definition.Map);
    }
}
