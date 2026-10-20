using System.Net;
using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Clients.Implementations;

namespace Garbati.OpsApi.Tests.Clients;

public class StaffIdentityApiClientTests
{
    private static StaffIdentityApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://staffidentity.invalid") });

    [Fact]
    public async Task AuthenticateAsync_OnA401_ReturnsNull()
    {
        // DESIGN.md Section 2.1: wrong username/password/suspended are all the same generic 401, with no
        // body - this client must surface that as null (task file: "propagate StaffIdentityApi's generic
        // 401 unchanged"), not throw.
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, jsonBody: null);
        var client = CreateClient(handler);

        var response = await client.AuthenticateAsync(new AuthenticateRequest("alice", "wrong"), CancellationToken.None);

        Assert.Null(response);
        Assert.Equal("POST", handler.LastRequest!.Method.Method);
        Assert.Equal("http://staffidentity.invalid/v1/staff/authenticate", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task AuthenticateAsync_OnSuccess_ParsesTheResponse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"staffId":"STF-0000000001","fullName":"Ada Lovelace","role":"Teller"}""");
        var client = CreateClient(handler);

        var response = await client.AuthenticateAsync(new AuthenticateRequest("ada", "correct"), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("STF-0000000001", response.StaffId);
        Assert.Equal("Teller", response.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_SendsTheExpectedRouteAndBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
            """{"staffId":"STF-1","fullName":"Bob","username":"bob","role":"Supervisor","status":"Active","mustChangePassword":false,"supervisorStaffId":null,"createdAt":"2026-10-20T00:00:00Z","createdByStaffId":"STF-ADMIN","updatedAt":"2026-10-20T00:00:00Z"}""");
        var client = CreateClient(handler);

        await client.ChangeRoleAsync("STF-1", new ChangeRoleRequest("STF-ADMIN", "Supervisor"), CancellationToken.None);

        Assert.Equal("PATCH", handler.LastRequest!.Method.Method);
        Assert.Equal("http://staffidentity.invalid/v1/staff/STF-1/role", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("""{"actingStaffId":"STF-ADMIN","role":"Supervisor"}""", handler.LastRequestBody);
    }
}
