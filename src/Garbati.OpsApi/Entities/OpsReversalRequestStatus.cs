namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 5.1 / garbati-platform T-034. "Expired" is never written to the database here - a
// PendingApproval row already past its 24-hour window is excluded from "pending" LIST results via a
// read-only query condition instead (Services/Implementations/ReversalRequestService.cs), the same lazy,
// never-a-write fix shape Garbati.IdentityApi's own T-031 used - not flipped to this value by any
// background job. Defined anyway because it IS a real value the design names, and because Approve/Reject
// attempted against a stale row still correctly fails (OpsRequestNotPendingException) even though the
// stored Status column never actually becomes "Expired".
public static class OpsReversalRequestStatus
{
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Executed = "Executed";
    public const string ExecutionFailed = "ExecutionFailed";
    public const string Expired = "Expired";
}
