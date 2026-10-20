using System.Net;
using Garbati.OpsApi.Clients.Contracts.Ledger;
using Garbati.OpsApi.Clients.Implementations;

namespace Garbati.OpsApi.Tests.Clients;

public class LedgerApiClientTests
{
    private static LedgerApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ledger.invalid") });

    [Fact]
    public async Task GetAccountAsync_OnA404_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, jsonBody: null);
        var client = CreateClient(handler);

        var response = await client.GetAccountAsync("9999999999", CancellationToken.None);

        Assert.Null(response);
        Assert.Equal("http://ledger.invalid/api/accounts/9999999999", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetBalanceAsync_SendsTheExpectedRoute()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"accountNumber":"1000000001","balance":500.00,"currencyCode":"NGN","asOf":"2026-10-20T00:00:00Z"}""");
        var client = CreateClient(handler);

        var response = await client.GetBalanceAsync("1000000001", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(500.00m, response.Balance);
        Assert.Equal("http://ledger.invalid/api/accounts/1000000001/balance", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task DepositAsync_SendsTheExpectedRouteAndBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"reference":"TXN-1","status":"Posted"}""");
        var client = CreateClient(handler);

        await client.DepositAsync(new DepositRequest("1000000001", 100m, "Till funding", "ops:1"), CancellationToken.None);

        Assert.Equal("http://ledger.invalid/api/deposits", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(
            """{"customerAccountNumber":"1000000001","amount":100,"narration":"Till funding","idempotencyKey":"ops:1"}""",
            handler.LastRequestBody);
    }

    [Fact]
    public async Task DepositAsync_OnA422_StillReturnsTheBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.UnprocessableEntity,
            """{"reference":"TXN-2","status":"Failed","failureReason":"Account closed"}""");
        var client = CreateClient(handler);

        var response = await client.DepositAsync(new DepositRequest("1000000002", 50m, null, "ops:2"), CancellationToken.None);

        Assert.Equal("Failed", response.Status);
    }
}
