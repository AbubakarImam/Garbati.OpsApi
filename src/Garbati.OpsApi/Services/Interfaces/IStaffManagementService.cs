using Garbati.OpsApi.Clients.Contracts.StaffIdentity;

namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 6 item 5: thin pass-through to every one of StaffIdentityApi's 8 endpoints, EXCEPT
// that ChangeRoleAsync/ChangeStatusAsync additionally run cascade revocation (Section 2.3, decision #2) -
// this is the one place that logic actually lives, so it can never be skipped by a controller forgetting
// to call it separately.
public interface IStaffManagementService
{
    Task<CreateStaffResponse> CreateStaffAsync(string fullName, string username, string role, string actingStaffId, CancellationToken cancellationToken);

    Task<StaffResponse?> GetStaffAsync(string staffId, CancellationToken cancellationToken);

    Task<StaffListResponse> ListStaffAsync(string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken);

    // DESIGN.md Section 2.3: deletes every OpsSession for staffId, in the same request, before returning.
    Task<StaffResponse> ChangeRoleAsync(string staffId, string role, string actingStaffId, CancellationToken cancellationToken);

    Task<StaffResponse> ChangeStatusAsync(string staffId, string status, string actingStaffId, CancellationToken cancellationToken);

    Task<StaffResponse> ChangeSupervisorAsync(string staffId, string? supervisorStaffId, string actingStaffId, CancellationToken cancellationToken);

    Task<ChangePasswordResponse> ResetPasswordAsync(string staffId, string actingStaffId, CancellationToken cancellationToken);
}
