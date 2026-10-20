namespace Garbati.OpsApi.Clients.Contracts.Ledger;

// Mirrors LedgerApi.Contracts.Responses.BalanceResponse.
public record BalanceResponse(
    string AccountNumber,
    decimal Balance,
    string CurrencyCode,
    DateTimeOffset AsOf);
