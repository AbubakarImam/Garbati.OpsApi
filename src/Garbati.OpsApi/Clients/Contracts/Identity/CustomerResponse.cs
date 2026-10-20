namespace Garbati.OpsApi.Clients.Contracts.Identity;

// Mirrors Garbati.IdentityApi.Contracts.Responses.CustomerResponse (the redacted view it already
// returns from GET /v1/customers/{id} - never raw CustomerIdentityData fields).
public record CustomerResponse(
    string CustomerId,
    string? CustomerReference,
    string CustomerStatus,
    string KycStatus,
    string? KycTier,
    string JurisdictionCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
