using System.Security.Cryptography;
using System.Text;

namespace Garbati.OpsApi.Authorization;

// Identical convention to Garbati.OrchestratorApi.Authorization.ApiKeyHasher (itself identical to
// LedgerApi's/IdentityApi's/StaffIdentityApi's own ApiKeyHasher): the raw session token is 32
// cryptographically random bytes, so a plain SHA-256 is enough - slow hashes (bcrypt/PBKDF2) are for
// low-entropy secrets like passwords, not high-entropy random tokens. Unlike ApiKeyHasher (which checks a
// presented key against a small configured list, so needs a fixed-time comparison), a session lookup is
// an indexed exact match on the hash column (Data/OpsDbContext.cs) - hashing already destroys
// guessability, the same way a password-reset-token lookup works.
public static class SessionTokenHasher
{
    // Base64Url, no padding: safe to put directly in a cookie value with no escaping.
    public static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Stored as base64 (matching every other hash-only column on this platform).
    public static string Hash(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
