using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Exceptions;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 4.1 (garbati-platform T-034/T-047). T-034 stopped at Approved; T-047 (this file's
// Approve tests below) closes that gap - Approve now actually calls the fake IIdentityApiClient and
// reaches Applied/ApplicationFailed, mirroring ReversalRequestsTests' own Approve coverage for the
// execution-call shape.
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
        var callsBefore = fixture.IdentityApiClient.UpdateCustomerDetailsCallCount;

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(callsBefore, fixture.IdentityApiClient.UpdateCustomerDetailsCallCount);
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
        var callsBefore = fixture.IdentityApiClient.UpdateCustomerDetailsCallCount;
        using var response = await Send(client, otherCsoToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(callsBefore, fixture.IdentityApiClient.UpdateCustomerDetailsCallCount);
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
        var callsBefore = fixture.IdentityApiClient.UpdateCustomerDetailsCallCount;

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(callsBefore, fixture.IdentityApiClient.UpdateCustomerDetailsCallCount);
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

    // T-047's core behaviour: Approve now actually calls IdentityApi and reaches Applied, not just
    // Approved. Confirms the exact customerId/FullName/Email/PhoneNumber the fake client was called with,
    // mirroring ReversalRequestsTests.Approve_CallsOrchestratorApi_...'s own "assert the exact downstream
    // call" shape.
    [Fact]
    public async Task Approve_Succeeds_CallsIdentityApi_AndReachesApplied()
    {
        var (requesterToken, requesterId) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();

        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-FULL", "New Full Name", "new@example.test", "+2348000000000"));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (approverToken, approverId) = await LoginAsAsync(OpsRoles.Supervisor);
        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();
        Assert.Equal("Applied", body!.Status);
        Assert.Null(body.ApplicationError);
        Assert.Equal(approverId, body.ApprovedByStaffId);
        Assert.Equal(requesterId, body.RequestedByStaffId);
        Assert.Equal("New Full Name", body.ProposedFullName);
        Assert.Equal("new@example.test", body.ProposedEmail);
        Assert.Equal("+2348000000000", body.ProposedPhoneNumber);

        var (calledCustomerId, calledRequest) = fixture.IdentityApiClient.LastUpdateCustomerDetailsCall!.Value;
        Assert.Equal("CUS-FULL", calledCustomerId);
        Assert.Equal("New Full Name", calledRequest.FullName);
        Assert.Equal("new@example.test", calledRequest.Email);
        Assert.Equal("+2348000000000", calledRequest.PhoneNumber);
    }

    // Task file: "any exception - not just a 422 business-rule failure - is still ApplicationFailed... the
    // error recorded is a clear, specific message." Approval itself (ApprovedByStaffId/DecidedAt) still
    // happened, same as ReversalRequestsTests.Approve_WhenOrchestratorApiCallThrows_... for reversals.
    [Fact]
    public async Task Approve_WhenIdentityApiCallThrows_StoresApplicationFailed_NotA500()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-THROWS", "Name Throws", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (approverToken, approverId) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.IdentityApiClient.UpdateCustomerDetailsThrows =
            (_, _) => new DownstreamApiException("IdentityApi", 500, "boom");

        using var response = await Send(client, approverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");
        fixture.IdentityApiClient.UpdateCustomerDetailsThrows = null;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerEditRequestResponse>();
        Assert.Equal("ApplicationFailed", body!.Status);
        Assert.NotNull(body.ApplicationError);
        Assert.Contains("500", body.ApplicationError);
        Assert.Contains("IdentityApi", body.ApplicationError);
        // Approval itself still happened, even though applying it failed.
        Assert.Equal(approverId, body.ApprovedByStaffId);
        Assert.NotNull(body.DecidedAt);
    }

    // Task file: "a second ApproveAsync call against an already-Applied (or ApplicationFailed) request
    // still throws OpsRequestNotPendingException" - confirms EnsureDecidable's existing
    // "Status != PendingApproval" check already covers both new terminal states with no code change there.
    [Fact]
    public async Task Approve_AlreadyAppliedRequest_Returns409()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-TWICE-APPLIED", "Name Twice", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (firstApproverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var firstApprove = await Send(client, firstApproverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");
        Assert.Equal("Applied", (await firstApprove.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Status);

        var (secondApproverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var secondApprove = await Send(client, secondApproverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task Approve_AlreadyApplicationFailedRequest_Returns409()
    {
        var (requesterToken, _) = await LoginAsAsync(OpsRoles.CustomerServiceOfficer);
        using var client = fixture.CreateClient();
        using var created = await Send(client, requesterToken, HttpMethod.Post, "/v1/ops/customer-edits",
            new CreateCustomerEditRequestRequest("CUS-TWICE-FAILED", "Name Twice Failed", null, null));
        var id = (await created.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Id;

        var (firstApproverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        fixture.IdentityApiClient.UpdateCustomerDetailsThrows =
            (_, _) => new DownstreamApiException("IdentityApi", 500, "boom");
        using var firstApprove = await Send(client, firstApproverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");
        fixture.IdentityApiClient.UpdateCustomerDetailsThrows = null;
        Assert.Equal("ApplicationFailed", (await firstApprove.Content.ReadFromJsonAsync<CustomerEditRequestResponse>())!.Status);

        var (secondApproverToken, _) = await LoginAsAsync(OpsRoles.Supervisor);
        using var secondApprove = await Send(client, secondApproverToken, HttpMethod.Post, $"/v1/ops/customer-edits/{id}/approve");

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
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
