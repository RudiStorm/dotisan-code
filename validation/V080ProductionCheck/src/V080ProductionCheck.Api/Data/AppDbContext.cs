using V080ProductionCheck.Api.Auditing;
using V080ProductionCheck.Api.Integrations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using V080ProductionCheck.Api.Identity;

namespace V080ProductionCheck.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<ApplicationSession> ApplicationSessions => Set<ApplicationSession>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
}