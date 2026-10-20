namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 6 item 5. ActingStaffId/IdempotencyKey are NOT accepted from the browser - OpsApi
// fills both in itself from the authenticated Administrator's own session and a server-generated key
// (Clients/Contracts/StaffIdentity/CreateStaffRequest.cs).
public record CreateStaffRequest(string FullName, string Username, string Role);
