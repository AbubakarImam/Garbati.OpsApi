namespace Garbati.OpsApi.Clients.Contracts.Identity;

// Mirrors Garbati.IdentityApi.Contracts.Responses.PagedResponse<T> (T-031). NextPageToken is opaque -
// this service never parses or constructs it, only forwards whatever IdentityApi sent.
public record PagedResponse<T>(IReadOnlyList<T> Items, string? NextPageToken);
