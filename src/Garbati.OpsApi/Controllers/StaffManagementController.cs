using System.Security.Claims;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// Aliased: Clients.Contracts.StaffIdentity and Contracts.Requests both define CreateStaffRequest/
// ChangeRoleRequest/ChangeStatusRequest/ChangeSupervisorRequest (the downstream shape vs. this service's
// own browser-facing shape - see the comment on each Contracts/Requests type). Only the response types
// are used from the downstream namespace here (this is a thin pass-through, DESIGN.md Section 6 item 5),
// so only those are aliased.
using StaffIdentityContracts = Garbati.OpsApi.Clients.Contracts.StaffIdentity;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 6 item 5 / Section 4: Administrator only. Thin pass-through to every one of
// StaffIdentityApi's 8 endpoints, except that ChangeRole/ChangeStatus additionally run cascade
// revocation (Section 2.3) via IStaffManagementService - that is this task's single most important
// behaviour, see Services/Implementations/StaffManagementService.cs.
[ApiController]
[Route("v1/ops/staff")]
[Authorize(Roles = OpsRoles.Administrator)]
public class StaffManagementController(IStaffManagementService staffManagementService) : ControllerBase
{
    private const int DefaultPageSize = 20;

    [HttpPost]
    public async Task<ActionResult<StaffIdentityContracts.CreateStaffResponse>> CreateStaff(
        [FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.CreateStaffAsync(
            request.FullName, request.Username, request.Role, CurrentStaffId(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{staffId}")]
    public async Task<ActionResult<StaffIdentityContracts.StaffResponse>> GetStaff(string staffId, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.GetStaffAsync(staffId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<StaffIdentityContracts.StaffListResponse>> ListStaff(
        [FromQuery] string? supervisorStaffId, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await staffManagementService.ListStaffAsync(supervisorStaffId, page, pageSize, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{staffId}/role")]
    public async Task<ActionResult<StaffIdentityContracts.StaffResponse>> ChangeRole(
        string staffId, [FromBody] ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.ChangeRoleAsync(staffId, request.Role, CurrentStaffId(), cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{staffId}/status")]
    public async Task<ActionResult<StaffIdentityContracts.StaffResponse>> ChangeStatus(
        string staffId, [FromBody] ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.ChangeStatusAsync(staffId, request.Status, CurrentStaffId(), cancellationToken);
        return Ok(response);
    }

    [HttpPatch("{staffId}/supervisor")]
    public async Task<ActionResult<StaffIdentityContracts.StaffResponse>> ChangeSupervisor(
        string staffId, [FromBody] ChangeSupervisorRequest request, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.ChangeSupervisorAsync(
            staffId, request.SupervisorStaffId, CurrentStaffId(), cancellationToken);
        return Ok(response);
    }

    [HttpPost("{staffId}/password")]
    public async Task<ActionResult<StaffIdentityContracts.ChangePasswordResponse>> ResetPassword(string staffId, CancellationToken cancellationToken)
    {
        var response = await staffManagementService.ResetPasswordAsync(staffId, CurrentStaffId(), cancellationToken);
        return Ok(response);
    }

    private string CurrentStaffId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
