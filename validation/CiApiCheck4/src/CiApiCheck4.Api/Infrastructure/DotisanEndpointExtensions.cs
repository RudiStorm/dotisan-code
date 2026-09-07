using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using CiApiCheck4.Api.Features.Health;
using CiApiCheck4.Api.Features.Jobs;



namespace CiApiCheck4.Api.Infrastructure;

public static class DotisanEndpointExtensions
{
    // Endpoint registration is intentionally explicit and inspectable.
    public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // DOTISAN:ENDPOINTS
        HealthEndpoints.MapHealthEndpoints(endpoints);
        JobEndpoints.MapJobEndpoints(endpoints);
        
        
        
        
        
        return endpoints;
    }
}