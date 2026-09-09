using Dotisan.AspNetCore;
using Dotisan.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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

    [Fact]
    public void Typed_definition_reads_static_endpoint_metadata()
    {
        var definition = DotisanEndpointDefinition.For<TestEndpoint>(
            static endpoints => endpoints.MapGet("/api/test", () => Results.Ok()));

        Assert.Equal("test.read", definition.Id);
        Assert.NotNull(definition.Map);
    }

    private sealed class TestEndpoint : IDotisanEndpoint
    {
        public static EndpointOptions Configure() => new(
            id: "test.read",
            feature: "Test",
            name: "ReadTest",
            method: "GET",
            route: "/api/test");
    }
}
