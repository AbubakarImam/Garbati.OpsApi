using Garbati.OpsApi.Clients.Interfaces;

namespace Garbati.OpsApi.Tests.Clients;

// DESIGN.md Section 3's scope table is deliberately narrow per downstream service. Rather than asserting
// a scope string somewhere (this service never sends per-request scopes - each downstream service's own
// ApiKeys config decides what OpsApi's one issued key per service may do), this proves the narrowness
// structurally: each client INTERFACE exposes only the method set that maps to its granted scopes, so a
// future change that accidentally widens what OpsApi can even attempt to call is caught here, at compile/
// test time, not left to be caught by a downstream 403 in production. The same kind of regression test
// T-024 showed is necessary - a captured-shape assertion, not just "it compiles".
public class DownstreamClientScopeTests
{
    [Fact]
    public void ILedgerApiClient_ExposesOnlyReadAndFundingOperations()
    {
        // DESIGN.md Section 3: ledger.read + ledger.funding ONLY - never ledger.accounts/.transfer/
        // .reverse/.admin. GetAccountAsync/GetBalanceAsync are the read operations; DepositAsync is the
        // one funding operation (3.4). No method here may ever call LedgerApi's account-CREATION,
        // transfer, reversal or system-account-admin endpoints.
        var methodNames = typeof(ILedgerApiClient).GetMethods().Select(m => m.Name).ToArray();

        Assert.Equal(["GetAccountAsync", "GetBalanceAsync", "DepositAsync"], methodNames);
    }

    [Fact]
    public void IOrchestratorApiClient_ExposesOnlyAccountOpeningsAndTransfers()
    {
        // DESIGN.md Section 3: orchestrator.account-openings + orchestrator.transfers - NOT
        // orchestrator.reversals. T-034's job, not this task's.
        var methodNames = typeof(IOrchestratorApiClient).GetMethods().Select(m => m.Name).ToArray();

        Assert.Equal(["StartAccountOpeningAsync", "TransferAsync"], methodNames);
        Assert.DoesNotContain(methodNames, name => name.Contains("Reversal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IIdentityApiClient_HasNoReversalCapability()
    {
        // DESIGN.md decision #5: identity.ops.reversal belongs to OrchestratorApi, never to OpsApi's own
        // IdentityApi client - this client must have no method that could even attempt to call it.
        var methodNames = typeof(IIdentityApiClient).GetMethods().Select(m => m.Name).ToArray();

        Assert.DoesNotContain(methodNames, name => name.Contains("Reversal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IStaffIdentityApiClient_ExposesExactlyStaffIdentityApisEightEndpoints()
    {
        var methodNames = typeof(IStaffIdentityApiClient).GetMethods().Select(m => m.Name).ToArray();

        Assert.Equal(
            [
                "AuthenticateAsync", "CreateStaffAsync", "GetStaffAsync", "ListStaffAsync",
                "ChangeRoleAsync", "ChangeStatusAsync", "ChangeSupervisorAsync", "AdminResetPasswordAsync",
            ],
            methodNames);
    }
}
