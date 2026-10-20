using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Services.Interfaces;
using OrchestratorContracts = Garbati.OpsApi.Clients.Contracts.Orchestrator;
using LedgerContracts = Garbati.OpsApi.Clients.Contracts.Ledger;

namespace Garbati.OpsApi.Services.Implementations;

public class TestingConsoleService(IOrchestratorApiClient orchestratorApiClient, ILedgerApiClient ledgerApiClient)
    : ITestingConsoleService
{
    public async Task<AccountOpeningResponse> StartAccountOpeningAsync(
        string fullName, string jurisdictionCode, string? customerReference, string? email, string? phoneNumber,
        string accountName, string accountType, string currencyCode, CancellationToken cancellationToken)
    {
        var request = new OrchestratorContracts.StartAccountOpeningRequest(
            $"ops:{Guid.NewGuid():N}", fullName, jurisdictionCode, customerReference, email, phoneNumber,
            accountName, accountType, currencyCode);

        var response = await orchestratorApiClient.StartAccountOpeningAsync(request, cancellationToken);

        return new AccountOpeningResponse(
            response.Id, response.Status, response.CurrentStep, response.CustomerId, response.LedgerAccountId,
            response.FailureReason, response.CreatedAt, response.CompletedAt);
    }

    public async Task<TransferResultResponse> TransferAsync(
        string requestedBy, string debitAccountId, string creditAccountId, decimal amount, string currency,
        string? narration, CancellationToken cancellationToken)
    {
        var request = new OrchestratorContracts.TransferRequest(
            $"ops:{Guid.NewGuid():N}", requestedBy, debitAccountId, creditAccountId, amount, currency, narration);

        var response = await orchestratorApiClient.TransferAsync(request, cancellationToken);

        return new TransferResultResponse(response.DecisionId, response.Reference, response.Status, response.FailureReason);
    }

    public async Task<FundingResultResponse> FundAsync(
        string customerAccountNumber, decimal amount, string? narration, CancellationToken cancellationToken)
    {
        var request = new LedgerContracts.DepositRequest(
            customerAccountNumber, amount, narration, $"ops:{Guid.NewGuid():N}");

        var response = await ledgerApiClient.DepositAsync(request, cancellationToken);

        return new FundingResultResponse(response.Reference, response.Status, response.FailureReason);
    }
}
