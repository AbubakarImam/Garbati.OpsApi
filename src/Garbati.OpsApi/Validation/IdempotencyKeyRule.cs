using System.Text.RegularExpressions;

namespace Garbati.OpsApi.Validation;

// Same shape as every sibling service's IdempotencyKeyRule (LedgerApi/Garbati.IdentityApi/
// Garbati.OrchestratorApi): short enough to always fit a unique index downstream, limited to characters
// safe in logs and URLs. No reserved prefix here - unlike OrchestratorApi (which derives its own
// "orch:..." keys for an account-opening's internal steps), OpsApi doesn't mint any keys of its own that
// a caller could collide with in this build.
public static partial class IdempotencyKeyRule
{
    public const int MaxLength = 128;

    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,128}\z")]
    private static partial Regex Pattern();

    public static string? Validate(string? key) =>
        string.IsNullOrWhiteSpace(key) ? "IdempotencyKey is required."
        : !Pattern().IsMatch(key) ? $"IdempotencyKey must be 1-{MaxLength} characters: letters, digits, '-', '_', '.' or ':'."
        : null;
}
