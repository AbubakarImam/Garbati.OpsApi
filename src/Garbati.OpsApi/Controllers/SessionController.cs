using System.Security.Claims;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 2.1/2.1a. Route literals match the versioned paths the design doc spells out
// exactly (ARCHITECTURE.md Section 5: "/v1/" in the path).
[ApiController]
[Route("v1/ops")]
public class SessionController(ISessionService sessionService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<MeResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sessionService.LoginAsync(request.Username, request.Password, cancellationToken);

        // DESIGN.md Section 2.1: "returns StaffIdentityApi's own generic 401 unchanged - no additional
        // information added or removed." A null result IS that 401, propagated unchanged (task file:
        // "On failure, propagate StaffIdentityApi's generic 401 unchanged").
        if (result is null)
        {
            return Unauthorized();
        }

        Response.Cookies.Append(OpsSessionCookie.Name, result.RawToken, OpsSessionCookie.Options(result.ExpiresAt));
        return Ok(new MeResponse(result.StaffId, result.FullName, result.Role));
    }

    // DESIGN.md Section 2.1a: "{staffId, fullName, role} from the current session - no downstream call."
    // SessionAuthenticationHandler has already resolved and validated the session before this action
    // runs; the claims are read back, not re-fetched.
    [Authorize]
    [HttpGet("me")]
    public ActionResult<MeResponse> Me()
    {
        var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fullName = User.FindFirstValue(ClaimTypes.Name)!;
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        return Ok(new MeResponse(staffId, fullName, role));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = long.Parse(User.FindFirstValue(SessionAuthenticationHandler.SessionIdClaimType)!);
        await sessionService.LogoutAsync(sessionId, cancellationToken);
        Response.Cookies.Delete(OpsSessionCookie.Name, OpsSessionCookie.DeleteOptions());
        return NoContent();
    }
}
