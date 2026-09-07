using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using GeneratedAppintegrationsCheck.Api.Features.Health;
using GeneratedAppintegrationsCheck.Api.Features.Jobs;
using GeneratedAppintegrationsCheck.Api.Integrations;
using GeneratedAppintegrationsCheck.Api.Features.Account;
    using GeneratedAppintegrationsCheck.Api.Features.Authorization;

namespace GeneratedAppintegrationsCheck.Api.Infrastructure;

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