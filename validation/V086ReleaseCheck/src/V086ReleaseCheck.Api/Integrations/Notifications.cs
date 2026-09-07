using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.AspNetCore.Routing;
using V086ReleaseCheck.Api.Data;
using V086ReleaseCheck.Api.Tenancy;

namespace V086ReleaseCheck.Api.Integrations;

public sealed class NotificationRecord
{
    public Guid Id { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder) { builder.HasKey(item => item.Id); builder.HasIndex(item => new { item.TenantId, item.RecipientId, item.IsRead }); builder.Property(item => item.Title).HasMaxLength(200); builder.Property(item => item.Body).HasMaxLength(4000); }
}
public interface INotificationStore
{
    Task<IReadOnlyList<NotificationRecord>> ListAsync(string recipientId, string? tenantId, CancellationToken cancellationToken);
    Task<NotificationRecord> AddAsync(string recipientId, string? tenantId, string title, string body, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid id, string recipientId, string? tenantId, CancellationToken cancellationToken);
}
public sealed class EfNotificationStore(AppDbContext db) : INotificationStore
{
    public async Task<IReadOnlyList<NotificationRecord>> ListAsync(string recipientId, string? tenantId, CancellationToken cancellationToken) => await db.Notifications.AsNoTracking().Where(item => item.RecipientId == recipientId && item.TenantId == tenantId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
    public async Task<NotificationRecord> AddAsync(string recipientId, string? tenantId, string title, string body, CancellationToken cancellationToken) { var item = new NotificationRecord { Id = Guid.NewGuid(), RecipientId = recipientId, TenantId = tenantId, Title = title, Body = body, CreatedAtUtc = DateTimeOffset.UtcNow }; db.Notifications.Add(item); await db.SaveChangesAsync(cancellationToken); return item; }
    public async Task<bool> MarkReadAsync(Guid id, string recipientId, string? tenantId, CancellationToken cancellationToken) { var item = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == recipientId && x.TenantId == tenantId, cancellationToken); if (item is null) return false; item.IsRead = true; await db.SaveChangesAsync(cancellationToken); return true; }
}
public sealed class NotificationHub : Hub { }
public static class NotificationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications"); group.RequireAuthorization();
        group.MapGet("", async (HttpContext httpContext, INotificationStore store, ITenantContext tenantContext, CancellationToken cancellationToken) => Results.Ok(new { notifications = await store.ListAsync(httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", tenantContext.TenantId, cancellationToken) }));
        group.MapPost("", async (NotificationRequest request, HttpContext httpContext, INotificationStore store, IHubContext<NotificationHub> hub, ITenantContext tenantContext, CancellationToken cancellationToken) => { if (string.IsNullOrWhiteSpace(request.RecipientId) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["notification"] = ["Recipient, title, and body are required."] }); var item = await store.AddAsync(request.RecipientId.Trim(), tenantContext.TenantId, request.Title.Trim(), request.Body.Trim(), cancellationToken); await hub.Clients.User(item.RecipientId).SendAsync("notification", item, cancellationToken); return Results.Created($"/api/notifications/{item.Id}", item); });
        group.MapPost("/{id:guid}/read", async (Guid id, HttpContext httpContext, INotificationStore store, ITenantContext tenantContext, CancellationToken cancellationToken) => await store.MarkReadAsync(id, httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous", tenantContext.TenantId, cancellationToken) ? Results.NoContent() : Results.NotFound());
    }
    public sealed record NotificationRequest(string RecipientId, string Title, string Body);
}