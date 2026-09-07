using CiApiCheck2.Api.Auditing;

using Microsoft.EntityFrameworkCore;

namespace CiApiCheck2.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    
    
}