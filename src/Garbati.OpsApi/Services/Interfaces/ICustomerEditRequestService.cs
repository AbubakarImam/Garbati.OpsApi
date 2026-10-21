using Garbati.OpsApi.Entities;

namespace Garbati.OpsApi.Services.Interfaces;

public interface ICustomerEditRequestService
{
    Task<OpsCustomerEditRequest> CreateAsync(
        string targetCustomerId, string? proposedFullName, string? proposedEmail, string? proposedPhoneNumber,
        string requestedByStaffId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OpsCustomerEditRequest>> ListAsync(string staffId, string role, CancellationToken cancellationToken);

    Task<OpsCustomerEditRequest?> GetAsync(string id, string staffId, string role, CancellationToken cancellationToken);

    // Stops at Approved - never calls IdentityApi (task file, explicit; see CustomerEditRequestService's
    // own comment).
    Task<OpsCustomerEditRequest> ApproveAsync(
        string id, string approvingStaffId, string approvingRole, CancellationToken cancellationToken);

    Task<OpsCustomerEditRequest> RejectAsync(
        string id, string rejectingStaffId, string rejectingRole, string rejectionReason, CancellationToken cancellationToken);
}
