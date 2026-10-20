namespace Garbati.OpsApi.Contracts.Requests;

// QA review of PR #1 (T-033): IdempotencyKey must be client-supplied - see
// StartAccountOpeningRequest.cs's comment for the full reasoning (same fix, same cause).
public record TransferRequest(
    string IdempotencyKey, string RequestedBy, string DebitAccountId, string CreditAccountId, decimal Amount, string Currency, string? Narration);
