namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record ChangePasswordResponse(string StaffId, string? TemporaryPassword);
