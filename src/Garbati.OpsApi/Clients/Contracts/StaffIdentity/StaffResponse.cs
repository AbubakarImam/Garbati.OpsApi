namespace Garbati.OpsApi.Clients.Contracts.StaffIdentity;

public record StaffResponse(
    string StaffId,
    string FullName,
    string Username,
    string Role,
    string Status,
    bool MustChangePassword,
    string? SupervisorStaffId,
    DateTimeOffset CreatedAt,
    string? CreatedByStaffId,
    DateTimeOffset UpdatedAt);
