using Microsoft.AspNetCore.Http;

namespace GeneratedAppintegrationsCheck.Api.Auditing;

public interface IAuditWriter
{
    Task RecordAsync(
        HttpContext httpContext,
        string entityType,
        string? entityId,
        string action,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken);
}