using System.Net;
using Garbati.OpsApi.Clients.Implementations;

namespace Garbati.OpsApi.Tests.Clients;

public class IdentityApiClientTests
{
    private static IdentityApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://identity.invalid") });

    [Fact]
    public async Task GetCustomerAsync_OnA404_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, jsonBody: null);
        var client = CreateClient(handler);

        var response = await client.GetCustomerAsync("CUS-DOES-NOT-EXIST", CancellationToken.None);

        Assert.Null(response);
        Assert.Equal("http://identity.invalid/v1/customers/CUS-DOES-NOT-EXIST", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetCustomerAsync_ParsesTheRedactedResponse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"customerId":"CUS-1","customerReference":null,"customerStatus":"Active","kycStatus":"Verified","kycTier":"Tier2","jurisdictionCode":"NG","createdAt":"2026-10-18T00:00:00Z","updatedAt":"2026-10-18T00:00:00Z"}""");
        var client = CreateClient(handler);

        var response = await client.GetCustomerAsync("CUS-1", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Verified", response.KycStatus);
    }

    [Fact]
    public async Task ListPendingAuthorizationRequestsAsync_SendsTheStatusFilterAndPageSize()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"items":[],"nextPageToken":null}""");
        var client = CreateClient(handler);

        await client.ListPendingAuthorizationRequestsAsync("Pending", 20, null, CancellationToken.None);

        Assert.Equal(
            "http://identity.invalid/v1/authorization/requests?status=Pending&pageSize=20",
            handler.LastRequest!.RequestUri!.ToString());
    }
}
