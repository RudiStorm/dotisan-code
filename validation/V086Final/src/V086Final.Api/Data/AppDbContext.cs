using V086Final.Api.Auditing;
using V086Final.Api.Integrations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using V086Final.Api.Identity;

namespace V086Final.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<ApplicationSession> ApplicationSessions => Set<ApplicationSession>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
}