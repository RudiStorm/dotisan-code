using Microsoft.AspNetCore.Http;

namespace CiApiCheck4.Api.Auditing;

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