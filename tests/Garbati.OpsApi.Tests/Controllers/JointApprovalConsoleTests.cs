using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 6 item 6 / Section 4: Supervisor can act, Auditor is read-only - enforced per action,
// not left to a UI that doesn't exist yet (task file: "an Auditor calling the approve action must be
// rejected, not just have it hidden from them in some UI that doesn't exist yet").
[Collection("Api")]
public class JointApprovalConsoleTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("Supervisor")]
    [InlineData("Auditor")]
    public async Task ListPending_WithSupervisorOrAuditor_Succeeds(string role)
    {
        var token = await LoginAs(role);
        fixture.IdentityApiClient.ListPendingHandler = (_, _, _) => new([], null);

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, HttpMethod.Get, "/v1/ops/joint-approvals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListPending_WithATellerSession_Returns403()
    {
        var token = await LoginAs("Teller");

        using var client = fixture.CreateClient();
        using var response = await Send(client, token, HttpMethod.Get, "/v1/ops/joint-approvals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithSupervisor_Succeeds()
    {
        var token = await LoginAs("Supervisor");
        fixture.IdentityApiClient.ApproveHandler = (id, request) => new(
            id, "Debit", "Approved", "1000000001", "CUS-REQ", "Debit", "1000000001", "1000000002",
            "100.00", "NGN", null, 2, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "DEC-1", "fake.grant");

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Post, "/v1/ops/joint-approvals/REQ-1/approve",
            new ApproveJointRequestRequest("CUS-APPROVER"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("REQ-1", "CUS-APPROVER"), (
            fixture.IdentityApiClient.LastApproveCall!.Value.Id, fixture.IdentityApiClient.LastApproveCall!.Value.Request.CustomerId));
    }

    // DESIGN.md Section 4: the exact case the task file calls out by name - Auditor is explicitly
    // read-only (4) and must be REJECTED from the approve action, not merely lack a button for it.
    [Fact]
    public async Task Approve_WithAuditor_Returns403_AndNeverCallsIdentityApi()
    {
        var token = await LoginAs("Auditor");
        // The fake client is shared across every test in this collection, so a count is compared
        // before/after THIS action rather than asserted as an absolute 0.
        var approveCallsBefore = fixture.IdentityApiClient.ApproveCallCount;

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Post, "/v1/ops/joint-approvals/REQ-1/approve",
            new ApproveJointRequestRequest("CUS-APPROVER"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(approveCallsBefore, fixture.IdentityApiClient.ApproveCallCount);
    }

    private async Task<string> LoginAs(string role)
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        return await fixture.CreateSessionDirectlyAsync(staffId, "Test Staff", role, now, now.AddHours(8));
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string token, HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={token}");
        return client.SendAsync(request);
    }
}
