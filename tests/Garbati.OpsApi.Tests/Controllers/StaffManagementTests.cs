using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 6 item 5 / Section 4: Administrator only.
[Collection("Api")]
public class StaffManagementTests(ApiFixture fixture)
{
    [Fact]
    public async Task CreateStaff_WithAdministrator_Succeeds_AndSendsTheCallingAdministratorAsActingStaffId()
    {
        var adminStaffId = $"STF-{Guid.NewGuid():N}";
        var token = await LoginAs(adminStaffId, "Administrator");

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Post, "/v1/ops/staff",
            new CreateStaffRequest("New Hire", "new.hire", "Teller"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Captured-argument assertion (task file: "the same kind of regression test T-024 showed is
        // necessary") - OpsApi itself must supply ActingStaffId from the session, never accept it (or
        // anything resembling it) from the browser's request body.
        Assert.NotNull(fixture.StaffIdentityApiClient.LastCreateStaffRequest);
        Assert.Equal(adminStaffId, fixture.StaffIdentityApiClient.LastCreateStaffRequest!.ActingStaffId);
        Assert.Equal("New Hire", fixture.StaffIdentityApiClient.LastCreateStaffRequest.FullName);
        Assert.Equal("Teller", fixture.StaffIdentityApiClient.LastCreateStaffRequest.Role);
    }

    [Theory]
    [InlineData("Teller")]
    [InlineData("Supervisor")]
    [InlineData("Auditor")]
    [InlineData("ComplianceOfficer")]
    [InlineData("RiskOfficer")]
    [InlineData("CustomerServiceOfficer")]
    public async Task CreateStaff_WithANonAdministratorRole_Returns403(string role)
    {
        var token = await LoginAs($"STF-{Guid.NewGuid():N}", role);
        // The fake client is shared across every test in this collection, so a count is compared
        // before/after THIS action rather than asserted as an absolute 0.
        var createStaffCallsBefore = fixture.StaffIdentityApiClient.CreateStaffCallCount;

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Post, "/v1/ops/staff",
            new CreateStaffRequest("New Hire", "new.hire", "Teller"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(createStaffCallsBefore, fixture.StaffIdentityApiClient.CreateStaffCallCount);
    }

    [Fact]
    public async Task ChangeRole_SendsTheCallingAdministratorAsActingStaffId_NotAnythingFromTheRequestBody()
    {
        var adminStaffId = $"STF-{Guid.NewGuid():N}";
        var token = await LoginAs(adminStaffId, "Administrator");
        var targetStaffId = $"STF-{Guid.NewGuid():N}";

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Patch, $"/v1/ops/staff/{targetStaffId}/role", new ChangeRoleRequest("Supervisor"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((targetStaffId, adminStaffId, "Supervisor"), (
            fixture.StaffIdentityApiClient.LastChangeRoleCall!.Value.StaffId,
            fixture.StaffIdentityApiClient.LastChangeRoleCall!.Value.Request.ActingStaffId,
            fixture.StaffIdentityApiClient.LastChangeRoleCall!.Value.Request.Role));
    }

    [Fact]
    public async Task ChangeSupervisor_DoesNotTriggerCascadeRevocation()
    {
        // DESIGN.md Section 2.3 names only role/status - a supervisor reassignment is not a
        // revocation trigger (StaffManagementService's own comment explains why).
        var adminStaffId = $"STF-{Guid.NewGuid():N}";
        var token = await LoginAs(adminStaffId, "Administrator");
        var targetStaffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var targetToken = await fixture.CreateSessionDirectlyAsync(targetStaffId, "Target", "Teller", now, now.AddHours(8));

        using var client = fixture.CreateClient();
        using var response = await Send(
            client, token, HttpMethod.Patch, $"/v1/ops/staff/{targetStaffId}/supervisor",
            new ChangeSupervisorRequest("STF-SOME-SUPERVISOR"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await fixture.CountSessionsForStaffAsync(targetStaffId));

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        meRequest.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={targetToken}");
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    private async Task<string> LoginAs(string staffId, string role)
    {
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
