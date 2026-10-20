using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 2.3, decision #2 - the single most important behavioural test in this task (task
// file: "this is the actual mechanism DESIGN.md 2.3 depends on, not just a documented intention"). Each
// test: creates a REAL session (a row in ops_sessions, reachable via a cookie), changes that staff
// member's role or status through this service's own staff-management endpoint, then confirms the
// session is actually gone - not just that the downstream client was called.
[Collection("Api")]
public class CascadeRevocationTests(ApiFixture fixture)
{
    private const string AdminStaffId = "STF-FIXED-ADMIN";

    [Fact]
    public async Task ChangingRole_DeletesEveryActiveSessionForThatStaffMember()
    {
        var targetStaffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var targetRawToken = await fixture.CreateSessionDirectlyAsync(
            targetStaffId, "Target Staff", "Teller", lastActivityAt: now, expiresAt: now.AddHours(8));
        var adminRawToken = await fixture.CreateSessionDirectlyAsync(
            AdminStaffId, "Admin", "Administrator", lastActivityAt: now, expiresAt: now.AddHours(8));

        fixture.StaffIdentityApiClient.ChangeRoleHandler = (staffId, request) => new(
            staffId, "Target Staff", "target.staff", request.Role, "Active", false, null,
            DateTimeOffset.UtcNow, request.ActingStaffId, DateTimeOffset.UtcNow);

        using var client = fixture.CreateClient();

        // Confirm the target's session genuinely works BEFORE the role change.
        using var meBefore = Request(HttpMethod.Get, "/v1/ops/me", targetRawToken);
        using var meBeforeResponse = await client.SendAsync(meBefore);
        Assert.Equal(HttpStatusCode.OK, meBeforeResponse.StatusCode);

        using var changeRoleRequest = Request(
            HttpMethod.Patch, $"/v1/ops/staff/{targetStaffId}/role", adminRawToken,
            new ChangeRoleRequest("Supervisor"));
        using var changeRoleResponse = await client.SendAsync(changeRoleRequest);
        Assert.Equal(HttpStatusCode.OK, changeRoleResponse.StatusCode);

        // The actual mechanism: the row is gone, not just "some revoke method was called".
        Assert.Equal(0, await fixture.CountSessionsForStaffAsync(targetStaffId));

        using var meAfter = Request(HttpMethod.Get, "/v1/ops/me", targetRawToken);
        using var meAfterResponse = await client.SendAsync(meAfter);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterResponse.StatusCode);
    }

    [Fact]
    public async Task ChangingStatus_DeletesEveryActiveSessionForThatStaffMember()
    {
        var targetStaffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var targetRawToken = await fixture.CreateSessionDirectlyAsync(
            targetStaffId, "Target Staff", "Teller", lastActivityAt: now, expiresAt: now.AddHours(8));
        var adminRawToken = await fixture.CreateSessionDirectlyAsync(
            $"{AdminStaffId}-{Guid.NewGuid():N}", "Admin", "Administrator", lastActivityAt: now, expiresAt: now.AddHours(8));

        fixture.StaffIdentityApiClient.ChangeStatusHandler = (staffId, request) => new(
            staffId, "Target Staff", "target.staff", "Teller", request.Status, false, null,
            DateTimeOffset.UtcNow, request.ActingStaffId, DateTimeOffset.UtcNow);

        using var client = fixture.CreateClient();

        using var changeStatusRequest = Request(
            HttpMethod.Patch, $"/v1/ops/staff/{targetStaffId}/status", adminRawToken,
            new ChangeStatusRequest("Suspended"));
        using var changeStatusResponse = await client.SendAsync(changeStatusRequest);
        Assert.Equal(HttpStatusCode.OK, changeStatusResponse.StatusCode);

        Assert.Equal(0, await fixture.CountSessionsForStaffAsync(targetStaffId));

        using var meAfter = Request(HttpMethod.Get, "/v1/ops/me", targetRawToken);
        using var meAfterResponse = await client.SendAsync(meAfter);
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterResponse.StatusCode);
    }

    [Fact]
    public async Task ChangingRole_DeletesMultipleConcurrentSessionsForTheSameStaffMember()
    {
        // DESIGN.md 2.3 says "every active OpsSession row" - plural. A staff member logged in from two
        // browsers/devices must lose both, not just the one used to trigger the request's precondition.
        var targetStaffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var tokenA = await fixture.CreateSessionDirectlyAsync(targetStaffId, "Target", "Teller", now, now.AddHours(8));
        var tokenB = await fixture.CreateSessionDirectlyAsync(targetStaffId, "Target", "Teller", now, now.AddHours(8));
        var adminToken = await fixture.CreateSessionDirectlyAsync(
            $"STF-ADMIN-{Guid.NewGuid():N}", "Admin", "Administrator", now, now.AddHours(8));

        fixture.StaffIdentityApiClient.ChangeRoleHandler = (staffId, request) => new(
            staffId, "Target", "target", request.Role, "Active", false, null, now, request.ActingStaffId, now);

        using var client = fixture.CreateClient();
        using var changeRoleRequest = Request(
            HttpMethod.Patch, $"/v1/ops/staff/{targetStaffId}/role", adminToken, new ChangeRoleRequest("Supervisor"));
        await client.SendAsync(changeRoleRequest);

        Assert.Equal(0, await fixture.CountSessionsForStaffAsync(targetStaffId));

        foreach (var token in new[] { tokenA, tokenB })
        {
            using var meRequest = Request(HttpMethod.Get, "/v1/ops/me", token);
            using var meResponse = await client.SendAsync(meRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string cookieToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={cookieToken}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return request;
    }
}
