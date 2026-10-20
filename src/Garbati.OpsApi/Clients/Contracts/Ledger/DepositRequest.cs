namespace Garbati.OpsApi.Clients.Contracts.Ledger;

// Mirrors LedgerApi.Contracts.Requests.DepositRequest. IdempotencyKey is forwarded from the browser's own
// request, prefixed with "ops:" for namespacing - fixed post-QA on PR #1 (T-033); see
// Orchestrator/StartAccountOpeningRequest.cs's comment for the full reasoning.
public record DepositRequest(string CustomerAccountNumber, decimal Amount, string? Narration, string IdempotencyKey);
