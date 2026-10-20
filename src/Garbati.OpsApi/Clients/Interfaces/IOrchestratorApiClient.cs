using Garbati.OpsApi.Clients.Contracts.Orchestrator;

namespace Garbati.OpsApi.Clients.Interfaces;

// This service as a client of OrchestratorApi (DESIGN.md Section 3: scopes orchestrator.account-openings,
// orchestrator.transfers). Deliberately no reversal method - that is T-034's job entirely (task file:
// "this service doesn't call OrchestratorApi's reversal endpoint from this task at all"), and
// orchestrator.reversals is not among this client's granted scopes.
public interface IOrchestratorApiClient
{
    Task<AccountOpeningResponse> StartAccountOpeningAsync(
        StartAccountOpeningRequest request, CancellationToken cancellationToken);

    Task<TransferResultResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken);
}
