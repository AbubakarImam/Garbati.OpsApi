using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 6 item 4 / Section 4: Teller or Supervisor only.
[Collection("Api")]
public class TestingConsoleTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("Teller")]
    [InlineData("Supervisor")]
    public async Task Fund_WithAnAllowedRole_Succeeds(string role)
    {
        var token = await LoginAs(role);
        fixture.LedgerApiClient.DepositHandler = _ => new("TXN-1", "Posted");

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, "/v1/ops/console/funding", new FundingRequest("1000000001", 100m, null, "fund-key-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FundingResultResponse>();
        Assert.Equal("Posted", body!.Status);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Auditor")]
    [InlineData("ComplianceOfficer")]
    [InlineData("RiskOfficer")]
    [InlineData("CustomerServiceOfficer")]
    public async Task Fund_WithADisallowedRole_Returns403(string role)
    {
        var token = await LoginAs(role);
        // The fake clients are shared across every test in this collection, so a count is compared
        // before/after THIS action rather than asserted as an absolute 0 - other tests in this class
        // legitimately call the same fake before or after this one runs.
        var depositCallsBefore = fixture.LedgerApiClient.DepositCallCount;

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, "/v1/ops/console/funding", new FundingRequest("1000000001", 100m, null, "fund-key-2"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(depositCallsBefore, fixture.LedgerApiClient.DepositCallCount);
    }

    [Fact]
    public async Task Transfer_UsesOrchestratorApi_NotLedgerApiDirectly()
    {
        var token = await LoginAs("Teller");
        fixture.OrchestratorApiClient.TransferHandler = _ => new("DEC-1", "TXN-1", "Posted", null);
        var transferCallsBefore = fixture.OrchestratorApiClient.TransferCallCount;
        var depositCallsBefore = fixture.LedgerApiClient.DepositCallCount;

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, "/v1/ops/console/transfers",
            new TransferRequest("transfer-key-1", "CUS-1", "1000000001", "1000000002", 50m, "NGN", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(transferCallsBefore + 1, fixture.OrchestratorApiClient.TransferCallCount);
        Assert.Equal(depositCallsBefore, fixture.LedgerApiClient.DepositCallCount);
    }

    [Fact]
    public async Task StartAccountOpening_WhenOrchestratorReportsFailed_Returns422()
    {
        var token = await LoginAs("Supervisor");
        fixture.OrchestratorApiClient.StartAccountOpeningHandler = _ => new(
            "OP-1", "Failed", "CreateLedgerAccount", null, null, "LedgerApi unavailable", DateTimeOffset.UtcNow, null);

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, "/v1/ops/console/account-openings",
            new StartAccountOpeningRequest("opening-key-1", "Ada Lovelace", "NG", null, null, null, "Ada's Savings", "Savings", "NGN"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // QA review of PR #1 (T-033): the original build generated a fresh IdempotencyKey server-side on
    // every call, which meant a browser retry (timeout, double-click) was indistinguishable from a brand
    // new operation downstream - exactly the scenario idempotency keys exist to prevent. This asserts the
    // actual captured value sent to the fake OrchestratorApi/LedgerApi clients matches the caller's own
    // key (prefixed "ops:" for namespacing), not just that a call happened - the same kind of regression
    // test T-024 showed is necessary for this class of bug.
    [Fact]
    public async Task Fund_ForwardsTheCallersIdempotencyKey_NotAFreshOnePerCall()
    {
        var token = await LoginAs("Teller");
        fixture.LedgerApiClient.DepositHandler = _ => new("TXN-1", "Posted");

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, "/v1/ops/console/funding", new FundingRequest("1000000001", 100m, null, "browser-retry-key"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ops:browser-retry-key", fixture.LedgerApiClient.LastDepositRequest!.IdempotencyKey);
    }

    [Fact]
    public async Task Transfer_ForwardsTheCallersIdempotencyKey_NotAFreshOnePerCall()
    {
        var token = await LoginAs("Teller");
        fixture.OrchestratorApiClient.TransferHandler = _ => new("DEC-1", "TXN-1", "Posted", null);

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, "/v1/ops/console/transfers",
            new TransferRequest("browser-retry-key", "CUS-1", "1000000001", "1000000002", 50m, "NGN", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ops:browser-retry-key", fixture.OrchestratorApiClient.LastTransferRequest!.IdempotencyKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has a space")]
    public async Task Fund_WithAMissingOrMalformedIdempotencyKey_Returns400_AndNeverCallsLedgerApi(string? key)
    {
        var token = await LoginAs("Teller");
        var depositCallsBefore = fixture.LedgerApiClient.DepositCallCount;

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, "/v1/ops/console/funding", new FundingRequest("1000000001", 100m, null, key!));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(depositCallsBefore, fixture.LedgerApiClient.DepositCallCount);
    }

    private async Task<string> LoginAs(string role)
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        return await fixture.CreateSessionDirectlyAsync(staffId, "Test Staff", role, now, now.AddHours(8));
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string token, string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={token}");
        return client.SendAsync(request);
    }
}
