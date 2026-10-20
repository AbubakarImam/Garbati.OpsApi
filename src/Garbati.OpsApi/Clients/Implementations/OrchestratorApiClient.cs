using Garbati.OpsApi.Clients.Contracts.Orchestrator;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Exceptions;

namespace Garbati.OpsApi.Clients.Implementations;

// Talks to OrchestratorApi over HTTP. Routes confirmed against Garbati.OrchestratorApi.Controllers.
// AccountOpeningsController/TransfersController's live [Route] attributes. A "Failed" result (422) is a
// business-rule failure, not an error this client throws on - OrchestratorApi's own controllers return
// the failed result with 200/422 interchangeably depending on Status, so this client reads the body on
// either status and lets the caller (OpsApi's own controller) decide what to do with Status == "Failed".
public class OrchestratorApiClient(HttpClient httpClient) : IOrchestratorApiClient
{
    public async Task<AccountOpeningResponse> StartAccountOpeningAsync(
        StartAccountOpeningRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/account-openings", request, cancellationToken);
        return await ReadBodyOrThrowAsync<AccountOpeningResponse>(
            response, "OrchestratorApi POST /v1/account-openings", cancellationToken);
    }

    public async Task<TransferResultResponse> TransferAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/transfers", request, cancellationToken);
        return await ReadBodyOrThrowAsync<TransferResultResponse>(
            response, "OrchestratorApi POST /v1/transfers", cancellationToken);
    }

    // Reads the body on 200 OR 422 (a committed-but-failed result, same shape either way); only a
    // genuinely unexpected status throws.
    private static async Task<T> ReadBodyOrThrowAsync<T>(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new DownstreamApiException("OrchestratorApi", (int)response.StatusCode, $"{what}: empty response body.");
        }

        var problem = await DownstreamResponseReader.ReadProblemAsync(response, cancellationToken);
        throw new DownstreamApiException(
            "OrchestratorApi", (int)response.StatusCode, $"{what}: {DownstreamResponseReader.DescribeFailure(problem, response)}");
    }
}
