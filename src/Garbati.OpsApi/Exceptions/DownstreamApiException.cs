namespace Garbati.OpsApi.Exceptions;

// Thrown by any of the four downstream HTTP clients (StaffIdentityApi/IdentityApi/OrchestratorApi/
// LedgerApi) for a response this service does not know how to treat as a specific, named outcome. Carries
// enough to explain a failure without repeating the downstream service's raw response body (which could
// contain customer-identifying details ARCHITECTURE.md Section 5 says must never be logged). Mirrors
// Garbati.OrchestratorApi.Exceptions.DownstreamApiException exactly - same shape, same reasoning, now
// shared across four clients instead of two.
public class DownstreamApiException(string serviceName, int statusCode, string detail)
    : Exception($"{serviceName} returned {statusCode}: {detail}")
{
    public string ServiceName { get; } = serviceName;
    public int StatusCode { get; } = statusCode;
}
