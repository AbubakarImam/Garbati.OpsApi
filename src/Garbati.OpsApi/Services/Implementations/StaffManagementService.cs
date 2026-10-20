using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Services.Interfaces;

namespace Garbati.OpsApi.Services.Implementations;

public class StaffManagementService(
    IStaffIdentityApiClient staffIdentityApiClient,
    ISessionService sessionService) : IStaffManagementService
{
    public async Task<CreateStaffResponse> CreateStaffAsync(
        string fullName, string username, string role, string actingStaffId, CancellationToken cancellationToken)
    {
        var idempotencyKey = $"ops:{Guid.NewGuid():N}";
        var request = new CreateStaffRequest(idempotencyKey, actingStaffId, fullName, username, role);
        return await staffIdentityApiClient.CreateStaffAsync(request, cancellationToken);
    }

    public Task<StaffResponse?> GetStaffAsync(string staffId, CancellationToken cancellationToken) =>
        staffIdentityApiClient.GetStaffAsync(staffId, cancellationToken);

    public Task<StaffListResponse> ListStaffAsync(
        string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken) =>
        staffIdentityApiClient.ListStaffAsync(supervisorStaffId, page, pageSize, cancellationToken);

    // DESIGN.md Section 2.3, decision #2: "whenever OpsApi calls PATCH .../role ... on StaffIdentityApi,
    // it deletes every active OpsSession row for that staffId in the same request, before returning
    // success to the calling Administrator." The revoke runs AFTER StaffIdentityApi confirms the change
    // (so a failed/rejected role change never revokes a session for no reason) and BEFORE this method
    // returns (so the Administrator's own response is only ever sent once revocation has actually
    // happened - there's no window where the caller sees success for a staff member whose session is
    // still live).
    public async Task<StaffResponse> ChangeRoleAsync(
        string staffId, string role, string actingStaffId, CancellationToken cancellationToken)
    {
        var response = await staffIdentityApiClient.ChangeRoleAsync(
            staffId, new ChangeRoleRequest(actingStaffId, role), cancellationToken);
        await sessionService.RevokeAllSessionsForStaffAsync(staffId, cancellationToken);
        return response;
    }

    public async Task<StaffResponse> ChangeStatusAsync(
        string staffId, string status, string actingStaffId, CancellationToken cancellationToken)
    {
        var response = await staffIdentityApiClient.ChangeStatusAsync(
            staffId, new ChangeStatusRequest(actingStaffId, status), cancellationToken);
        await sessionService.RevokeAllSessionsForStaffAsync(staffId, cancellationToken);
        return response;
    }

    // DESIGN.md Section 2.3 names only role/status - a supervisor reassignment does not change what a
    // staff member is allowed to do themselves, so it is deliberately NOT a cascade-revocation trigger.
    public Task<StaffResponse> ChangeSupervisorAsync(
        string staffId, string? supervisorStaffId, string actingStaffId, CancellationToken cancellationToken) =>
        staffIdentityApiClient.ChangeSupervisorAsync(
            staffId, new ChangeSupervisorRequest(actingStaffId, supervisorStaffId), cancellationToken);

    // Also deliberately not a revocation trigger - a password reset does not change role/status, and the
    // staff member's own current session (if any) is not what is being reset here anyway (an
    // Administrator resetting someone else's forgotten password does not imply that other person is
    // mid-session right now).
    public Task<ChangePasswordResponse> ResetPasswordAsync(
        string staffId, string actingStaffId, CancellationToken cancellationToken) =>
        staffIdentityApiClient.AdminResetPasswordAsync(
            staffId, new ChangeStaffPasswordRequest(actingStaffId, null, null), cancellationToken);
}
