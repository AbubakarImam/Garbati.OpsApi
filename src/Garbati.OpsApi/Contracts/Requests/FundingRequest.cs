namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md 3.4: a deposit, called directly against LedgerApi - no OrchestratorApi involvement.
public record FundingRequest(string CustomerAccountNumber, decimal Amount, string? Narration);
