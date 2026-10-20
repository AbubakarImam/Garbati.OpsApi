using Garbati.OpsApi.Contracts.Responses;

namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 6 item 4 / 3.4. Account opening and transfer go through OrchestratorApi; funding
// goes directly to LedgerApi (the one deliberate exception). IdempotencyKey is client-supplied (fixed
// post-QA on PR #1, T-033 - see Contracts.Requests.StartAccountOpeningRequest's comment), forwarded to
// each downstream call prefixed with "ops:" for namespacing.
public interface ITestingConsoleService
{
    Task<AccountOpeningResponse> StartAccountOpeningAsync(
        string idempotencyKey, string fullName, string jurisdictionCode, string? customerReference, string? email,
        string? phoneNumber, string accountName, string accountType, string currencyCode, CancellationToken cancellationToken);

    Task<TransferResultResponse> TransferAsync(
        string idempotencyKey, string requestedBy, string debitAccountId, string creditAccountId, decimal amount,
        string currency, string? narration, CancellationToken cancellationToken);

    Task<FundingResultResponse> FundAsync(
        string idempotencyKey, string customerAccountNumber, decimal amount, string? narration, CancellationToken cancellationToken);
}
