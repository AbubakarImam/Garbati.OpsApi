namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md 3.4: a deposit, called directly against LedgerApi - no OrchestratorApi involvement.
// QA review of PR #1 (T-033): IdempotencyKey must be client-supplied - see
// StartAccountOpeningRequest.cs's comment for the full reasoning (same fix, same cause).
public record FundingRequest(string CustomerAccountNumber, decimal Amount, string? Narration, string IdempotencyKey);
