using CiApiCheck.Api.Auditing;

using Microsoft.EntityFrameworkCore;

namespace CiApiCheck.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    
    
}