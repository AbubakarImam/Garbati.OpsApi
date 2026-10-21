using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Data;
using Garbati.OpsApi.Entities;
using Garbati.OpsApi.Exceptions;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrchestratorContracts = Garbati.OpsApi.Clients.Contracts.Orchestrator;

namespace Garbati.OpsApi.Services.Implementations;

// DESIGN.md Section 5 (garbati-platform T-034) - the one workflow built fully executable end to end.
public class ReversalRequestService(
    OpsDbContext dbContext, IOrchestratorApiClient orchestratorApiClient, ILogger<ReversalRequestService> logger)
    : IReversalRequestService
{
    // DESIGN.md Section 5.1: "Expired after 24 hours in PendingApproval" - deliberately longer than
    // IdentityApi's own 1-hour joint-approval window (T-018): a live customer transfer approval is
    // time-pressured, a staff review of a reversal is not.
    private static readonly TimeSpan PendingApprovalExpiry = TimeSpan.FromHours(24);

    private const int MaxIdGenerationAttempts = 5;

    // Matches the unique index EF generates for (RequestedByStaffId, IdempotencyKey) - see
    // OpsDbContext.OnModelCreating and the migration this produces.
    private const string IdempotencyIndexName = "ix_ops_reversal_requests_requested_by_staff_id_idempotency_key";

    public async Task<OpsReversalRequest> CreateAsync(
        string idempotencyKey, string originalTransactionReference, decimal amount, string currency,
        string reasonCode, string requestedByStaffId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxIdGenerationAttempts; attempt++)
        {
            var entity = new OpsReversalRequest
            {
                Id = OpsRequestIdGenerator.Generate("OPS-REV"),
                IdempotencyKey = idempotencyKey,
                OriginalTransactionReference = originalTransactionReference,
                Amount = amount,
                Currency = currency,
                ReasonCode = reasonCode,
                RequestedByStaffId = requestedByStaffId,
                Status = OpsReversalRequestStatus.PendingApproval,
                RequestedAt = DateTimeOffset.UtcNow,
            };

            dbContext.OpsReversalRequests.Add(entity);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return entity;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pgEx)
            {
                dbContext.Entry(entity).State = EntityState.Detached;

                if (pgEx.ConstraintName == IdempotencyIndexName)
                {
                    // Retry-safe replay (task file, explicit: "it must be retry-safe from the moment it's
                    // created") - a browser retry under the same key returns the request that key already
                    // created, rather than creating a second row for the same reversal.
                    return await dbContext.OpsReversalRequests.AsNoTracking().FirstOrDefaultAsync(
                        r => r.RequestedByStaffId == requestedByStaffId && r.IdempotencyKey == idempotencyKey, cancellationToken)
                        ?? throw new InvalidOperationException("Error retrieving the result of the existing idempotency key.");
                }
                // Otherwise the collision was on Id (astronomically unlikely at 32^10) - loop and generate another.
            }
        }

        throw new InvalidOperationException("Could not generate a unique reversal request id after several attempts.");
    }

    public async Task<IReadOnlyList<OpsReversalRequest>> ListAsync(string staffId, string role, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - PendingApprovalExpiry;
        var query = BuildVisibilityQuery(dbContext.OpsReversalRequests, staffId, role, cutoff);
        return await query.OrderByDescending(r => r.RequestedAt).ToListAsync(cancellationToken);
    }

    public async Task<OpsReversalRequest?> GetAsync(string id, string staffId, string role, CancellationToken cancellationToken)
    {
        var request = await dbContext.OpsReversalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return request is not null && IsVisible(request, staffId, role) ? request : null;
    }

    public async Task<OpsReversalRequest> ApproveAsync(
        string id, string approvingStaffId, string approvingRole, CancellationToken cancellationToken)
    {
        var request = await GetTrackedAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        EnsureDecidable(request, now);
        EnsureMakerChecker(request.RequestedByStaffId, approvingStaffId, approvingRole);

        request.Status = OpsReversalRequestStatus.Approved;
        request.ApprovedByStaffId = approvingStaffId;
        request.DecidedAt = now;

        // DESIGN.md 5.3/5.4 (now closed by this task). RequestedBy is the ORIGINAL requester's staff id
        // (task file, explicit) - never the approver's: the approver is this workflow's own internal
        // checker, RequestedBy is "who is actually asking for this reversal", consistent with how
        // OrchestratorApi's own ReversalRequest.RequestedBy is used elsewhere on this platform. The
        // idempotency key is DETERMINISTIC, derived from this OpsReversalRequest's own Id - not a fresh
        // one per call - so a browser retry of this very approve action cannot create two separate
        // execution attempts at OrchestratorApi (task file, explicit; mirrors OrchestratorApi's own pattern
        // of deriving deterministic downstream keys from its own operation id).
        OrchestratorContracts.ReversalResultResponse? result = null;
        string? executionError = null;
        try
        {
            result = await orchestratorApiClient.ReverseAsync(
                new OrchestratorContracts.ReversalRequest(
                    $"ops:{request.Id}", request.RequestedByStaffId, request.OriginalTransactionReference,
                    request.Amount, request.Currency, request.ReasonCode),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Any other error from the call - not just a 422 business-rule failure - is still
            // ExecutionFailed (task file, explicit): a 404/409/500/timeout from OrchestratorApi is recorded
            // as a failed execution, never left to bubble as an opaque 500 that masks what actually
            // happened.
            executionError = ex is DownstreamApiException downstream
                ? $"OrchestratorApi returned {downstream.StatusCode}: {downstream.Message}"
                : $"{ex.GetType().Name}: {ex.Message}";
        }

        if (result is not null && result.Status != "Failed")
        {
            request.Status = OpsReversalRequestStatus.Executed;
            request.ExecutedReference = result.Reference;
            request.ExecutionDetail = $"DecisionId {result.DecisionId}";
        }
        else
        {
            request.Status = OpsReversalRequestStatus.ExecutionFailed;
            request.ExecutedReference = result?.Reference;
            request.ExecutionDetail = result is not null
                ? $"DecisionId {result.DecisionId}; {result.FailureReason}"
                : executionError;
        }

        // Mirrors Garbati.OrchestratorApi.Services.Implementations.ReversalService's own principle: by
        // this point the execution outcome is already real and known (OrchestratorApi either executed it,
        // definitively could not, or something else went wrong) - a failure to PERSIST that outcome must
        // not mask it from our own caller. Logged, not rethrown; the in-memory request (returned below)
        // still reflects the true outcome even if this particular save did not make it to the database.
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveEx)
        {
            logger.LogError(saveEx,
                "Reversal request {Id} reached {Status} but the result could not be saved.", request.Id, request.Status);
        }

        return request;
    }

    public async Task<OpsReversalRequest> RejectAsync(
        string id, string rejectingStaffId, string rejectingRole, string rejectionReason, CancellationToken cancellationToken)
    {
        var request = await GetTrackedAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        EnsureDecidable(request, now);
        EnsureMakerChecker(request.RequestedByStaffId, rejectingStaffId, rejectingRole);

        request.Status = OpsReversalRequestStatus.Rejected;
        request.ApprovedByStaffId = rejectingStaffId;
        request.RejectionReason = rejectionReason;
        request.DecidedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return request;
    }

    private async Task<OpsReversalRequest> GetTrackedAsync(string id, CancellationToken cancellationToken) =>
        await dbContext.OpsReversalRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new OpsRequestNotFoundException($"Reversal request '{id}' was not found.");

    private static void EnsureDecidable(OpsReversalRequest request, DateTimeOffset now)
    {
        if (request.Status != OpsReversalRequestStatus.PendingApproval)
        {
            throw new OpsRequestNotPendingException($"Reversal request '{request.Id}' is {request.Status}, not PendingApproval.");
        }

        if (now - request.RequestedAt >= PendingApprovalExpiry)
        {
            throw new OpsRequestNotPendingException(
                $"Reversal request '{request.Id}' expired {PendingApprovalExpiry.TotalHours:0} hours after it was requested.");
        }
    }

    // Hard rule (task file / DESIGN.md decision #4). approvingRole/approvingStaffId are read fresh from
    // the CURRENT request's own claims by the controller (SessionAuthenticationHandler re-resolves them
    // from the database on every single request, never reused from an earlier one, and never anything a
    // caller could supply directly in a request body) - so there is nothing here for a stale, browser- or
    // queue-display-cached value to poison. [Authorize(Roles = OpsRoles.Supervisor)] on the controller
    // action already guarantees the role half of this for every real request; checking it again here too
    // is the "enforced in code, not just the attribute" belt-and-suspenders the task file asks for, and
    // makes the rule directly unit-testable without the full HTTP pipeline.
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

    // DESIGN.md Section 5 / Section 4: visibility for a SINGLE row by id. Deliberately NOT expiry-filtered
    // - only LIST results exclude a stale pending row (BuildVisibilityQuery below); a GET by id still
    // shows the real, possibly-stale status, the same "GET by id still works" shape Garbati.IdentityApi's
    // own T-031 fix used.
    private static bool IsVisible(OpsReversalRequest request, string staffId, string role) => role switch
    {
        OpsRoles.Teller or OpsRoles.CustomerServiceOfficer => request.RequestedByStaffId == staffId,
        OpsRoles.Supervisor => request.Status == OpsReversalRequestStatus.PendingApproval || request.ApprovedByStaffId == staffId,
        OpsRoles.ComplianceOfficer or OpsRoles.RiskOfficer or OpsRoles.Auditor => true,
        _ => false,
    };

    // DESIGN.md Section 5 / task file's lazy-expiry-aware listing: a PendingApproval row already past its
    // 24-hour window must not show up as actionable in LIST results (mirrors Garbati.IdentityApi's own
    // T-031 fix - a plain read-only query condition, never a write to Status). ComplianceOfficer/
    // RiskOfficer/Auditor are deliberately exempt from the expiry filter - their role is full read-only
    // oversight over "every request, any status" (task file, explicit), which includes a request that
    // silently expired without ever being decided; hiding that from them would work against the very
    // oversight purpose that role exists for.
    private static IQueryable<OpsReversalRequest> BuildVisibilityQuery(
        IQueryable<OpsReversalRequest> query, string staffId, string role, DateTimeOffset cutoff) => role switch
    {
        OpsRoles.Teller or OpsRoles.CustomerServiceOfficer => query.Where(r =>
            r.RequestedByStaffId == staffId && (r.Status != OpsReversalRequestStatus.PendingApproval || r.RequestedAt > cutoff)),
        OpsRoles.Supervisor => query.Where(r =>
            (r.Status == OpsReversalRequestStatus.PendingApproval && r.RequestedAt > cutoff) || r.ApprovedByStaffId == staffId),
        OpsRoles.ComplianceOfficer or OpsRoles.RiskOfficer or OpsRoles.Auditor => query,
        _ => query.Where(_ => false),
    };
}
