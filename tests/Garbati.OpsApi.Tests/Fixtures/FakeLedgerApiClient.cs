using Garbati.OpsApi.Clients.Contracts.Ledger;
using Garbati.OpsApi.Clients.Interfaces;

namespace Garbati.OpsApi.Tests.Fixtures;

public class FakeLedgerApiClient : ILedgerApiClient
{
    public int GetAccountCallCount { get; private set; }
    public int GetBalanceCallCount { get; private set; }
    public int DepositCallCount { get; private set; }

    public string? LastGetAccountNumber { get; private set; }
    public string? LastGetBalanceAccountNumber { get; private set; }
    public DepositRequest? LastDepositRequest { get; private set; }

    public Func<string, AccountResponse?>? GetAccountHandler { get; set; }
    public Func<string, BalanceResponse?>? GetBalanceHandler { get; set; }
    public Func<DepositRequest, TransferResponse>? DepositHandler { get; set; }

    public Task<AccountResponse?> GetAccountAsync(string accountNumber, CancellationToken cancellationToken)
    {
        GetAccountCallCount++;
        LastGetAccountNumber = accountNumber;
        var handler = GetAccountHandler ?? (_ => null);
        return Task.FromResult(handler(accountNumber));
    }

    public Task<BalanceResponse?> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken)
    {
        GetBalanceCallCount++;
        LastGetBalanceAccountNumber = accountNumber;
        var handler = GetBalanceHandler ?? (_ => null);
        return Task.FromResult(handler(accountNumber));
    }

    public Task<TransferResponse> DepositAsync(DepositRequest request, CancellationToken cancellationToken)
    {
        DepositCallCount++;
        LastDepositRequest = request;
        var handler = DepositHandler ?? (r => new TransferResponse($"TXN-FAKE{DepositCallCount:D7}", "Posted"));
        return Task.FromResult(handler(request));
    }
}
