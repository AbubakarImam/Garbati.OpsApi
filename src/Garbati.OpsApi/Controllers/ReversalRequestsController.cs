using System.Security.Claims;
using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Entities;
using Garbati.OpsApi.Services.Interfaces;
using Garbati.OpsApi.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 5 / Section 4 (garbati-platform T-034): the roles that may call this endpoint group at
// all (OpsRoles.ReversalQueueRoles, the class-level gate) are wider than what each of them is actually
// allowed to SEE - IReversalRequestService applies the real per-role filtering (task file, explicit:
// "enforce this as application-layer filtering inside the service, not just a single
// [Authorize(Roles=...)] gate").
[ApiController]
[Route("v1/ops/reversals")]
[Authorize(Roles = OpsRoles.ReversalQueueRoles)]
public class ReversalRequestsController(IReversalRequestService reversalRequestService) : ControllerBase
{
    // Narrower than the class-level gate - an action-level [Authorize] REPLACES it, not adds to it (same
    // documented ASP.NET Core behaviour JointApprovalConsoleController's own Approve action already
    // relies on), so ComplianceOfficer/RiskOfficer/Auditor are correctly excluded here even though they
    // may reach every other action in this controller.
    [Authorize(Roles = OpsRoles.TellerOrCustomerServiceOfficer)]
    [HttpPost]
    public async Task<ActionResult<ReversalRequestResponse>> Create(
        [FromBody] CreateReversalRequestRequest request, CancellationToken cancellationToken)
    {
        if (IdempotencyKeyRule.Validate(request.IdempotencyKey) is { } idempotencyError)
        {
            return BadRequest(idempotencyError);
        }

        if (!ReversalReasonCodes.IsValid(request.ReasonCode))
        {
            return BadRequest($"ReasonCode must be one of: {string.Join(", ", ReversalReasonCodes.All)}.");
        }

        if (string.IsNullOrWhiteSpace(request.OriginalTransactionReference))
        {
            return BadRequest("OriginalTransactionReference is required.");
        }

        if (request.Amount <= 0m)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
        {
            return BadRequest("Currency must be a 3-letter currency code.");
        }

        var entity = await reversalRequestService.CreateAsync(
            request.IdempotencyKey, request.OriginalTransactionReference, request.Amount, request.Currency,
            request.ReasonCode, CurrentStaffId(), cancellationToken);

        return Ok(Map(entity));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReversalRequestResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await reversalRequestService.ListAsync(CurrentStaffId(), CurrentRole(), cancellationToken);
        return Ok(items.Select(Map).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReversalRequestResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var entity = await reversalRequestService.GetAsync(id, CurrentStaffId(), CurrentRole(), cancellationToken);
        return entity is null ? NotFound() : Ok(Map(entity));
    }

    [Authorize(Roles = OpsRoles.Supervisor)]
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ReversalRequestResponse>> Approve(string id, CancellationToken cancellationToken)
    {
        var entity = await reversalRequestService.ApproveAsync(id, CurrentStaffId(), CurrentRole(), cancellationToken);
        var response = Map(entity);

        // Mirrors every other business-outcome response on this platform (TestingConsoleController,
        // OrchestratorApi's own TransfersController/ReversalsController): the approve ACTION itself always
        // succeeds here (the decision was validly recorded) - but ExecutionFailed is still a business-rule
        // failure, returned with 422, not a fabricated 200.
        return response.Status == OpsReversalRequestStatus.ExecutionFailed ? UnprocessableEntity(response) : Ok(response);
    }

    [Authorize(Roles = OpsRoles.Supervisor)]
    [HttpPost("{id}/reject")]
    public async Task<ActionResult<ReversalRequestResponse>> Reject(
        string id, [FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return BadRequest("RejectionReason is required.");
        }

        var entity = await reversalRequestService.RejectAsync(
            id, CurrentStaffId(), CurrentRole(), request.RejectionReason, cancellationToken);

        return Ok(Map(entity));
    }

    // Read fresh from the current request's own claims (SessionAuthenticationHandler resolves them from
    // the database on every request) - never from anything the caller supplies in a request body. This is
    // what makes the hard maker-checker rule (ReversalRequestService.EnsureMakerChecker) "live", not a
    // value cached from an earlier page load.
    private string CurrentStaffId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentRole() => User.FindFirstValue(ClaimTypes.Role)!;

    private static ReversalRequestResponse Map(OpsReversalRequest r) => new(
        r.Id, r.OriginalTransactionReference, r.Amount, r.Currency, r.ReasonCode, r.RequestedByStaffId,
        r.Status, r.ApprovedByStaffId, r.RejectionReason, r.RequestedAt, r.DecidedAt, r.ExecutedReference, r.ExecutionDetail);
}
