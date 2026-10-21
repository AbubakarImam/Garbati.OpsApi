using Garbati.OpsApi.Entities;

namespace Garbati.OpsApi.Services.Interfaces;

public interface IReversalRequestService
{
    Task<OpsReversalRequest> CreateAsync(
        string idempotencyKey, string originalTransactionReference, decimal amount, string currency,
        string reasonCode, string requestedByStaffId, CancellationToken cancellationToken);

    // staffId/role are the CALLING session's own, read fresh from its claims by the controller - see
    // ReversalRequestService's own comment on why this is what makes the visibility rules and the hard
    // maker-checker rule "live", not cached.
    Task<IReadOnlyList<OpsReversalRequest>> ListAsync(string staffId, string role, CancellationToken cancellationToken);

    Task<OpsReversalRequest?> GetAsync(string id, string staffId, string role, CancellationToken cancellationToken);

    Task<OpsReversalRequest> ApproveAsync(
        string id, string approvingStaffId, string approvingRole, CancellationToken cancellationToken);

    Task<OpsReversalRequest> RejectAsync(
        string id, string rejectingStaffId, string rejectingRole, string rejectionReason, CancellationToken cancellationToken);
}
