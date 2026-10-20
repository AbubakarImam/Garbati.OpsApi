namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

public record AccountOpeningResponse(
    string Id,
    string Status,
    string CurrentStep,
    string? CustomerId,
    string? LedgerAccountId,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
