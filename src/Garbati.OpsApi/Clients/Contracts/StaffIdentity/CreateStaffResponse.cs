namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record CreateStaffResponse(StaffResponse Staff, string? TemporaryPassword);
