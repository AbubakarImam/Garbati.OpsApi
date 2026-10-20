using System.Net;
using Garbati.OpsApi.Clients.Contracts.Ledger;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Exceptions;

namespace Garbati.OpsApi.Clients.Implementations;

// Talks to LedgerApi over HTTP. Routes confirmed against LedgerApi.Controllers.AccountsController/
// DepositsController's live [Route("api/[controller]")] attributes (LedgerApi predates the platform's
// "/v1/" convention - ARCHITECTURE.md Section 5 - on its existing routes, confirmed directly against its
// source rather than assumed).
public class LedgerApiClient(HttpClient httpClient) : ILedgerApiClient
{
    public async Task<AccountResponse?> GetAccountAsync(string accountNumber, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/accounts/{accountNumber}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<AccountResponse>(response, $"LedgerApi GET /api/accounts/{accountNumber}", cancellationToken);
    }

    public async Task<BalanceResponse?> GetBalanceAsync(string accountNumber, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/accounts/{accountNumber}/balance", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<BalanceResponse>(
            response, $"LedgerApi GET /api/accounts/{accountNumber}/balance", cancellationToken);
    }

    public async Task<TransferResponse> DepositAsync(DepositRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/deposits", request, cancellationToken);

        // A business-rule failure (e.g. account not found/closed) is committed and returned like a
        // success, but with 422 (LedgerApi decision #29) - read the body either way, same as
        // OrchestratorApiClient does for its own 422 case.
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return await response.Content.ReadFromJsonAsync<TransferResponse>(cancellationToken)
                ?? throw new DownstreamApiException("LedgerApi", (int)response.StatusCode, "LedgerApi POST /api/deposits: empty response body.");
        }

        var problem = await DownstreamResponseReader.ReadProblemAsync(response, cancellationToken);
        throw new DownstreamApiException(
            "LedgerApi", (int)response.StatusCode, $"LedgerApi POST /api/deposits: {DownstreamResponseReader.DescribeFailure(problem, response)}");
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new DownstreamApiException("LedgerApi", (int)response.StatusCode, $"{what}: empty response body.");
        }

        var problem = await DownstreamResponseReader.ReadProblemAsync(response, cancellationToken);
        throw new DownstreamApiException(
            "LedgerApi", (int)response.StatusCode, $"{what}: {DownstreamResponseReader.DescribeFailure(problem, response)}");
    }
}
