using Microsoft.AspNetCore.Routing;

namespace Dotisan.AspNetCore;

public sealed record DotisanEndpointDefinition(string Id, Action<IEndpointRouteBuilder> Map);

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
