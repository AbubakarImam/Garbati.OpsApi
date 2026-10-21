using System.Security.Claims;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Entities;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 4.1 (garbati-platform T-034): unlike reversals, ONLY CustomerServiceOfficer (own
// requests) and Supervisor (everything) have any access to this endpoint group at all - every other
// role, including Administrator, gets 403 at this class-level gate
// (OpsRoles.CustomerServiceOfficerOrSupervisor). This workflow STOPS at Approved - see
// Services/Implementations/CustomerEditRequestService.cs's own comment for why no action here ever calls
// IdentityApi.
[ApiController]
[Route("v1/ops/customer-edits")]
[Authorize(Roles = OpsRoles.CustomerServiceOfficerOrSupervisor)]
public class CustomerEditRequestsController(ICustomerEditRequestService customerEditRequestService) : ControllerBase
{
    [Authorize(Roles = OpsRoles.CustomerServiceOfficer)]
    [HttpPost]
    public async Task<ActionResult<CustomerEditRequestResponse>> Create(
        [FromBody] CreateCustomerEditRequestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetCustomerId))
        {
            return BadRequest("TargetCustomerId is required.");
        }

        if (request.ProposedFullName is null && request.ProposedEmail is null && request.ProposedPhoneNumber is null)
        {
            return BadRequest("At least one of ProposedFullName, ProposedEmail or ProposedPhoneNumber is required.");
        }

        var entity = await customerEditRequestService.CreateAsync(
            request.TargetCustomerId, request.ProposedFullName, request.ProposedEmail, request.ProposedPhoneNumber,
            CurrentStaffId(), cancellationToken);

        return Ok(Map(entity));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerEditRequestResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await customerEditRequestService.ListAsync(CurrentStaffId(), CurrentRole(), cancellationToken);
        return Ok(items.Select(Map).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerEditRequestResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var entity = await customerEditRequestService.GetAsync(id, CurrentStaffId(), CurrentRole(), cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [Authorize(Roles = OpsRoles.Supervisor)]
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<CustomerEditRequestResponse>> Approve(string id, CancellationToken cancellationToken)
    {
        var entity = await customerEditRequestService.ApproveAsync(id, CurrentStaffId(), CurrentRole(), cancellationToken);
        return Ok(Map(entity));
    }

    [Authorize(Roles = OpsRoles.Supervisor)]
    [HttpPost("{id}/reject")]
    public async Task<ActionResult<CustomerEditRequestResponse>> Reject(
        string id, [FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return BadRequest("RejectionReason is required.");
        }

        var entity = await customerEditRequestService.RejectAsync(
            id, CurrentStaffId(), CurrentRole(), request.RejectionReason, cancellationToken);

        return Ok(Map(entity));
    }

    private string CurrentStaffId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentRole() => User.FindFirstValue(ClaimTypes.Role)!;

    private static CustomerEditRequestResponse Map(OpsCustomerEditRequest r) => new(
        r.Id, r.TargetCustomerId, r.ProposedFullName, r.ProposedEmail, r.ProposedPhoneNumber, r.RequestedByStaffId,
        r.Status, r.ApprovedByStaffId, r.RejectionReason, r.RequestedAt, r.DecidedAt);
}
