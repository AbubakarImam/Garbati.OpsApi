using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Services.Interfaces;

namespace Garbati.OpsApi.Services.Implementations;

public class JointApprovalConsoleService(IIdentityApiClient identityApiClient) : IJointApprovalConsoleService
{
    private const string PendingStatus = "Pending";

    public Task<PagedResponse<PendingApprovalRequestResponse>> ListPendingAsync(
        int pageSize, string? pageToken, CancellationToken cancellationToken) =>
        identityApiClient.ListPendingAuthorizationRequestsAsync(PendingStatus, pageSize, pageToken, cancellationToken);

    public Task<PendingApprovalRequestResponse?> GetAsync(string requestId, CancellationToken cancellationToken) =>
        identityApiClient.GetAuthorizationRequestAsync(requestId, cancellationToken);

    public Task<PendingApprovalRequestResponse> ApproveAsync(
        string requestId, string approvingCustomerId, CancellationToken cancellationToken) =>
        identityApiClient.ApproveAuthorizationRequestAsync(requestId, new ApproveRequest(approvingCustomerId), cancellationToken);
}
