namespace Garbati.OpsApi.Contracts.Responses;

public record TransferResultResponse(string DecisionId, string Reference, string Status, string? FailureReason);
