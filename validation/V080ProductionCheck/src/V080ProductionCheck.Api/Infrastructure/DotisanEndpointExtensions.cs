using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using V080ProductionCheck.Api.Features.Health;
using V080ProductionCheck.Api.Features.Jobs;
using V080ProductionCheck.Api.Integrations;
using V080ProductionCheck.Api.Features.Account;
    using V080ProductionCheck.Api.Features.Authorization;

namespace V080ProductionCheck.Api.Infrastructure;

public static class DotisanEndpointExtensions
{
    // Endpoint registration is intentionally explicit and inspectable.
    public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // DOTISAN:ENDPOINTS
        HealthEndpoints.MapHealthEndpoints(endpoints);
        JobEndpoints.MapJobEndpoints(endpoints);
        NotificationEndpoints.Map(endpoints); endpoints.MapHub<NotificationHub>("/hubs/notifications");
        StorageEndpoints.Map(endpoints);
        ImportExportEndpoints.Map(endpoints);
        WebhookEndpoints.Map(endpoints);
        AccountEndpoints.MapAccountEndpoints(endpoints, true);
            AuthorizationEndpoints.MapAuthorizationEndpoints(endpoints);
        return endpoints;
    }
}