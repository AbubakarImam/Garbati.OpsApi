using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Clients.Interfaces;

namespace Garbati.OpsApi.Tests.Fixtures;

// A hand-rolled fake, not a mocking library (matching Garbati.OrchestratorApi's own test style - neither
// project references Moq/NSubstitute). Captures the exact request object passed to each method (not just
// a call count) so tests can assert on fields, e.g. "ChangeRoleAsync was called with ActingStaffId equal
// to the current session's staffId" - the same captured-argument style T-024 showed was necessary.
public class FakeStaffIdentityApiClient : IStaffIdentityApiClient
{
    public int AuthenticateCallCount { get; private set; }
    public int CreateStaffCallCount { get; private set; }
    public int GetStaffCallCount { get; private set; }
    public int ListStaffCallCount { get; private set; }
    public int ChangeRoleCallCount { get; private set; }
    public int ChangeStatusCallCount { get; private set; }
    public int ChangeSupervisorCallCount { get; private set; }
    public int AdminResetPasswordCallCount { get; private set; }

    public AuthenticateRequest? LastAuthenticateRequest { get; private set; }
    public CreateStaffRequest? LastCreateStaffRequest { get; private set; }
    public (string StaffId, ChangeRoleRequest Request)? LastChangeRoleCall { get; private set; }
    public (string StaffId, ChangeStatusRequest Request)? LastChangeStatusCall { get; private set; }
    public (string StaffId, ChangeSupervisorRequest Request)? LastChangeSupervisorCall { get; private set; }
    public (string StaffId, ChangeStaffPasswordRequest Request)? LastAdminResetPasswordCall { get; private set; }

    public Func<AuthenticateRequest, AuthenticateResponse?>? AuthenticateHandler { get; set; }
    public Func<CreateStaffRequest, CreateStaffResponse>? CreateStaffHandler { get; set; }
    public Func<string, StaffResponse?>? GetStaffHandler { get; set; }
    public Func<string?, int, int, StaffListResponse>? ListStaffHandler { get; set; }
    public Func<string, ChangeRoleRequest, StaffResponse>? ChangeRoleHandler { get; set; }
    public Func<string, ChangeStatusRequest, StaffResponse>? ChangeStatusHandler { get; set; }
    public Func<string, ChangeSupervisorRequest, StaffResponse>? ChangeSupervisorHandler { get; set; }
    public Func<string, ChangeStaffPasswordRequest, ChangePasswordResponse>? AdminResetPasswordHandler { get; set; }

    public Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken cancellationToken)
    {
        AuthenticateCallCount++;
        LastAuthenticateRequest = request;
        var handler = AuthenticateHandler ?? (_ => null);
        return Task.FromResult(handler(request));
    }

    public Task<CreateStaffResponse> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        CreateStaffCallCount++;
        LastCreateStaffRequest = request;
        var handler = CreateStaffHandler ?? (r => new CreateStaffResponse(
            new StaffResponse($"STF-FAKE{CreateStaffCallCount:D7}", r.FullName, r.Username, r.Role, "Active", true,
                null, DateTimeOffset.UtcNow, r.ActingStaffId, DateTimeOffset.UtcNow),
            "TempPass123!"));
        return Task.FromResult(handler(request));
    }

    public Task<StaffResponse?> GetStaffAsync(string staffId, CancellationToken cancellationToken)
    {
        GetStaffCallCount++;
        var handler = GetStaffHandler ?? (_ => null);
        return Task.FromResult(handler(staffId));
    }

    public Task<StaffListResponse> ListStaffAsync(string? supervisorStaffId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ListStaffCallCount++;
        var handler = ListStaffHandler ?? ((_, p, ps) => new StaffListResponse([], p, ps, 0));
        return Task.FromResult(handler(supervisorStaffId, page, pageSize));
    }

    public Task<StaffResponse> ChangeRoleAsync(string staffId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        ChangeRoleCallCount++;
        LastChangeRoleCall = (staffId, request);
        var handler = ChangeRoleHandler ?? ((id, r) => new StaffResponse(
            id, "Fake Staff", "fake.staff", r.Role, "Active", false, null, DateTimeOffset.UtcNow, r.ActingStaffId, DateTimeOffset.UtcNow));
        return Task.FromResult(handler(staffId, request));
    }

    public Task<StaffResponse> ChangeStatusAsync(string staffId, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        ChangeStatusCallCount++;
        LastChangeStatusCall = (staffId, request);
        var handler = ChangeStatusHandler ?? ((id, r) => new StaffResponse(
            id, "Fake Staff", "fake.staff", "Teller", r.Status, false, null, DateTimeOffset.UtcNow, r.ActingStaffId, DateTimeOffset.UtcNow));
        return Task.FromResult(handler(staffId, request));
    }

    public Task<StaffResponse> ChangeSupervisorAsync(string staffId, ChangeSupervisorRequest request, CancellationToken cancellationToken)
    {
        ChangeSupervisorCallCount++;
        LastChangeSupervisorCall = (staffId, request);
        var handler = ChangeSupervisorHandler ?? ((id, r) => new StaffResponse(
            id, "Fake Staff", "fake.staff", "Teller", "Active", false, r.SupervisorStaffId, DateTimeOffset.UtcNow, r.ActingStaffId, DateTimeOffset.UtcNow));
        return Task.FromResult(handler(staffId, request));
    }

    public Task<ChangePasswordResponse> AdminResetPasswordAsync(string staffId, ChangeStaffPasswordRequest request, CancellationToken cancellationToken)
    {
        AdminResetPasswordCallCount++;
        LastAdminResetPasswordCall = (staffId, request);
        var handler = AdminResetPasswordHandler ?? ((id, _) => new ChangePasswordResponse(id, "NewTempPass123!"));
        return Task.FromResult(handler(staffId, request));
    }
}
