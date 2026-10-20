namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

// Mirrors Garbati.StaffIdentityApi.Contracts.Requests.AuthenticateRequest exactly - this service's own
// copy, not a shared reference (ARCHITECTURE.md Section 5: no shared database, no cross-service type
// reuse; every GARBATI client maintains its own copy of a downstream contract).
public record AuthenticateRequest(string Username, string Password);
