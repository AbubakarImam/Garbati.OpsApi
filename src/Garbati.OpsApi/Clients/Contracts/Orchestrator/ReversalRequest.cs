namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

// Mirrors Garbati.OrchestratorApi.Contracts.Requests.ReversalRequest exactly - read directly against its
// real source (T-034's task file instruction, not guessed) before this was written: IdempotencyKey,
// RequestedBy, OriginalTransactionReference, Amount, Currency, ReasonCode, in that order.
// garbati-platform T-030 built and live-verified the real OrchestratorApi POST /v1/reversals endpoint this
// calls.
public record ReversalRequest(
    string IdempotencyKey,
    string RequestedBy,
    string OriginalTransactionReference,
    decimal Amount,
    string Currency,
    string ReasonCode);
