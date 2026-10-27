namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 4.1. Same shape discipline, and the same deliberate "Id is the primary key directly"
// judgment call, as OpsReversalRequest (see its own comment) - the task file's field table names one id
// field here too, with no separate internal numeric key.
public class OpsCustomerEditRequest
{
    public string Id { get; set; } = null!;

    public string TargetCustomerId { get; set; } = null!;

    // All nullable - a partial update, matching IdentityApi's own UpdateCustomerDetailsRequest (T-035)
    // field-for-field, not a generic untyped JSON blob (task file, explicit). At least one must be
    // non-null at creation - checked by the controller (400 otherwise), not here.
    public string? ProposedFullName { get; set; }
    public string? ProposedEmail { get; set; }
    public string? ProposedPhoneNumber { get; set; }

    public string RequestedByStaffId { get; set; } = null!;
    public string Status { get; set; } = OpsCustomerEditRequestStatus.PendingApproval;

    // Same hard rule as OpsReversalRequest.ApprovedByStaffId - set on both approve and reject.
    public string? ApprovedByStaffId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    // garbati-platform T-047. Mirrors OpsReversalRequest.ExecutionDetail's role - populated only once
    // IdentityApi PATCH /v1/customers/{customerId}/details has actually been called, on Approve, and only
    // when that call failed (Status = ApplicationFailed). Null on Applied (success leaves no detail worth
    // recording) and null on every other status. No equivalent to ExecutedReference is needed here - a
    // detail-update has no reference/transaction id to record on success, unlike a reversal.
    public string? ApplicationError { get; set; }
}
