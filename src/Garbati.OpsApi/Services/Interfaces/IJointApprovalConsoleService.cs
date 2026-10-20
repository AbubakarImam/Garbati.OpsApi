using Garbati.OpsApi.Clients.Contracts.Identity;

namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 6 item 6: thin pass-through to IdentityApi's existing single-request lookup/approve
// endpoints and its pending-requests list endpoint (T-031).
public interface IJointApprovalConsoleService
{
    Task<PagedResponse<PendingApprovalRequestResponse>> ListPendingAsync(
        int pageSize, string? pageToken, CancellationToken cancellationToken);

    Task<PendingApprovalRequestResponse?> GetAsync(string requestId, CancellationToken cancellationToken);

    Task<PendingApprovalRequestResponse> ApproveAsync(
        string requestId, string approvingCustomerId, CancellationToken cancellationToken);
}
