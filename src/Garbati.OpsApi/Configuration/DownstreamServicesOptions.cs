using Microsoft.Extensions.Options;

namespace Garbati.OpsApi.Configuration;

// This service's own outbound credentials: the X-Api-Key value it presents to each of the four downstream
// services, and the base URL it calls them at. Deliberately separate from any inbound-auth configuration -
// OpsApi has no API-key-authenticated callers of its own in this version (task file point 4); this is only
// ever used as this service's own OUTGOING key. Same shape as Garbati.OrchestratorApi's
// DownstreamServicesOptions, extended from two services to four (task file: "extended to four clients
// instead of two").
public class DownstreamServiceOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public class DownstreamServicesOptions
{
    public DownstreamServiceOptions StaffIdentityApi { get; set; } = new();
    public DownstreamServiceOptions IdentityApi { get; set; } = new();
    public DownstreamServiceOptions OrchestratorApi { get; set; } = new();
    public DownstreamServiceOptions LedgerApi { get; set; } = new();
}

// Runs at startup (ValidateOnStart): a missing base URL or key would otherwise surface only the first time
// a request tries to use it, as a confusing failure deep inside some endpoint.
public class DownstreamServicesOptionsValidator : IValidateOptions<DownstreamServicesOptions>
{
    public ValidateOptionsResult Validate(string? name, DownstreamServicesOptions options)
    {
        var errors = new List<string>();

        Check(options.StaffIdentityApi, "DownstreamServices:StaffIdentityApi", errors);
        Check(options.IdentityApi, "DownstreamServices:IdentityApi", errors);
        Check(options.OrchestratorApi, "DownstreamServices:OrchestratorApi", errors);
        Check(options.LedgerApi, "DownstreamServices:LedgerApi", errors);

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void Check(DownstreamServiceOptions service, string prefix, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(service.BaseUrl) || !Uri.TryCreate(service.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add($"{prefix}:BaseUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(service.ApiKey))
        {
            errors.Add($"{prefix}:ApiKey is required.");
        }
    }
}
