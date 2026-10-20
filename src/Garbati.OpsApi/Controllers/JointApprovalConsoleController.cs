using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.Identity;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 6 item 6 / Section 4: Supervisor can act, Auditor is read-only - enforced per action
// below (the class-level attribute covers both roles for the read endpoints; Approve narrows to
// Supervisor only), not left to the UI.
[ApiController]
[Route("v1/ops/joint-approvals")]
[Authorize(Roles = OpsRoles.SupervisorOrAuditor)]
public class JointApprovalConsoleController(IJointApprovalConsoleService jointApprovalConsoleService) : ControllerBase
{
    private const int DefaultPageSize = 20;

    [HttpGet]
    public async Task<ActionResult<PagedResponse<PendingApprovalRequestResponse>>> ListPending(
        [FromQuery] int pageSize = DefaultPageSize, [FromQuery] string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var response = await jointApprovalConsoleService.ListPendingAsync(pageSize, pageToken, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{requestId}")]
    public async Task<ActionResult<PendingApprovalRequestResponse>> Get(string requestId, CancellationToken cancellationToken)
    {
        var response = await jointApprovalConsoleService.GetAsync(requestId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    // DESIGN.md Section 4: Auditor is explicitly read-only here - narrower than the class-level
    // [Authorize] above, which is why this action carries its own [Authorize(Roles = Supervisor)]
    // instead of relying on the controller-level attribute (ASP.NET Core's own documented behaviour: an
    // action-level [Authorize] REPLACES the controller-level one for that action, it does not add to it -
    // so Auditor is correctly excluded here even though it is allowed on every other action in this
    // controller).
    [Authorize(Roles = OpsRoles.Supervisor)]
    [HttpPost("{requestId}/approve")]
    public async Task<ActionResult<PendingApprovalRequestResponse>> Approve(
        string requestId, [FromBody] ApproveJointRequestRequest request, CancellationToken cancellationToken)
    {
        var response = await jointApprovalConsoleService.ApproveAsync(requestId, request.CustomerId, cancellationToken);
        return Ok(response);
    }
}
