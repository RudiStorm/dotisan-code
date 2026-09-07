namespace V086ReleaseCheck2.Api.Auditing;

public sealed class AuditEntry
{
    public Guid Id { get; set; }
    public string? ActorId { get; set; }
    public string? TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Changes { get; set; } = "{}";
    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}