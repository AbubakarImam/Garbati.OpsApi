using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Clients.Contracts.Ledger;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 6 item 1 / Section 4: every one of the seven roles may use this read-only lookup -
// there is no role to reject for this specific action, so the "gate works both ways" proof here is
// "any authenticated role succeeds" vs. "no session at all is rejected" (SessionTests already covers the
// missing/expired-session case in depth; this file focuses on the lookup's own combining behaviour).
[Collection("Api")]
public class CustomerLookupTests(ApiFixture fixture)
{
    [Fact]
    public async Task GetCustomer_WithAnAccountNumber_CombinesIdentityAndLedgerIntoOneResponse()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var token = await fixture.CreateSessionDirectlyAsync(staffId, "Auditor One", "Auditor", now, now.AddHours(8));

        fixture.IdentityApiClient.GetCustomerHandler = id => id == "CUS-1"
            ? new CustomerResponse("CUS-1", null, "Active", "Verified", "Tier2", "NG", now, now)
            : null;
        fixture.LedgerApiClient.GetAccountHandler = number => number == "1000000001"
            ? new AccountResponse("1000000001", "Ada's Savings", "Savings", "Customer", "NGN", "Active", now)
            : null;
        fixture.LedgerApiClient.GetBalanceHandler = number => number == "1000000001"
            ? new BalanceResponse("1000000001", 500m, "NGN", now)
            : null;

        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/customers/CUS-1?accountNumber=1000000001");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={token}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerLookupResponse>();
        Assert.Equal("CUS-1", body!.CustomerId);
        Assert.Equal("1000000001", body.AccountNumber);
        Assert.Equal(500m, body.Balance);
    }

    [Fact]
    public async Task GetCustomer_WithNoAccountNumber_ReturnsTheCustomerWithNullAccountFields()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var token = await fixture.CreateSessionDirectlyAsync(staffId, "Teller One", "Teller", now, now.AddHours(8));

        fixture.IdentityApiClient.GetCustomerHandler = _ =>
            new CustomerResponse("CUS-2", null, "Active", "Pending", null, "NG", now, now);

        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/customers/CUS-2");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={token}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerLookupResponse>();
        Assert.Null(body!.AccountNumber);
        Assert.Null(body.Balance);
    }

    [Fact]
    public async Task GetCustomer_WhenIdentityApiHasNoSuchCustomer_Returns404()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var token = await fixture.CreateSessionDirectlyAsync(staffId, "Teller Two", "Teller", now, now.AddHours(8));
        fixture.IdentityApiClient.GetCustomerHandler = _ => null;

        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/customers/CUS-MISSING");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={token}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomer_WithNoSession_Returns401()
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/v1/ops/customers/CUS-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
