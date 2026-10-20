namespace Garbati.OpsApi.Contracts.Responses;

public record FundingResultResponse(string Reference, string Status, string? FailureReason);
