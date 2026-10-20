using Garbati.OpsApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garbati.OpsApi.Data;

public class OpsDbContext(DbContextOptions<OpsDbContext> options) : DbContext(options)
{
    public DbSet<OpsSession> OpsSessions => Set<OpsSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OpsSession>(entity =>
        {
            entity.HasKey(s => s.SessionId);
            entity.HasIndex(s => s.SessionTokenHash).IsUnique();

            // Cascade revocation (DESIGN.md Section 2.3, decision #2) deletes every session for a
            // StaffId in one request - an index on StaffId makes that a cheap, indexed delete, not a
            // table scan.
            entity.HasIndex(s => s.StaffId);
        });
    }
}
