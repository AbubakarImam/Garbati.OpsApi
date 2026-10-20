using Garbati.OpsApi.Contracts.Responses;

namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 6 item 1: combines an IdentityApi customer read with an optional LedgerApi
// account/balance read into one response.
public interface ICustomerLookupService
{
    // Null = no such customer in IdentityApi. accountNumber is optional - a customer may be looked up
    // before any ledger account number is known.
    Task<CustomerLookupResponse?> LookupAsync(string customerId, string? accountNumber, CancellationToken cancellationToken);
}
