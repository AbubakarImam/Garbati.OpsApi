using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 6 item 1 / Section 4: every role may use this (read-only).
[ApiController]
[Route("v1/ops/customers")]
public class CustomerLookupController(ICustomerLookupService lookupService) : ControllerBase
{
    [Authorize(Roles = OpsRoles.AnyRole)]
    [HttpGet("{customerId}")]
    public async Task<ActionResult<CustomerLookupResponse>> GetCustomer(
        string customerId, [FromQuery] string? accountNumber, CancellationToken cancellationToken)
    {
        var result = await lookupService.LookupAsync(customerId, accountNumber, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
