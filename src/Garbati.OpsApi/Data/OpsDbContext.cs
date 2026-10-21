using Garbati.OpsApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace Garbati.OpsApi.Data;

public class OpsDbContext(DbContextOptions<OpsDbContext> options) : DbContext(options)
{
    public DbSet<OpsSession> OpsSessions => Set<OpsSession>();
    public DbSet<OpsReversalRequest> OpsReversalRequests => Set<OpsReversalRequest>();
    public DbSet<OpsCustomerEditRequest> OpsCustomerEditRequests => Set<OpsCustomerEditRequest>();

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

        // DESIGN.md Section 5.1 (garbati-platform T-034). Id is the primary key directly (see
        // OpsReversalRequest's own comment on this judgment call). The unique index on
        // (RequestedByStaffId, IdempotencyKey) is what makes CreateAsync retry-safe
        // (ReversalRequestService); its generated name is referenced directly by that class's own
        // conflict-handling code, so renaming either column requires updating both places together.
        modelBuilder.Entity<OpsReversalRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.RequestedByStaffId, r.IdempotencyKey }).IsUnique();
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.ApprovedByStaffId);
        });

        // DESIGN.md Section 4.1 (garbati-platform T-034). No idempotency key here - unlike a reversal,
        // staging a customer-detail edit does not move money, so a duplicate submission is merely
        // redundant, not unsafe (CustomerEditRequestService's own comment).
        modelBuilder.Entity<OpsCustomerEditRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.RequestedByStaffId);
            entity.HasIndex(r => r.Status);
        });
    }
}
