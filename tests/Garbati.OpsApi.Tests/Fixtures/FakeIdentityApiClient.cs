using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Clients.Interfaces;

namespace Garbati.OpsApi.Tests.Fixtures;

public class FakeIdentityApiClient : IIdentityApiClient
{
    public int GetCustomerCallCount { get; private set; }
    public int ListPendingCallCount { get; private set; }
    public int GetAuthorizationRequestCallCount { get; private set; }
    public int ApproveCallCount { get; private set; }
    public int UpdateCustomerDetailsCallCount { get; private set; }

    public string? LastGetCustomerId { get; private set; }
    public (string Id, ApproveRequest Request)? LastApproveCall { get; private set; }
    public (string CustomerId, UpdateCustomerDetailsRequest Request)? LastUpdateCustomerDetailsCall { get; private set; }

    public Func<string, CustomerResponse?>? GetCustomerHandler { get; set; }
    public Func<string, int, string?, PagedResponse<PendingApprovalRequestResponse>>? ListPendingHandler { get; set; }
    public Func<string, PendingApprovalRequestResponse?>? GetAuthorizationRequestHandler { get; set; }
    public Func<string, ApproveRequest, PendingApprovalRequestResponse>? ApproveHandler { get; set; }
    public Func<string, UpdateCustomerDetailsRequest, CustomerResponse>? UpdateCustomerDetailsHandler { get; set; }
    public Func<string, UpdateCustomerDetailsRequest, Exception>? UpdateCustomerDetailsThrows { get; set; }

    public Task<CustomerResponse?> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
    {
        GetCustomerCallCount++;
        LastGetCustomerId = customerId;
        var handler = GetCustomerHandler ?? (_ => null);
        return Task.FromResult(handler(customerId));
    }

    public Task<PagedResponse<PendingApprovalRequestResponse>> ListPendingAuthorizationRequestsAsync(
        string status, int pageSize, string? pageToken, CancellationToken cancellationToken)
    {
        ListPendingCallCount++;
        var handler = ListPendingHandler ?? ((_, _, _) => new PagedResponse<PendingApprovalRequestResponse>([], null));
        return Task.FromResult(handler(status, pageSize, pageToken));
    }

    public Task<PendingApprovalRequestResponse?> GetAuthorizationRequestAsync(string id, CancellationToken cancellationToken)
    {
        GetAuthorizationRequestCallCount++;
        var handler = GetAuthorizationRequestHandler ?? (_ => null);
        return Task.FromResult(handler(id));
    }

    public Task<PendingApprovalRequestResponse> ApproveAuthorizationRequestAsync(
        string id, ApproveRequest request, CancellationToken cancellationToken)
    {
        ApproveCallCount++;
        LastApproveCall = (id, request);
        var handler = ApproveHandler ?? ((requestId, approve) => new PendingApprovalRequestResponse(
            requestId, "Debit", "Approved", "1000000001", "CUS-REQ", "Debit", "1000000001", "1000000002",
            "100.00", "NGN", null, 2, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), $"DEC-{requestId}", "fake.grant"));
        return Task.FromResult(handler(id, request));
    }

    public Task<CustomerResponse> UpdateCustomerDetailsAsync(
        string customerId, UpdateCustomerDetailsRequest request, CancellationToken cancellationToken)
    {
        UpdateCustomerDetailsCallCount++;
        LastUpdateCustomerDetailsCall = (customerId, request);

        if (UpdateCustomerDetailsThrows?.Invoke(customerId, request) is { } ex)
        {
            throw ex;
        }

        var handler = UpdateCustomerDetailsHandler ?? ((id, req) => new CustomerResponse(
            id, null, "Active", "Verified", "Tier2", "NG", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        return Task.FromResult(handler(customerId, request));
    }
}
