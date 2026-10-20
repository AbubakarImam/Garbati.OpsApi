using Garbati.OpsApi.Clients.Contracts.Identity;

namespace Garbati.OpsApi.Clients.Interfaces;

// This service as a client of IdentityApi (DESIGN.md Section 3: scopes identity.customer.read,
// identity.customer.write, identity.authorization.check - this task only exercises the read and the
// authorization-check scopes; identity.customer.write is granted for T-034's account/customer-detail-edit
// workflow and deliberately unused here). GetCustomerAsync covers the lookup screen (group 1); the three
// authorization-request methods cover the joint-account approval test console (group 6) - the task file's
// own client-method list named only "customer read" for this client, but Section 6 item 6 and the Section
// 3 scope table both require identity.authorization.check, so this is read as the task file's own
// enumeration being incomplete, not as a reason to leave group 6 unbuilt (see the PR description).
//
// Deliberately absent: anything for identity.ops.reversal (that scope belongs to OrchestratorApi instead,
// DESIGN.md decision #5 - this client has no method that could call it even if someone mistakenly granted
// the scope) and anything for identity.customer.write/.kyc.manage/.mandate.*/.ops.freeze (none of groups
// 1/4/5/6 need them).
public interface IIdentityApiClient
{
    Task<CustomerResponse?> GetCustomerAsync(string customerId, CancellationToken cancellationToken);

    Task<PagedResponse<PendingApprovalRequestResponse>> ListPendingAuthorizationRequestsAsync(
        string status, int pageSize, string? pageToken, CancellationToken cancellationToken);

    Task<PendingApprovalRequestResponse?> GetAuthorizationRequestAsync(string id, CancellationToken cancellationToken);

    Task<PendingApprovalRequestResponse> ApproveAuthorizationRequestAsync(
        string id, ApproveRequest request, CancellationToken cancellationToken);
}
