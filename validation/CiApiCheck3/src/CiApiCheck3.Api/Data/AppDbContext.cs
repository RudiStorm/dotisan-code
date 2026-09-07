using CiApiCheck3.Api.Auditing;

using Microsoft.EntityFrameworkCore;

namespace CiApiCheck3.Api.Data;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    
    
}