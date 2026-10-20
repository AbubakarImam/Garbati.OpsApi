using System.Net;
using System.Net.Http.Json;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

[Collection("Api")]
public class SessionTests(ApiFixture fixture)
{
    [Fact]
    public async Task Login_WithValidCredentials_SetsAWorkingSessionCookie()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        fixture.StaffIdentityApiClient.AuthenticateHandler = request =>
            request.Username == "ada" && request.Password == "correct-password"
                ? new AuthenticateResponse(staffId, "Ada Lovelace", "Teller")
                : null;

        using var client = fixture.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync(
            "/v1/ops/login", new LoginRequest("ada", "correct-password"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(staffId, body!.StaffId);
        Assert.Equal("Teller", body.Role);

        var cookie = CookieHelpers.ExtractCookiePair(loginResponse, OpsSessionCookie.Name);
        Assert.NotNull(cookie);

        // DESIGN.md Section 2.1: HttpOnly/Secure/SameSite=Strict.
        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookieHeader, StringComparison.OrdinalIgnoreCase);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        meRequest.Headers.Add("Cookie", cookie);
        using var meResponse = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(staffId, me!.StaffId);
        Assert.Equal("Ada Lovelace", me.FullName);
        Assert.Equal("Teller", me.Role);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401Unchanged()
    {
        // DESIGN.md Section 2.1: "returns StaffIdentityApi's own generic 401 unchanged" - the wrong
        // password, wrong username and suspended-staff cases are all this exact same response.
        fixture.StaffIdentityApiClient.AuthenticateHandler = _ => null;

        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/v1/ops/login", new LoginRequest("nobody", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Me_WithNoCookie_Returns401()
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/v1/ops/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAnUnknownCookieValue_Returns401()
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}=not-a-real-token");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithASessionPastIdleTimeout_Returns401()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        // LastActivityAt far enough in the past to exceed the 30-minute idle timeout, well inside the
        // 8-hour absolute timeout - isolates idle expiry from absolute expiry.
        var rawToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Idle Staff", "Teller", lastActivityAt: now.AddMinutes(-31), expiresAt: now.AddHours(7));

        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={rawToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithASessionPastAbsoluteTimeout_Returns401()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        // LastActivityAt is recent (would pass the idle check on its own), but ExpiresAt (the 8-hour
        // absolute maximum) is already in the past - isolates absolute expiry from idle expiry.
        var rawToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Stale Staff", "Teller", lastActivityAt: now.AddMinutes(-1), expiresAt: now.AddMinutes(-1));

        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        request.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={rawToken}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeletesTheSession_SoALaterMeCallWithTheSameCookieIs401()
    {
        var staffId = $"STF-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var rawToken = await fixture.CreateSessionDirectlyAsync(
            staffId, "Logout Staff", "Teller", lastActivityAt: now, expiresAt: now.AddHours(8));

        using var client = fixture.CreateClient();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/ops/logout");
        logoutRequest.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={rawToken}");
        using var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        Assert.Equal(0, await fixture.CountSessionsForStaffAsync(staffId));

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/ops/me");
        meRequest.Headers.Add("Cookie", $"{OpsSessionCookie.Name}={rawToken}");
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }
}
