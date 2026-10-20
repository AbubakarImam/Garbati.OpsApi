using System.Net;
using Garbati.OpsApi.Clients.Contracts.Orchestrator;
using Garbati.OpsApi.Clients.Implementations;

namespace Garbati.OpsApi.Tests.Clients;

public class OrchestratorApiClientTests
{
    private static OrchestratorApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://orchestrator.invalid") });

    [Fact]
    public async Task TransferAsync_OnA422_StillReturnsTheBody()
    {
        // OrchestratorApi's own TransfersController returns 200 or 422 for the exact same response
        // shape, depending on Status - this client must read the body either way (confirmed against the
        // live controller, Garbati.OrchestratorApi.Controllers.TransfersController.Transfer).
        var handler = new FakeHttpMessageHandler(HttpStatusCode.UnprocessableEntity,
            """{"decisionId":"DEC-1","reference":"TXN-1","status":"Failed","failureReason":"Insufficient funds"}""");
        var client = CreateClient(handler);

        var request = new TransferRequest("ops:1", "CUS-1", "1000000001", "1000000002", 100m, "NGN", null);
        var response = await client.TransferAsync(request, CancellationToken.None);

        Assert.Equal("Failed", response.Status);
        Assert.Equal("Insufficient funds", response.FailureReason);
    }

    [Fact]
    public async Task StartAccountOpeningAsync_SendsTheExpectedRoute()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"id":"OP-1","status":"Complete","currentStep":"Complete","customerId":"CUS-1","ledgerAccountId":"1000000001","failureReason":null,"createdAt":"2026-10-18T00:00:00Z","completedAt":"2026-10-18T00:00:00Z"}""");
        var client = CreateClient(handler);

        var request = new StartAccountOpeningRequest(
            "ops:1", "Ada Lovelace", "NG", null, null, null, "Ada's Savings", "Savings", "NGN");
        await client.StartAccountOpeningAsync(request, CancellationToken.None);

        Assert.Equal("http://orchestrator.invalid/v1/account-openings", handler.LastRequest!.RequestUri!.ToString());
    }
}
