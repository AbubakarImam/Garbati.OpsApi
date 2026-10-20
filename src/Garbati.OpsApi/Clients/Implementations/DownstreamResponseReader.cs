using System.Net;
using System.Text.Json;

namespace Garbati.OpsApi.Clients.Implementations;

// Shared response-reading for all four downstream clients. Every GARBATI service follows
// ARCHITECTURE.md Section 5's error mapping, but with one real inconsistency confirmed by reading the
// live controllers of StaffIdentityApi/IdentityApi/OrchestratorApi/LedgerApi (not assumed, same as
// Garbati.OrchestratorApi's own client code this mirrors): a 400 body is a plain JSON array of strings
// (`BadRequest(errors)` where errors is `List<string>`), while every other error status is
// application/problem+json. ReadProblemAsync handles both without the callers needing to know which.
internal static class DownstreamResponseReader
{
    // Every sibling's ProblemDetails body uses camelCase JSON property names ("detail", "correlationId",
    // ...) against this type's PascalCase properties - case-insensitive matching is required, not optional.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<ProblemDetailsBody?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errors = JsonSerializer.Deserialize<List<string>>(body, JsonOptions);
                return errors is { Count: > 0 } ? new ProblemDetailsBody { Detail = string.Join("; ", errors) } : null;
            }

            return JsonSerializer.Deserialize<ProblemDetailsBody>(body, JsonOptions);
        }
        catch (JsonException)
        {
            // Not JSON at all (e.g. an upstream proxy's HTML error page) - fall back to the raw text.
            return new ProblemDetailsBody { Detail = body };
        }
    }

    public static string DescribeFailure(ProblemDetailsBody? problem, HttpResponseMessage response) =>
        problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? "(no detail)";
}
