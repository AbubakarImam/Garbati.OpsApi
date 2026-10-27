using System.Net;
using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Exceptions;

namespace Garbati.OpsApi.Clients.Implementations;

// Talks to IdentityApi over HTTP. Routes confirmed against Garbati.IdentityApi.Controllers.
// CustomersController/AuthorizationController's live [Route] attributes.
//
// GetAuthorizationRequestAsync/ListPendingAuthorizationRequestsAsync are scoped to the joint-account
// approval test console (DESIGN.md Section 6 item 6) specifically - the ids this console's queue
// produces are always PendingApprovalRequest ids (the T-018 joint-approval row this shape was designed
// for), never a single-holder AuthorizationDecision id, which IdentityApi's own GET .../{id} endpoint
// would return in a structurally different shape. This client does not attempt to handle that second
// shape - see the PR description's judgment calls.
public class IdentityApiClient(HttpClient httpClient) : IIdentityApiClient
{
    public async Task<CustomerResponse?> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"v1/customers/{customerId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<CustomerResponse>(response, $"IdentityApi GET /v1/customers/{customerId}", cancellationToken);
    }

    public async Task<PagedResponse<PendingApprovalRequestResponse>> ListPendingAuthorizationRequestsAsync(
        string status, int pageSize, string? pageToken, CancellationToken cancellationToken)
    {
        var query = $"v1/authorization/requests?status={Uri.EscapeDataString(status)}&pageSize={pageSize}"
            + (pageToken is null ? "" : $"&pageToken={Uri.EscapeDataString(pageToken)}");
        using var response = await httpClient.GetAsync(query, cancellationToken);
        return await ReadOrThrowAsync<PagedResponse<PendingApprovalRequestResponse>>(
            response, "IdentityApi GET /v1/authorization/requests", cancellationToken);
    }

    public async Task<PendingApprovalRequestResponse?> GetAuthorizationRequestAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"v1/authorization/requests/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<PendingApprovalRequestResponse>(
            response, $"IdentityApi GET /v1/authorization/requests/{id}", cancellationToken);
    }

    public async Task<PendingApprovalRequestResponse> ApproveAuthorizationRequestAsync(
        string id, ApproveRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync($"v1/authorization/requests/{id}/approve", request, cancellationToken);
        return await ReadOrThrowAsync<PendingApprovalRequestResponse>(
            response, $"IdentityApi POST /v1/authorization/requests/{id}/approve", cancellationToken);
    }

    // garbati-platform T-047. PatchAsJsonAsync is the same PATCH-with-body helper
    // StaffIdentityApiClient.ChangeRoleAsync/ChangeStatusAsync and OrchestratorApi's own
    // IdentityApiClient.RecordKycDecisionAsync already use against this same style of IdentityApi PATCH
    // endpoint - not a new convention.
    public async Task<CustomerResponse> UpdateCustomerDetailsAsync(
        string customerId, UpdateCustomerDetailsRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PatchAsJsonAsync($"v1/customers/{customerId}/details", request, cancellationToken);
        return await ReadOrThrowAsync<CustomerResponse>(
            response, $"IdentityApi PATCH /v1/customers/{customerId}/details", cancellationToken);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new DownstreamApiException("IdentityApi", (int)response.StatusCode, $"{what}: empty response body.");
        }

        var problem = await DownstreamResponseReader.ReadProblemAsync(response, cancellationToken);
        throw new DownstreamApiException(
            "IdentityApi", (int)response.StatusCode, $"{what}: {DownstreamResponseReader.DescribeFailure(problem, response)}");
    }
}
