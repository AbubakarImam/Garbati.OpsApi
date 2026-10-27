namespace Garbati.OpsApi.Clients.Contracts.Identity;

// Mirrors Garbati.IdentityApi.Contracts.Requests.UpdateCustomerDetailsRequest field-for-field (read
// directly from its source before this was written - garbati-platform T-035/D-19). No idempotency key:
// same reasoning as that side's own comment - a field-level update has no "retry creates a duplicate"
// risk to guard against, since setting the same value twice has the same end state as setting it once.
// That is a plain setter, unlike OrchestratorApi.ReversalRequest, which moves money and must never
// execute twice - see CustomerEditRequestService.ApproveAsync's own comment for why that asymmetry is
// deliberate, not an oversight.
public record UpdateCustomerDetailsRequest(string? FullName, string? Email, string? PhoneNumber);
