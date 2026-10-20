namespace Garbati.OpsApi.Contracts.Responses;

// DESIGN.md Section 6 item 1: "combining an IdentityApi customer read and a LedgerApi account/balance
// read into one response - the frontend shouldn't have to make two calls itself." Account fields are null
// when the caller did not supply an accountNumber to look up (a customer may be found with no account
// number known yet, e.g. right after account opening before the staff member has it on hand).
public record CustomerLookupResponse(
    string CustomerId,
    string? CustomerReference,
    string CustomerStatus,
    string KycStatus,
    string? KycTier,
    string JurisdictionCode,
    string? AccountNumber,
    string? AccountName,
    string? AccountType,
    string? AccountStatus,
    string? CurrencyCode,
    decimal? Balance);
