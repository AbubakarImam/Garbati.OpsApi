namespace Garbati.OpsApi.Contracts.Requests;

public record TransferRequest(
    string RequestedBy, string DebitAccountId, string CreditAccountId, decimal Amount, string Currency, string? Narration);
