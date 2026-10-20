using System.Net;
using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Exceptions;

namespace Garbati.OpsApi.Clients.Implementations;

// Talks to StaffIdentityApi over HTTP. The HttpClient is configured (BaseAddress, X-Api-Key header,
// X-Correlation-ID propagation) at registration time in Program.cs - this class only knows the paths and
// how to turn a response into either a typed result or an exception. Routes confirmed against
// Garbati.StaffIdentityApi.Controllers.StaffController's live [Route("v1/staff")] attribute.
public class StaffIdentityApiClient(HttpClient httpClient) : IStaffIdentityApiClient
{
    // DESIGN.md Section 2.1: wrong username, wrong password and Status != Active are all the exact same
    // 401 with no distinguishing body - returning null here (rather than throwing) lets
    // SessionController.Login propagate that same 401 "unchanged", per the task file.
    public async Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/staff/authenticate", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await ReadOrThrowAsync<AuthenticateResponse>(response, "StaffIdentityApi POST /v1/staff/authenticate", cancellationToken);
    }

    public async Task<CreateStaffResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("v1/staff", request, cancellationToken);
        return await ReadOrThrowAsync<CreateStaffResponse>(response, "StaffIdentityApi POST /v1/staff", cancellationToken);
    }

    public async Task<StaffResponse?> GetStaffAsync(string staffId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"v1/staff/{staffId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadOrThrowAsync<StaffResponse>(response, $"StaffIdentityApi GET /v1/staff/{staffId}", cancellationToken);
    }

    public async Task<StaffListResponse> ListStaffAsync(
        string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = $"v1/staff?page={page}&pageSize={pageSize}"
            + (supervisorStaffId is null ? "" : $"&supervisorStaffId={Uri.EscapeDataString(supervisorStaffId)}");
        using var response = await httpClient.GetAsync(query, cancellationToken);
        return await ReadOrThrowAsync<StaffListResponse>(response, "StaffIdentityApi GET /v1/staff", cancellationToken);
    }

    public async Task<StaffResponse> ChangeRoleAsync(string staffId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PatchAsJsonAsync($"v1/staff/{staffId}/role", request, cancellationToken);
        return await ReadOrThrowAsync<StaffResponse>(response, $"StaffIdentityApi PATCH /v1/staff/{staffId}/role", cancellationToken);
    }

    public async Task<StaffResponse> ChangeStatusAsync(string staffId, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PatchAsJsonAsync($"v1/staff/{staffId}/status", request, cancellationToken);
        return await ReadOrThrowAsync<StaffResponse>(response, $"StaffIdentityApi PATCH /v1/staff/{staffId}/status", cancellationToken);
    }

    public async Task<StaffResponse> ChangeSupervisorAsync(
        string staffId, ChangeSupervisorRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PatchAsJsonAsync($"v1/staff/{staffId}/supervisor", request, cancellationToken);
        return await ReadOrThrowAsync<StaffResponse>(response, $"StaffIdentityApi PATCH /v1/staff/{staffId}/supervisor", cancellationToken);
    }

    public async Task<ChangePasswordResponse> AdminResetPasswordAsync(
        string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync($"v1/staff/{staffId}/password", request, cancellationToken);
        return await ReadOrThrowAsync<ChangePasswordResponse>(response, $"StaffIdentityApi POST /v1/staff/{staffId}/password", cancellationToken);
    }

    private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response, string what, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new DownstreamApiException("StaffIdentityApi", (int)response.StatusCode, $"{what}: empty response body.");
        }

        var problem = await DownstreamResponseReader.ReadProblemAsync(response, cancellationToken);
        throw new DownstreamApiException(
            "StaffIdentityApi", (int)response.StatusCode, $"{what}: {DownstreamResponseReader.DescribeFailure(problem, response)}");
    }
}
