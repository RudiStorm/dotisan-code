using Microsoft.AspNetCore.Routing;
using Dotisan.Core;

namespace Dotisan.AspNetCore;

public sealed record DotisanEndpointDefinition(string Id, Action<IEndpointRouteBuilder> Map)
{
    public static DotisanEndpointDefinition For<TEndpoint>(Action<IEndpointRouteBuilder> map)
        where TEndpoint : IDotisanEndpoint
    {
        ArgumentNullException.ThrowIfNull(map);
        return new DotisanEndpointDefinition(TEndpoint.Configure().Id, map);
    }
}

public static class DotisanEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDotisanEndpoints(
        this IEndpointRouteBuilder endpoints,
        params DotisanEndpointDefinition[] definitions)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(definitions);

        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Map(endpoints);
        }

        return endpoints;
    }
}
