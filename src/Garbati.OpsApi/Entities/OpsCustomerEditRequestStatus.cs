namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 4.1 / garbati-platform T-034. The full lifecycle is defined now (matching the design's
// "same shape [as OpsReversalRequest]" framing), but THIS task's own code never reaches Applied or
// ApplicationFailed - Approved is this task's deliberate terminal state. Applying the change via
// IdentityApi PATCH /v1/customers/{customerId}/details (garbati-platform T-035) is a small, separate
// follow-up once that endpoint is confirmed built and deployed (see the PR description for whether T-035
// had merged by the time this was written) - not this task's job. Expired follows the exact same lazy,
// read-only-query-exclusion treatment as OpsReversalRequestStatus.Expired - never written here either.
public static class OpsCustomerEditRequestStatus
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Applied = "Applied";
    public const string ApplicationFailed = "ApplicationFailed";
    public const string Expired = "Expired";
}
