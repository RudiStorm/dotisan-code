using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using CiPnpmCheck4.Api.Features.Health;
using CiPnpmCheck4.Api.Features.Jobs;

using CiPnpmCheck4.Api.Features.Account;
    using CiPnpmCheck4.Api.Features.Authorization;

namespace CiPnpmCheck4.Api.Infrastructure;

public static class DotisanEndpointExtensions
{
    // Endpoint registration is intentionally explicit and inspectable.
    public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // DOTISAN:ENDPOINTS
        HealthEndpoints.MapHealthEndpoints(endpoints);
        JobEndpoints.MapJobEndpoints(endpoints);
        
        
        
        
        AccountEndpoints.MapAccountEndpoints(endpoints, true);
            AuthorizationEndpoints.MapAuthorizationEndpoints(endpoints);
        return endpoints;
    }
}