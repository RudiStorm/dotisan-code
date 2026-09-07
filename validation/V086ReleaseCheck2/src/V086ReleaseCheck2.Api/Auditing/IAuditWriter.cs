using Microsoft.AspNetCore.Http;

namespace V086ReleaseCheck2.Api.Auditing;

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