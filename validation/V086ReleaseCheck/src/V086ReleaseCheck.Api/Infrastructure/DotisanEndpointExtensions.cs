using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using V086ReleaseCheck.Api.Features.Health;
using V086ReleaseCheck.Api.Features.Jobs;
using V086ReleaseCheck.Api.Integrations;
using V086ReleaseCheck.Api.Features.Account;
    using V086ReleaseCheck.Api.Features.Authorization;

namespace V086ReleaseCheck.Api.Infrastructure;

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