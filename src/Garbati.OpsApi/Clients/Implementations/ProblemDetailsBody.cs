using System.Text.Json.Serialization;

namespace Garbati.OpsApi.Clients.Implementations;

// A minimal mirror of Microsoft.AspNetCore.Mvc.ProblemDetails for reading a response, not writing one.
// Identical to Garbati.OrchestratorApi.Clients.Implementations.ProblemDetailsBody - shared shape across
// every GARBATI service's ExceptionHandlingMiddleware output.
internal sealed class ProblemDetailsBody
{
    public string? Title { get; set; }
    public string? Detail { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extensions { get; set; }

    public string? ExtensionString(string key) =>
        Extensions is not null && Extensions.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
}
