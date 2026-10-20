using Garbati.OpsApi.Clients.Contracts.Ledger;

namespace Garbati.OpsApi.Clients.Interfaces;

// This service as a client of LedgerApi (DESIGN.md Section 3: scopes ledger.read, ledger.funding ONLY -
// deliberately no ledger.accounts/.transfer/.reverse/.admin, all of which stay exclusively behind
// OrchestratorApi or out of this service's reach entirely (3.4). This interface has no method that could
// call LedgerApi's account-creation, transfer, reversal or system-account-admin endpoints, so that
// boundary is enforced by what this type can even do, not only by configuration.
public interface ILedgerApiClient
{
    Task<AccountResponse?> GetAccountAsync(string accountNumber, CancellationToken cancellationToken);

    Task<BalanceResponse?> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken);

    // DESIGN.md 3.4: the one deliberate exception to "only the Orchestrator moves money" - a deposit at
    // a teller window, called directly, not through OrchestratorApi (which has no funding endpoint).
    Task<TransferResponse> DepositAsync(DepositRequest request, CancellationToken cancellationToken);
}
