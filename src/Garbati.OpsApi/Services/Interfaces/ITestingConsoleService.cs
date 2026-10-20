using Garbati.OpsApi.Contracts.Responses;

namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 6 item 4 / 3.4. Account opening and transfer go through OrchestratorApi; funding
// goes directly to LedgerApi (the one deliberate exception). IdempotencyKey for every downstream call is
// generated here, server-side, never accepted from the browser.
public interface ITestingConsoleService
{
    Task<AccountOpeningResponse> StartAccountOpeningAsync(
        string fullName, string jurisdictionCode, string? customerReference, string? email, string? phoneNumber,
        string accountName, string accountType, string currencyCode, CancellationToken cancellationToken);

    Task<TransferResultResponse> TransferAsync(
        string requestedBy, string debitAccountId, string creditAccountId, decimal amount, string currency,
        string? narration, CancellationToken cancellationToken);

    Task<FundingResultResponse> FundAsync(
        string customerAccountNumber, decimal amount, string? narration, CancellationToken cancellationToken);
}
