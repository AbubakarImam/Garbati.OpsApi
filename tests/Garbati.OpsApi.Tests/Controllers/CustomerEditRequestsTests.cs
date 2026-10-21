using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 4.1 (garbati-platform T-034) - STOPS at Approved in this task. Every test here that
// reaches Approve also confirms the fake IIdentityApiClient was never touched (task file, explicit).
[Collection("Api")]
public class CustomerEditRequestsTests(ApiFixture fixture)
{
    [Fact]
    public async Task Create_WithCustomerServiceOfficerSession_Succeeds()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-1", "New Name", null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();
        Assert.StartsWith("OPS-EDIT-", body!.Id);
        Assert.Equal("PendingApproval", body.Status);
    }

    [Theory]
    [InlineData(OpsRoles.Teller)]
    [InlineData(OpsRoles.Supervisor)]
    [InlineData(OpsRoles.Administrator)]
    public async Task Create_WithARoleThatMayNotProposeAnEdit_Returns403(string role)
    {
        var (token, _) = await LoginAsAsync(role);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-1", "New Name", null, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNoProposedFieldsAtAll_Returns400()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-1", null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithOnlyAnEmailProposed_Succeeds()
    {
        var (token, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-1", null, "new@example.test", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Task file: "only CustomerServiceOfficer (own requests) and Supervisor (everything) have any access
    // at all - every other role, including Administrator, gets 403."
    [Theory]
    [InlineData(OpsRoles.Teller)]
    [InlineData(OpsRoles.ComplianceOfficer)]
    [InlineData(OpsRoles.RiskOfficer)]
    [InlineData(OpsRoles.Auditor)]
    [InlineData(OpsRoles.Administrator)]
    public async Task List_WithAnyOtherRole_Returns403(string role)
    {
        var (token, _) = await LoginAsAsync(role);
        using var client = fixture.CreateClient();

        using var response = await Send(client, token, HttpMethod.Get, "/v1/ops/customer-edits");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_CustomerServiceOfficerSeesOnlyTheirOwnRequests()
    {
        var (tokenA, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        var (tokenB, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var created = await Send(client, tokenA, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-OWN", "Name A", null, null));
        var createdId = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        using var listAsB = await Send(client, tokenB, HttpMethod.Get, "/v1/ops/customer-edits");
        var listBodyB = await listAsB.Content.ReadFromJsonAsync<List<CustomerEditRequestResponse>>();
        Assert.DoesNotContain(listBodyB!, r => r.Id == createdId);

        using var listAsA = await Send(client, tokenA, HttpMethod.Get, "/v1/ops/customer-edits");
        var listBodyA = await listAsA.Content.ReadFromJsonAsync<List<CustomerEditRequestResponse>>();
        Assert.Contains(listBodyA!, r => r.Id == createdId);
    }

    [Fact]
    public async Task List_SupervisorSeesEveryRequest()
    {
        var (csoToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, csoToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-SUP-VIEW", "Name B", null, null));
        var createdId = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (supervisorToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var list = await Send(client, supervisorToken, HttpMethod.Get, "/v1/ops/customer-edits");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<List<CustomerEditRequestResponse>>();
        Assert.Contains(listBody!, r => r.Id == createdId);
    }

    [Fact]
    public async Task Approve_ByTheOriginalRequester_Returns422()
    {
        const string staffId = "STF-CSO-SELF";
        var requesterToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Self Approver", OpsRoles.CustomerServiceOfficer, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8));
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-SELF", "Name C", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var approverToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Self Approver", OpsRoles.Supervisor, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8));

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ByANonSupervisor_Returns403()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-NONSUP", "Name D", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (otherCsoToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var response = await Send(client, otherCsoToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WhenTheApproversRoleHasChangedSinceLogin_Returns403()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-LIVE", "Name E", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        await fixture.SetSessionRoleAsync(approverToken, OpsRoles.CustomerServiceOfficer);

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithAMissingRejectionReason_Returns400()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-REJNR", "Name F", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (approverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/reject",
            new RejectRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // This task's core boundary, stated explicitly in the PR: this workflow never calls IdentityApi, even
    // on a successful approval. Task file: "assert the fake IIdentityApiClient's relevant call count
    // stays zero throughout."
    [Fact]
    public async Task Approve_Succeeds_StopsAtApproved_AndNeverCallsIdentityApi()
    {
        var (requesterToken, requesterId) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        var getCustomerCallsBefore = fixture.IdentityApiClient.GetCustomerCallCount;
        var listPendingCallsBefore = fixture.IdentityApiClient.ListPendingCallCount;
        var getAuthorizationCallsBefore = fixture.IdentityApiClient.GetAuthorizationRequestCallCount;
        var approveCallsBefore = fixture.IdentityApiClient.ApproveCallCount;

        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-FULL", "New Full Name", "new@example.test", "+2348000000000"));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (approverToken, approverId) = await LoginAsAsync(OpsRoles.Supervisor);
        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();
        Assert.Equal("Approved", body!.Status);
        Assert.Equal(approverId, body.ApprovedByStaffId);
        Assert.Equal(requesterId, body.RequestedByStaffId);
        Assert.Equal("New Full Name", body.ProposedFullName);
        Assert.Equal("new@example.test", body.ProposedEmail);
        Assert.Equal("+2348000000000", body.ProposedPhoneNumber);

        // Nothing about this workflow ever touched IdentityApi - every counter is exactly where it was
        // before this test's own create+approve calls ran.
        Assert.Equal(getCustomerCallsBefore, fixture.IdentityApiClient.GetCustomerCallCount);
        Assert.Equal(listPendingCallsBefore, fixture.IdentityApiClient.ListPendingCallCount);
        Assert.Equal(getAuthorizationCallsBefore, fixture.IdentityApiClient.GetAuthorizationRequestCallCount);
        Assert.Equal(approveCallsBefore, fixture.IdentityApiClient.ApproveCallCount);
    }

    [Fact]
    public async Task List_ExcludesAPendingRequestPastItsExpiryWindow()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-EXPIRY", "Name G", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        await fixture.AgeCustomerEditRequestAsync(id, DateTimeOffset.UtcNow.AddHours(-25));

        var (supervisorToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var list = await Send(client, supervisorToken, HttpMethod.Get, "/v1/ops/customer-edits");
        var listBody = await list.Content.ReadFromJsonAsync<List<CustomerEditRequestResponse>>();
        Assert.DoesNotContain(listBody!, r => r.Id == id);

        // Even though approving it would still correctly fail (task file's own framing, mirrored from
        // reversals) - GET by id still works and shows the real stale status.
        using var get = await Send(client, supervisorToken, HttpMethod.Get, $"/v1/ops/customer-edits/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        using var approve = await Send(client, supervisorToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
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
