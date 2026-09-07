using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using V086ReleaseCheck2.Api.Data;
using V086ReleaseCheck2.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace V086ReleaseCheck2.Api.Auditing;

public sealed class AuditWriter(AppDbContext db, IConfiguration configuration, ITenantContext tenantContext) : IAuditWriter
{
    public async Task RecordAsync(
        HttpContext httpContext,
        string entityType,
        string? entityId,
        string action,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Audit:Enabled", true))
        {
            return;
        }

        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? httpContext.TraceIdentifier;
        db.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            ActorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            TenantId = tenantContext.TenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
            TraceId = Activity.Current?.TraceId.ToString(),
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}