namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 5.1: this workflow's own state belongs to OpsApi, not IdentityApi or OrchestratorApi.
// Unlike AccountOpening/Mandate/Customer elsewhere on this platform, the task file's own field table names
// exactly one id field ("Id | Public id, OPS-REV-XXXXXXXXXX") with no separate internal numeric key at all
// - so, deliberately diverging from the platform's usual "never expose the database primary key" split,
// Id IS the EF primary key here directly. Flagged explicitly in the PR description as a judgment call,
// not a silent departure from convention.
public class OpsReversalRequest
{
    public string Id { get; set; } = null!;

    // Bookkeeping for retry-safety, not named in the task file's own business-field table (which lists
    // only caller-visible fields) - added because the endpoint behaviour it specifies ("a client-supplied,
    // validated IdempotencyKey is required... must be retry-safe from the moment it's created") has nowhere
    // else to be persisted. Scoped per-requester (RequestedByStaffId + IdempotencyKey, unique together) -
    // see ReversalRequestService.CreateAsync.
    public string IdempotencyKey { get; set; } = null!;

    // Caller-supplied at request time - LedgerApi has no lookup-by-reference endpoint to derive these from
    // (confirmed directly against its real source before this was written; TransactionResponse carries no
    // amount at all) - the same "caller supplies it" pattern TransferRequest/AuthorizationRequest already
    // rely on elsewhere on this platform, not a new gap introduced here.
    public string OriginalTransactionReference { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;

    // One of Validation.ReversalReasonCodes.All (DUPL/TECH/FRAD/CUST/UNAUTH) - IdentityApi's own fixed
    // vocabulary, reused as plain strings (no shared assembly between services).
    public string ReasonCode { get; set; } = null!;

    public string RequestedByStaffId { get; set; } = null!;
    public string Status { get; set; } = OpsReversalRequestStatus.PendingApproval;

    // Hard maker-checker rule (DESIGN.md decision #4): must not equal RequestedByStaffId; the deciding
    // session must currently hold Supervisor, re-checked live at decision time
    // (ReversalRequestService.EnsureMakerChecker). Set on BOTH approve and reject - the task file's own
    // field table lists no separate "RejectedBy" field, and the identical hard rule applies to either
    // outcome, so this field tracks "who decided", not literally "who approved".
    public string? ApprovedByStaffId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    // Populated only once OrchestratorApi POST /v1/reversals has actually been called, on Approve
    // (DESIGN.md 5.4 - now closed by this task). ExecutedReference holds OrchestratorApi's own
    // ReversalResultResponse.Reference; ExecutionDetail holds its DecisionId and, on failure, its
    // FailureReason - whatever OrchestratorApi told us, for traceability. Both remain null until a decision
    // actually reaches Executed/ExecutionFailed.
    public string? ExecutedReference { get; set; }
    public string? ExecutionDetail { get; set; }
}
