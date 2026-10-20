namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

public record TransferResultResponse(
    string DecisionId,
    string Reference,
    string Status,
    string? FailureReason);
