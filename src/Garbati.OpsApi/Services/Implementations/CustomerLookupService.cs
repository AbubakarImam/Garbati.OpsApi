using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Contracts.Responses;
using Garbati.OpsApi.Services.Interfaces;

namespace Garbati.OpsApi.Services.Implementations;

public class CustomerLookupService(IIdentityApiClient identityApiClient, ILedgerApiClient ledgerApiClient)
    : ICustomerLookupService
{
    public async Task<CustomerLookupResponse?> LookupAsync(
        string customerId, string? accountNumber, CancellationToken cancellationToken)
    {
        var customer = await identityApiClient.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        string? resolvedAccountNumber = null, accountName = null, accountType = null, accountStatus = null, currencyCode = null;
        decimal? balance = null;

        // A missing/unknown account number is not treated as an error for the overall lookup - the
        // customer half is still useful on its own (DESIGN.md Section 6 item 1 frames this as combining
        // two reads "into one response", not as requiring both to succeed).
        if (!string.IsNullOrWhiteSpace(accountNumber))
        {
            var account = await ledgerApiClient.GetAccountAsync(accountNumber, cancellationToken);
            if (account is not null)
            {
                resolvedAccountNumber = account.AccountNumber;
                accountName = account.AccountName;
                accountType = account.AccountType;
                accountStatus = account.Status;
                currencyCode = account.CurrencyCode;

                var balanceResponse = await ledgerApiClient.GetBalanceAsync(accountNumber, cancellationToken);
                balance = balanceResponse?.Balance;
            }
        }

        return new CustomerLookupResponse(
            customer.CustomerId,
            customer.CustomerReference,
            customer.CustomerStatus,
            customer.KycStatus,
            customer.KycTier,
            customer.JurisdictionCode,
            resolvedAccountNumber,
            accountName,
            accountType,
            accountStatus,
            currencyCode,
            balance);
    }
}
