using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Data;
using Garbati.OpsApi.Entities;
using Garbati.OpsApi.Exceptions;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Garbati.OpsApi.Services.Implementations;

// DESIGN.md Section 4.1 (garbati-platform T-034). STOPS AT Approved in this task - deliberately never
// calls IIdentityApiClient at all, even though identity.customer.write is already granted to this
// service's IdentityApi client for exactly this future step (that client's own interface comment already
// says so). Applying the change via IdentityApi PATCH /v1/customers/{customerId}/details
// (garbati-platform T-035) is a small, separate follow-up once that endpoint is confirmed built and
// deployed - not this task's job (see the PR description for whether T-035 had merged by the time this
// was written). This boundary is also enforced by what this class does NOT take as a constructor
// dependency: no IIdentityApiClient here at all.
public class CustomerEditRequestService(OpsDbContext dbContext) : ICustomerEditRequestService
{
    // Same cadence as OpsReversalRequest (DESIGN.md Section 5.1) - the task file leaves this choice open
    // ("unless you have a specific reason to choose differently") and no such reason applies here: a
    // customer-detail-edit review is a staff review, not time-pressured, the same character a reversal
    // review has - not the live-customer urgency a joint-account approval has (IdentityApi's T-018, 1 hour).
    private static readonly TimeSpan PendingApprovalExpiry = TimeSpan.FromHours(24);

    public async Task<OpsCustomerEditRequest> CreateAsync(
        string targetCustomerId, string? proposedFullName, string? proposedEmail, string? proposedPhoneNumber,
        string requestedByStaffId, CancellationToken cancellationToken)
    {
        var entity = new OpsCustomerEditRequest
        {
            Id = OpsRequestIdGenerator.Generate("OPS-EDIT"),
            TargetCustomerId = targetCustomerId,
            ProposedFullName = proposedFullName,
            ProposedEmail = proposedEmail,
            ProposedPhoneNumber = proposedPhoneNumber,
            RequestedByStaffId = requestedByStaffId,
            Status = OpsCustomerEditRequestStatus.PendingApproval,
            RequestedAt = DateTimeOffset.UtcNow,
        };

        dbContext.OpsCustomerEditRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<OpsCustomerEditRequest>> ListAsync(string staffId, string role, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - PendingApprovalExpiry;
        IQueryable<OpsCustomerEditRequest> query = dbContext.OpsCustomerEditRequests;

        // DESIGN.md Section 4.1 / task file: ONLY CustomerServiceOfficer (own requests) and Supervisor
        // (everything) have any access at all - every other role is blocked at the controller level
        // before this is ever called (OpsRoles.CustomerServiceOfficerOrSupervisor), so the catch-all branch
        // below is defensive, never actually reachable. Lazy-expiry-aware listing (task file, "the same
        // way" as reversals): a stale PendingApproval row is excluded from both views, the same read-only
        // query condition shape.
        query = role switch
        {
            OpsRoles.CustomerServiceOfficer => query.Where(r =>
                r.RequestedByStaffId == staffId && (r.Status != OpsCustomerEditRequestStatus.PendingApproval || r.RequestedAt > cutoff)),
            OpsRoles.Supervisor => query.Where(r => r.Status != OpsCustomerEditRequestStatus.PendingApproval || r.RequestedAt > cutoff),
            _ => query.Where(_ => false),
        };

        return await query.OrderByDescending(r => r.RequestedAt).ToListAsync(cancellationToken);
    }

    public async Task<OpsCustomerEditRequest?> GetAsync(string id, string staffId, string role, CancellationToken cancellationToken)
    {
        var request = await dbContext.OpsCustomerEditRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        // Deliberately NOT expiry-filtered - only LIST results exclude a stale pending row; a GET by id
        // still shows the real, possibly-stale status (same shape as ReversalRequestService.IsVisible).
        var visible = role switch
        {
            OpsRoles.CustomerServiceOfficer => request.RequestedByStaffId == staffId,
            OpsRoles.Supervisor => true,
            _ => false,
        };

        return visible ? request : null;
    }

    public async Task<OpsCustomerEditRequest> ApproveAsync(
        string id, string approvingStaffId, string approvingRole, CancellationToken cancellationToken)
    {
        var request = await GetTrackedAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        EnsureDecidable(request, now);
        EnsureMakerChecker(request.RequestedByStaffId, approvingStaffId, approvingRole);

        // Stops here - Approved is this task's deliberate terminal state (see this class's own comment
        // above). No downstream call of any kind.
        request.Status = OpsCustomerEditRequestStatus.Approved;
        request.ApprovedByStaffId = approvingStaffId;
        request.DecidedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<OpsCustomerEditRequest> RejectAsync(
        string id, string rejectingStaffId, string rejectingRole, string rejectionReason, CancellationToken cancellationToken)
    {
        var request = await GetTrackedAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        EnsureDecidable(request, now);
        EnsureMakerChecker(request.RequestedByStaffId, rejectingStaffId, rejectingRole);

        request.Status = OpsCustomerEditRequestStatus.Rejected;
        request.ApprovedByStaffId = rejectingStaffId;
        request.RejectionReason = rejectionReason;
        request.DecidedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    private async Task<OpsCustomerEditRequest> GetTrackedAsync(string id, CancellationToken cancellationToken) =>
        await dbContext.OpsCustomerEditRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new OpsRequestNotFoundException($"Customer-edit request '{id}' was not found.");

    private static void EnsureDecidable(OpsCustomerEditRequest request, DateTimeOffset now)
    {
        if (request.Status != OpsCustomerEditRequestStatus.PendingApproval)
        {
            throw new OpsRequestNotPendingException($"Customer-edit request '{request.Id}' is {request.Status}, not PendingApproval.");
        }

        if (now - request.RequestedAt >= PendingApprovalExpiry)
        {
            throw new OpsRequestNotPendingException(
                $"Customer-edit request '{request.Id}' expired {PendingApprovalExpiry.TotalHours:0} hours after it was requested.");
        }
    }

    // Same hard rule as ReversalRequestService.EnsureMakerChecker - see its own comment.
    private static void EnsureMakerChecker(string requestedByStaffId, string approvingStaffId, string approvingRole)
    {
        if (string.Equals(requestedByStaffId, approvingStaffId, StringComparison.Ordinal))
        {
            throw new MakerCheckerViolationException("The approver must not be the same person as the requester.");
        }

        if (approvingRole != OpsRoles.Supervisor)
        {
            throw new MakerCheckerViolationException("The approving session must currently hold the Supervisor role.");
        }
    }
}
