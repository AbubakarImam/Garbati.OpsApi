using Garbati.OpsApi.Clients.Contracts.Orchestrator;

namespace Garbati.OpsApi.Clients.Interfaces;

// This service as a client of OrchestratorApi (DESIGN.md Section 3: scopes orchestrator.account-openings,
// orchestrator.transfers, orchestrator.reversals). ReverseAsync is T-034's addition - on an
// OpsReversalRequest's Approved transition, ReversalRequestService calls this (DESIGN.md 5.3/5.4): the
// real, live-verified OrchestratorApi POST /v1/reversals (garbati-platform T-030), not IdentityApi
// directly - OpsApi never holds identity.ops.reversal (that scope belongs to OrchestratorApi instead).
public interface IOrchestratorApiClient
{
    Task<AccountOpeningResponse> StartAccountOpeningAsync(
        StartAccountOpeningRequest request, CancellationToken cancellationToken);

    Task<TransferResultResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken);

    Task<ReversalResultResponse> ReverseAsync(ReversalRequest request, CancellationToken cancellationToken);
}
