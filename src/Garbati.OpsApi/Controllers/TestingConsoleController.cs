using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Contracts.Requests;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garbati.OpsApi.Controllers;

// DESIGN.md Section 6 item 4 / Section 4: Teller or Supervisor only.
[ApiController]
[Route("v1/ops/console")]
[Authorize(Roles = OpsRoles.TellerOrSupervisor)]
public class TestingConsoleController(ITestingConsoleService testingConsoleService) : ControllerBase
{
    [HttpPost("account-openings")]
    public async Task<ActionResult<AccountOpeningResponse>> StartAccountOpening(
        [FromBody] StartAccountOpeningRequest request, CancellationToken cancellationToken)
    {
        var response = await testingConsoleService.StartAccountOpeningAsync(
            request.FullName, request.JurisdictionCode, request.CustomerReference, request.Email,
            request.PhoneNumber, request.AccountName, request.AccountType, request.CurrencyCode, cancellationToken);

        return response.Status == "Failed" ? UnprocessableEntity(response) : Ok(response);
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<TransferResultResponse>> Transfer(
        [FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        var response = await testingConsoleService.TransferAsync(
            request.RequestedBy, request.DebitAccountId, request.CreditAccountId, request.Amount,
            request.Currency, request.Narration, cancellationToken);

        return response.Status == "Failed" ? UnprocessableEntity(response) : Ok(response);
    }

    [HttpPost("funding")]
    public async Task<ActionResult<FundingResultResponse>> Fund(
        [FromBody] FundingRequest request, CancellationToken cancellationToken)
    {
        var response = await testingConsoleService.FundAsync(
            request.CustomerAccountNumber, request.Amount, request.Narration, cancellationToken);

        return response.Status == "Failed" ? UnprocessableEntity(response) : Ok(response);
    }
}
