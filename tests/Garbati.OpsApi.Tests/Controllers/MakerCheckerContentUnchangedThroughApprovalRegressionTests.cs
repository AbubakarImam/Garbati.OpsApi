using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Orchestrator;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// D-29 (garbati-platform), second round of the same reviewer's feedback on the same thread:
// "a regression test can preserve that design choice: assert the request's content stays identical from
// creation through approval in each flow." This is complementary to
// MakerCheckerNoEditActionRegressionTests/MakerCheckerServiceInterfaceRegressionTests, not a replacement -
// those prove STRUCTURALLY that no edit endpoint/method exists; these prove BEHAVIOURALLY that the actual
// create -> approve round trip never changes the fields a checker reviewed. That also catches a subtler
// bug the structural tests cannot: ApproveAsync (or anything it calls) accidentally reassigning one of
// those fields on the same entity before saving, with no edit endpoint involved at all.
//
// Each test captures a snapshot of the content-bearing fields right after creation, drives the request
// through the real Approve action (against this suite's existing fake downstream clients - the same
// ApiFixture/FakeOrchestratorApiClient/FakeIdentityApiClient pattern every other Approve test in this repo
// already uses, not a new mocking pattern), re-fetches the entity by id, and asserts every one of those
// fields is still exactly what was captured. If a future change ever lets Approve (or anything upstream of
// it) mutate the reviewed content, this fails - the same "force a deliberate decision" shape D-29's first
// round already established for the structural tests.
[Collection("Api")]
public class MakerCheckerContentUnchangedThroughApprovalRegressionTests(ApiFixture fixture)
{
    [Fact]
    public async Task ReversalRequest_ContentIsUnchanged_FromCreationThroughApproval()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.Teller);
        using var client = fixture.CreateClient();

        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/reversals",
            new CreateReversalRequestRequest("rev-key-content-unchanged", "TXN-CONTENT", 777.50m, "NGN", "DUPL"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var snapshot = await created.Content.ReadFromJsonAsync<ReversalRequestResponse>();
        var id = snapshot!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.OrchestratorApiClient.ReverseHandler =
            r => new ReversalResultResponse("DEC-CONTENT-1", "TXN-CONTENT-REVERSED", "Success", null);

        using var approve = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/reversals/{id}/approve");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var get = await Send(client, approverToken, HttpMethod.Get, $"/v1/ops/reversals/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var afterApproval = await get.Content.ReadFromJsonAsync<ReversalRequestResponse>();

        Assert.Equal(snapshot.OriginalTransactionReference, afterApproval!.OriginalTransactionReference);
        Assert.Equal(snapshot.Amount, afterApproval.Amount);
        Assert.Equal(snapshot.Currency, afterApproval.Currency);
        Assert.Equal(snapshot.ReasonCode, afterApproval.ReasonCode);
    }

    [Fact]
    public async Task CustomerEditRequest_ContentIsUnchanged_FromCreationThroughApproval()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest(
                "CUS-CONTENT", "Content Unchanged Name", "content-unchanged@example.test", "+2348011111111"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var snapshot = await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();
        var id = snapshot!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);

        using var approve = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        using var get = await Send(client, approverToken, HttpMethod.Get, $"/v1/ops/customer-edits/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var afterApproval = await get.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();

        Assert.Equal(snapshot.TargetCustomerId, afterApproval!.TargetCustomerId);
        Assert.Equal(snapshot.ProposedFullName, afterApproval.ProposedFullName);
        Assert.Equal(snapshot.ProposedEmail, afterApproval.ProposedEmail);
        Assert.Equal(snapshot.ProposedPhoneNumber, afterApproval.ProposedPhoneNumber);
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
