using System.Security.Claims;
using System.Text.Encodings.Web;
using Garbati.OpsApi.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Garbati.OpsApi.Configuration;

namespace Garbati.OpsApi.Authorization;

// This service's own inbound authentication (task file point 4: NOT the ApiKeyAuthenticationHandler
// pattern every sibling service uses for its own inbound callers - OpsApi's only caller is the browser,
// via the session cookie set by POST /v1/ops/login). Resolves the current OpsSession from the cookie,
// enforces both idle and absolute expiry (DESIGN.md Section 2.2, decision #1), and exposes
// {staffId, fullName, role} as claims - the "session-validating middleware/filter" the task file asks
// for, shared by every authenticated endpoint via the normal ASP.NET Core authentication pipeline rather
// than duplicated per action.
public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    OpsDbContext dbContext,
    IOptions<OpsSessionOptions> sessionOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OpsSession";

    // Claim carrying the OpsSession's own internal id (not StaffId) - POST /v1/ops/logout reads this to
    // delete exactly this row, rather than re-hashing the cookie a second time.
    public const string SessionIdClaimType = "ops:session_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(OpsSessionCookie.Name, out var rawToken) || string.IsNullOrEmpty(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        var tokenHash = SessionTokenHasher.Hash(rawToken);
        var session = await dbContext.OpsSessions.SingleOrDefaultAsync(s => s.SessionTokenHash == tokenHash);

        if (session is null)
        {
            Response.Cookies.Delete(OpsSessionCookie.Name, OpsSessionCookie.DeleteOptions());
            return AuthenticateResult.Fail("The session does not exist.");
        }

        var now = DateTimeOffset.UtcNow;
        var idleTimeout = TimeSpan.FromMinutes(sessionOptions.Value.IdleTimeoutMinutes);

        if (now >= session.ExpiresAt || now - session.LastActivityAt >= idleTimeout)
        {
            // Expired sessions are cleaned up as soon as they're seen, not left to linger - the same
            // "instant revocation" spirit as cascade revocation (DESIGN.md Section 2.3), just triggered
            // by time instead of a role/status change.
            dbContext.OpsSessions.Remove(session);
            await dbContext.SaveChangesAsync();
            Response.Cookies.Delete(OpsSessionCookie.Name, OpsSessionCookie.DeleteOptions());
            return AuthenticateResult.Fail("The session has expired.");
        }

        // Sliding idle window: every authenticated request extends LastActivityAt, but never ExpiresAt
        // (the absolute maximum, Section 2.2 decision #1, is deliberately not extended by activity).
        session.LastActivityAt = now;
        await dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.StaffId),
            new(ClaimTypes.Name, session.FullName),
            new(ClaimTypes.Role, session.Role),
            new(SessionIdClaimType, session.SessionId.ToString()),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        return WriteProblemAsync(StatusCodes.Status401Unauthorized, "A valid session is required.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status403Forbidden, "This role is not allowed to perform this action.");

    // Same ProblemDetails shape (with correlationId) as ExceptionHandlingMiddleware's errors.
    private Task WriteProblemAsync(int status, string title)
    {
        Response.StatusCode = status;
        return Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["correlationId"] = Context.TraceIdentifier }
            },
            options: null,
            contentType: "application/problem+json");
    }
}
