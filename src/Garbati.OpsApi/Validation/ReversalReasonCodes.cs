namespace Garbati.OpsApi.Validation;

// Garbati.IdentityApi's own fixed vocabulary (Entities.ReversalReasonCode, confirmed directly against its
// real source before this was written) - reused as plain strings here, not a shared enum
// (ARCHITECTURE.md Section 5: no shared assembly between services). Task file: "validate against this
// exact list, reject anything else with 400" - checked case-sensitively against these exact uppercase
// values. IdentityApi's own ReversalAuthorizationService happens to parse case-insensitively, but OpsApi is
// the entry point into this vocabulary for a staff-initiated request, so it is the right place to hold
// callers to the canonical casing rather than silently pass through something IdentityApi would merely
// tolerate.
public static class ReversalReasonCodes
{
    public const string Duplicate = "DUPL";
    public const string Technical = "TECH";
    public const string Fraud = "FRAD";
    public const string CustomerRequested = "CUST";
    public const string Unauthorized = "UNAUTH";

    public static readonly IReadOnlyCollection<string> All =
        [Duplicate, Technical, Fraud, CustomerRequested, Unauthorized];

    public static bool IsValid(string? code) => code is not null && All.Contains(code);
}
