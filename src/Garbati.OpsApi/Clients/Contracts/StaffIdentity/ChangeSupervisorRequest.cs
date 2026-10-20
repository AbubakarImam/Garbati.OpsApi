namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record ChangeSupervisorRequest(string ActingStaffId, string? SupervisorStaffId);
