namespace CiPnpmCheck.Api.Identity;

public sealed class ApplicationSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string DeviceName { get; set; } = "Unknown device";
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}