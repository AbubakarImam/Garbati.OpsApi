using Garbati.OpsApi.Clients.Contracts.StaffIdentity;

namespace Garbati.OpsApi.Clients.Interfaces;

// This service as a client of StaffIdentityApi (DESIGN.md Section 3: scopes staffidentity.authenticate +
// staffidentity.admin). Mirrors all 8 of StaffIdentityApi's own endpoints (T-032) - every one of them is
// needed, either for login (AuthenticateAsync) or for the staff-management screens (group 5).
public interface IStaffIdentityApiClient
{
    // Null on a 401 (wrong username, wrong password, or Status != Active - StaffIdentityApi reports all
    // three identically, task file: "propagate StaffIdentityApi's generic 401 unchanged").
    Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken cancellationToken);

    Task<CreateStaffResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken);

    Task<StaffResponse?> GetStaffAsync(string staffId, CancellationToken cancellationToken);

    Task<StaffListResponse> ListStaffAsync(
        string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken);

    // Cascade revocation (DESIGN.md Section 2.3, decision #2) is the caller's job, not this client's -
    // see Services/Implementations/StaffManagementService.cs. This method only talks to StaffIdentityApi.
    Task<StaffResponse> ChangeRoleAsync(string staffId, ChangeRoleRequest request, CancellationToken cancellationToken);

    Task<StaffResponse> ChangeStatusAsync(string staffId, ChangeStatusRequest request, CancellationToken cancellationToken);

    Task<StaffResponse> ChangeSupervisorAsync(
        string staffId, ChangeSupervisorRequest request, CancellationToken cancellationToken);

    Task<ChangePasswordResponse> AdminResetPasswordAsync(
        string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken);
}
