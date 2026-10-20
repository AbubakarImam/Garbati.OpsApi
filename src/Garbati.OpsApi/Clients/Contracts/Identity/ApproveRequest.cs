namespace Garbati.OpsApi.Clients.Contracts.Identity;

// Mirrors Garbati.IdentityApi.Contracts.Requests.ApproveRequest. CustomerId is the approving holder's
// public CustomerId - the joint-account test console (DESIGN.md Section 6 item 6) is explicitly a
// stand-in for a real customer channel that doesn't exist yet, so this is supplied by the staff member
// operating the console, not derived from the OpsSession (a Supervisor is not the approving customer).
public record ApproveRequest(string CustomerId);
