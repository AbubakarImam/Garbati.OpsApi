namespace Garbati.OpsApi.Contracts.Requests;

// DESIGN.md Section 6 item 4 (testing console). IdempotencyKey is generated server-side, not accepted
// from the browser - see Clients/Contracts/Orchestrator/StartAccountOpeningRequest.cs.
public record StartAccountOpeningRequest(
    string FullName,
    string JurisdictionCode,
    string? CustomerReference,
    string? Email,
    string? PhoneNumber,
    string AccountName,
    string AccountType,
    string CurrencyCode);
