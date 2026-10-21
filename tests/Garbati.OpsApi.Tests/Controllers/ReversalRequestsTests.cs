using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Orchestrator;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Exceptions;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 5 / Section 4 (garbati-platform T-034) - the one workflow built fully executable end
// to end against OrchestratorApi's real POST /v1/reversals contract.
[Collection("Api")]
public class ReversalRequestsTests(ApiFixture fixture)
{
    [Fact]
    public async Task Create_WithTellerSession_Succeeds()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-1", "TXN-1", 100m, "NGN", "DUPL"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.StartsWith("OPS-REV-", body!.Id);
        Assert.Equal("PendingApproval", body.Status);
    }

    [Fact]
    public async Task Create_WithCustomerServiceOfficerSession_Succeeds()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-cso", "TXN-CSO", 100m, "NGN", "CUST"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(OpsRoles.Supervisor)]
    [InlineData(OpsRoles.ComplianceOfficer)]
    [InlineData(OpsRoles.Administrator)]
    public async Task Create_WithARoleThatMayNotRequestAReversal_Returns403(string role)
    {
        var (token, _) = await LoginAsAsync(role);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-2", "TXN-2", 100m, "NGN", "DUPL"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAMissingIdempotencyKey_Returns400()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("", "TXN-3", 100m, "NGN", "DUPL"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("BOGUS")]
    [InlineData("dupl")] // lower-case rejected deliberately - exact-list match, task file: "validate
                          // against this exact list" (not IdentityApi's own more lenient case-insensitive parse).
    public async Task Create_WithAnInvalidReasonCode_Returns400(string reasonCode)
    {
        var (token, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-4", "TXN-4", 100m, "NGN", reasonCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithANonPositiveAmount_Returns400()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-5", "TXN-5", 0m, "NGN", "DUPL"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Task file: "a client-supplied, validated IdempotencyKey is required... it must be retry-safe from
    // the moment it's created" - a browser retry of the CREATE action itself must return the original
    // request, not silently create a second one for the same reversal.
    [Fact]
    public async Task Create_RetriedWithTheSameIdempotencyKey_ReturnsTheSameRequest_NotASecondOne()
    {
        var (token, staffId) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        var request = new CreateReversalRequestRequest("rev-key-retry", "TXN-RETRY", 50m, "NGN", "TECH");

        using var first = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals", request);
        using var second = await Send(client, token, HttpMethod.Post, "/v1/ops/reversals", request);

        var firstBody = await first.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody!.Id, secondBody!.Id);

        using var list = await Send(client, token, HttpMethod.Get, "/v1/ops/reversals");
        var listBody = await list.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();
        Assert.Single(listBody!.Where(r => r.RequestedByStaffId == staffId && r.OriginalTransactionReference == "TXN-RETRY"));
    }

    // Task file: "prove a Teller cannot see another Teller's reversal request."
    [Fact]
    public async Task List_TellerSeesOnlyTheirOwnRequests()
    {
        var (tokenA, _) = await LoginAsAsync(OpsRoles.Teller);
        var (tokenB, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var created = await Send(client, tokenA, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-ownership", "TXN-OWN", 10m, "NGN", "TECH"));
        var createdId = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        using var listAsB = await Send(client, tokenB, HttpMethod.Get, "/v1/ops/reversals");
        var listBodyB = await listAsB.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();
        Assert.DoesNotContain(listBodyB!, r => r.Id == createdId);

        using var listAsA = await Send(client, tokenA, HttpMethod.Get, "/v1/ops/reversals");
        var listBodyA = await listAsA.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();
        Assert.Contains(listBodyA!, r => r.Id == createdId);
    }

    // Task file: "prove a ComplianceOfficer can see everything."
    [Theory]
    [InlineData(OpsRoles.ComplianceOfficer)]
    [InlineData(OpsRoles.RiskOfficer)]
    [InlineData(OpsRoles.Auditor)]
    public async Task List_FullOversightRoles_SeeEveryRequest_RegardlessOfWhoCreatedIt(string oversightRole)
    {
        var (tellerToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, tellerToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest($"rev-key-oversight-{oversightRole}", "TXN-OVR", 10m, "NGN", "TECH"));
        var createdId = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (oversightToken, _) = await LoginAsAsync(oversightRole);
        using var list = await Send(client, oversightToken, HttpMethod.Get, "/v1/ops/reversals");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();
        Assert.Contains(listBody!, r => r.Id == createdId);
    }

    [Fact]
    public async Task List_WithAdministratorSession_Returns403()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.Administrator);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Get, "/v1/ops/reversals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Task file: "the requester cannot approve their own request" - same staffId, a later session
    // happens to hold Supervisor (isolates the identity rule from the role rule, tested separately below).
    [Fact]
    public async Task Approve_ByTheOriginalRequester_Returns422_AndNeverCallsOrchestratorApi()
    {
        const string staffId = "STF-SELF-APPROVE";
        var requesterToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Self Approver", OpsRoles.Teller, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8));
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-self", "TXN-SELF", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var approverToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Self Approver", OpsRoles.Supervisor, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8));
        var callsBefore = fixture.OrchestratorApiClient.ReverseCallCount;

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(callsBefore, fixture.OrchestratorApiClient.ReverseCallCount);
    }

    // Task file: "a non-Supervisor approver is rejected."
    [Fact]
    public async Task Approve_ByANonSupervisor_Returns403_AndNeverCallsOrchestratorApi()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-nonsup", "TXN-NONSUP", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (auditorToken, _) = await LoginAsAsync(OpsRoles.Auditor);
        var callsBefore = fixture.OrchestratorApiClient.ReverseCallCount;

        using var response = await Send(client, auditorToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(callsBefore, fixture.OrchestratorApiClient.ReverseCallCount);
    }

    // Task file / DESIGN.md decision #4: "re-checked live, not just at queue-display time." A role that
    // was valid when the approver's session was first issued, but has since changed, must be rejected -
    // not a value anything cached from an earlier page load.
    [Fact]
    public async Task Approve_WhenTheApproversRoleHasChangedSinceLogin_Returns403()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-livecheck", "TXN-LIVE", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        // Demoted after login, before the approve request arrives - exactly the scenario the hard rule
        // guards against.
        await fixture.SetSessionRoleAsync(approverToken, OpsRoles.Teller);

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_AlreadyDecidedRequest_Returns409()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-twice", "TXN-TWICE", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var firstApprove = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");
        Assert.True(firstApprove.IsSuccessStatusCode || firstApprove.StatusCode == HttpStatusCode.UnprocessableEntity);

        var (secondApproverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var secondApprove = await Send(client, secondApproverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task Reject_WithAMissingRejectionReason_Returns400()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-rejnoreason", "TXN-REJNR", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/reject",
            new RejectRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithAReason_SetsRejectedAndNeverCallsOrchestratorApi()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-reject", "TXN-REJ", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, approverId) = await LoginAsAsync(OpsRoles.Supervisor);
        var callsBefore = fixture.OrchestratorApiClient.ReverseCallCount;
        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/reject",
            new RejectRequest("Insufficient evidence."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal("Rejected", body!.Status);
        Assert.Equal("Insufficient evidence.", body.RejectionReason);
        Assert.Equal(approverId, body.ApprovedByStaffId);
        Assert.Equal(callsBefore, fixture.OrchestratorApiClient.ReverseCallCount);
    }

    // The single most important behaviour this task adds: the maker-checker approval actually EXECUTES.
    // Task file: "RequestedBy sent to OrchestratorApi must be the ORIGINAL requester's staff id, not the
    // approver's" + "a deterministic idempotency key derived from this OpsReversalRequest's own id."
    [Fact]
    public async Task Approve_CallsOrchestratorApi_WithADeterministicIdempotencyKey_AndTheOriginalRequestersStaffId()
    {
        var (requesterToken, requesterId) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-exec", "TXN-EXEC", 250m, "NGN", "FRAD"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, approverId) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.OrchestratorApiClient.ReverseHandler =
            r => new ReversalResultResponse("DEC-EXEC-1", "TXN-REVERSED-1", "Success", null);

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var last = fixture.OrchestratorApiClient.LastReversalRequest!;
        Assert.Equal($"ops:{id}", last.IdempotencyKey);
        Assert.Equal(requesterId, last.RequestedBy);
        Assert.NotEqual(approverId, last.RequestedBy);
        Assert.Equal(250m, last.Amount);
        Assert.Equal("NGN", last.Currency);
        Assert.Equal("FRAD", last.ReasonCode);
        Assert.Equal("TXN-EXEC", last.OriginalTransactionReference);

        var body = await response.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal("Executed", body!.Status);
        Assert.Equal("TXN-REVERSED-1", body.ExecutedReference);
        Assert.Contains("DEC-EXEC-1", body.ExecutionDetail);
        Assert.Equal(approverId, body.ApprovedByStaffId);
    }

    [Fact]
    public async Task Approve_WhenOrchestratorApiReportsABusinessFailure_StoresExecutionFailed_AndReturns422()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-failed", "TXN-FAILED", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.OrchestratorApiClient.ReverseHandler =
            r => new ReversalResultResponse("DEC-FAIL-1", "TXN-FAILED", "Failed", "insufficient funds");

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal("ExecutionFailed", body!.Status);
        Assert.Contains("insufficient funds", body.ExecutionDetail);
    }

    // Task file: "do not let a failure to record that detail mask the original call's own outcome" - any
    // OTHER error from the call (not just a 422), including a transport-level/unexpected exception, is
    // also ExecutionFailed, not an opaque 500.
    [Fact]
    public async Task Approve_WhenOrchestratorApiCallThrows_StillStoresExecutionFailed_NotA500()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-throws", "TXN-THROWS", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.OrchestratorApiClient.ReverseThrows =
            r => new DownstreamApiException("OrchestratorApi", 500, "boom");

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal("ExecutionFailed", body!.Status);
        Assert.Contains("500", body.ExecutionDetail);
    }

    // Task file's lazy-expiry-aware listing: mirrors Garbati.IdentityApi's own T-031 fix shape - a
    // read-only query condition, not a write. A stale row still correctly fails if actually approved.
    [Fact]
    public async Task List_ExcludesAPendingRequestPastItsExpiryWindow_ForTheSupervisorQueue()
    {
        var (requesterToken, requesterId) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-expiry", "TXN-EXPIRY", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        await fixture.AgeReversalRequestAsync(id, DateTimeOffset.UtcNow.AddHours(-25));

        var (supervisorToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var list = await Send(client, supervisorToken, HttpMethod.Get, "/v1/ops/reversals");
        var listBody = await list.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();
        Assert.DoesNotContain(listBody!, r => r.Id == id);

        // GET by id still works and still shows the real, stale status (same shape T-031 used) - it is
        // only excluded from LIST results, never silently rewritten.
        using var get = await Send(client, supervisorToken, HttpMethod.Get, $"/v1/ops/reversals/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var getBody = await get.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        Assert.Equal("PendingApproval", getBody!.Status);

        // Task file: "even though approving it would still correctly fail" - the live expiry check at
        // decision time, not just the list exclusion.
        using var approve = await Send(client, supervisorToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
    }

    [Fact]
    public async Task List_ComplianceOfficer_StillSeesAnExpiredPendingRequest_FullOversightIsNeverFiltered()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-expiry-oversight", "TXN-EXPOV", 10m, "NGN", "TECH"));
        var id = (await created.Content.ReadFromJsonAsync<ReversalRequestResponse>())!.Id;

        await fixture.AgeReversalRequestAsync(id, DateTimeOffset.UtcNow.AddHours(-25));

        var (complianceToken, _) = await LoginAsAsync(OpsRoles.ComplianceOfficer);
        using var list = await Send(client, complianceToken, HttpMethod.Get, "/v1/ops/reversals");
        var listBody = await list.Content.ReadFromJsonAsync<List<ReversalRequestResponse>>();

        Assert.Contains(listBody!, r => r.Id == id);
    }

    private async Task<(string Token, string StaffId)> LoginAsAsync(string role)
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var token = await fixture.CreateSessionDirectlyAsync(staffId, "Test Staff", role, now, now.AddHours(8));
        return (token, staffId);
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
