namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 4.1 / garbati-platform T-034/T-047. The full lifecycle was defined in T-034 (matching
// the design's "same shape [as OpsReversalRequest]" framing), but T-034's own code never reached Applied
// or ApplicationFailed - Approved was that task's deliberate terminal state, since IdentityApi PATCH
// /v1/customers/{customerId}/details didn't exist yet. T-047 closes that gap:
// CustomerEditRequestService.ApproveAsync now actually calls it and reaches Applied/ApplicationFailed, the
// same way ReversalRequestService already reaches Executed/ExecutionFailed. Expired still follows the
// exact same lazy, read-only-query-exclusion treatment as OpsReversalRequestStatus.Expired - never written
// here either.
public static class OpsCustomerEditRequestStatus
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Applied = "Applied";
    public const string ApplicationFailed = "ApplicationFailed";
    public const string Expired = "Expired";
}
