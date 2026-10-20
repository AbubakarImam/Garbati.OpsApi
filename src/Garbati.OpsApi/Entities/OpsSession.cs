namespace Garbati.OpsApi.Entities;

// DESIGN.md Section 2.2. The browser's cookie carries only the opaque raw token (Authorization/
// SessionAuthenticationHandler.cs issues/validates it); the raw token itself is never persisted here -
// only its SHA-256 hash, the same hash-only convention this platform already uses for API keys
// (ARCHITECTURE.md Section 5, Authorization/SessionTokenHasher.cs). A database leak alone cannot be used
// to forge a session.
public class OpsSession
{
    public long SessionId { get; set; }

    public string SessionTokenHash { get; set; } = null!;

    // Captured at login time (DESIGN.md Section 2.3): cached for the session's lifetime rather than
    // re-fetched from StaffIdentityApi per request. Role/status changes reach this table only via
    // cascade revocation (Section 2.3, decision #2), never by this row going stale while active.
    public string StaffId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
