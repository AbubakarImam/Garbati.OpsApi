namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record AuthenticateResponse(string StaffId, string FullName, string Role);
