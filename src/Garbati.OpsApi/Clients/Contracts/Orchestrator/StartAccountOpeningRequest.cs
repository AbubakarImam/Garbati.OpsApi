namespace Garbati.OpsApi.Clients.Contracts.Orchestrator;

// Mirrors Garbati.OrchestratorApi.Contracts.Requests.StartAccountOpeningRequest. IdempotencyKey is
// forwarded from the browser's own request (Contracts.Requests.StartAccountOpeningRequest), prefixed with
// "ops:" for namespacing - fixed post-QA on PR #1 (T-033); a server-generated key per call defeated
// retry-safety entirely.
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
