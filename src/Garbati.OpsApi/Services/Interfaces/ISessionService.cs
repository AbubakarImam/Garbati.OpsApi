namespace Garbati.OpsApi.Services.Interfaces;

// DESIGN.md Section 2.1/2.1a/2.3. The one place OpsSession rows are created, deleted one at a time
// (logout) or deleted in bulk for a staff member (cascade revocation, decision #2).
public interface ISessionService
{
    // Null = StaffIdentityApi rejected the credentials (wrong username/password, or Status != Active) -
    // the controller propagates that as the same generic 401, per the task file.
    Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken);

    Task LogoutAsync(long sessionId, CancellationToken cancellationToken);

    // DESIGN.md Section 2.3, decision #2: deletes every OpsSession row for this StaffId. Called by
    // IStaffManagementService in the same request as a role/status change, before it returns success.
    Task RevokeAllSessionsForStaffAsync(string staffId, CancellationToken cancellationToken);
}

public record LoginResult(string RawToken, string StaffId, string FullName, string Role, DateTimeOffset ExpiresAt);
