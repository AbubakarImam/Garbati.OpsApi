namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 6 item 4 (testing console). QA review of PR #1 (T-033): IdempotencyKey must be
// client-supplied, not server-generated per call - a browser retry (timeout, double-click) with a fresh
// server-generated key would be treated as a brand-new operation downstream, exactly the scenario
// idempotency keys exist to prevent. The browser generates one id per logical attempt and resends the
// same value on a retry of that same attempt - see Validation/IdempotencyKeyRule.cs.
public record StartAccountOpeningRequest(
    string IdempotencyKey,
    string FullName,
    string JurisdictionCode,
    string? CustomerReference,
    string? Email,
    string? PhoneNumber,
    string AccountName,
    string AccountType,
    string CurrencyCode);
