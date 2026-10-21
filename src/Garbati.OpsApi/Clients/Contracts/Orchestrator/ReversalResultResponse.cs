namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

// Mirrors Garbati.OrchestratorApi.Contracts.Responses.ReversalResultResponse exactly - read directly
// against its real source before this was written. Status follows the same Success/Failed vocabulary
// TransferResultResponse already uses on this client.
public record ReversalResultResponse(
    string DecisionId,
    string Reference,
    string Status,
    string? FailureReason);
