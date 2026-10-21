using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Data;
using Garbati.OpsApi.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Garbati.OpsApi.Tests.Fixtures;

// Runs the real app in memory (WebApplicationFactory) against its own Postgres container, so requests go
// through the full pipeline: correlation id, session authentication, role authorization, controllers and
// the database.
//
// All four downstream clients (IStaffIdentityApiClient/IIdentityApiClient/IOrchestratorApiClient/
// ILedgerApiClient) are replaced with hand-rolled fakes here - this service is tested against
// mocked/stubbed HTTP clients, not real locally-run StaffIdentityApi/IdentityApi/OrchestratorApi/LedgerApi
// instances (see the PR description for the reasoning and what that means this suite cannot verify).
// DownstreamServices:* configuration is still filled with syntactically valid placeholder values below
// purely so DownstreamServicesOptionsValidator's ValidateOnStart passes - those values are never actually
// dialled.
public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public FakeStaffIdentityApiClient StaffIdentityApiClient { get; } = new();
    public FakeIdentityApiClient IdentityApiClient { get; } = new();
    public FakeOrchestratorApiClient OrchestratorApiClient { get; } = new();
    public FakeLedgerApiClient LedgerApiClient { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using (var ctx = CreateContext())
        {
            await ctx.Database.MigrateAsync();
        }

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:OpsDb"] = _container.GetConnectionString(),
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Swagger:Enabled"] = "true",
            ["DownstreamServices:StaffIdentityApi:BaseUrl"] = "http://unused.invalid",
            ["DownstreamServices:StaffIdentityApi:ApiKey"] = "unused-placeholder",
            ["DownstreamServices:IdentityApi:BaseUrl"] = "http://unused.invalid",
            ["DownstreamServices:IdentityApi:ApiKey"] = "unused-placeholder",
            ["DownstreamServices:OrchestratorApi:BaseUrl"] = "http://unused.invalid",
            ["DownstreamServices:OrchestratorApi:ApiKey"] = "unused-placeholder",
            ["DownstreamServices:LedgerApi:BaseUrl"] = "http://unused.invalid",
            ["DownstreamServices:LedgerApi:ApiKey"] = "unused-placeholder",
        };

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStaffIdentityApiClient>();
                services.AddSingleton<IStaffIdentityApiClient>(StaffIdentityApiClient);
                services.RemoveAll<IIdentityApiClient>();
                services.AddSingleton<IIdentityApiClient>(IdentityApiClient);
                services.RemoveAll<IOrchestratorApiClient>();
                services.AddSingleton<IOrchestratorApiClient>(OrchestratorApiClient);
                services.RemoveAll<ILedgerApiClient>();
                services.AddSingleton<ILedgerApiClient>(LedgerApiClient);
            });
        });
    }

    public OpsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OpsDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OpsDbContext(options);
    }

    // A plain HTTP client for the in-memory app. Cookie handling is done manually in tests (extracting
    // Set-Cookie and attaching it as a Cookie header on later requests) rather than relying on
    // HttpClient's automatic cookie jar, because the session cookie is marked Secure (DESIGN.md Section
    // 2.1) and WebApplicationFactory's default BaseAddress is http://, not https:// - a real cookie jar
    // would correctly refuse to resend a Secure cookie over that scheme, which would make every test
    // using the automatic jar fail for a reason that has nothing to do with this service's own logic.
    public HttpClient CreateClient() => Factory.CreateClient();

    // Creates an OpsSession row directly, bypassing POST /v1/ops/login, so tests can set
    // LastActivityAt/ExpiresAt to arbitrary values in the past (DESIGN.md Section 2.2, decision #1's idle
    // and absolute timeouts) - the task file's own test list: "a session artificially pushed past idle
    // timeout -> 401" and "past absolute timeout -> 401". Returns the raw token to use as the cookie
    // value - never the hash, mirroring exactly what a real login response would have put in the cookie.
    public async Task<string> CreateSessionDirectlyAsync(
        string staffId, string fullName, string role, DateTimeOffset lastActivityAt, DateTimeOffset expiresAt)
    {
        var rawToken = SessionTokenHasher.GenerateRawToken();
        await using var context = CreateContext();
        context.OpsSessions.Add(new OpsSession
        {
            SessionTokenHash = SessionTokenHasher.Hash(rawToken),
            StaffId = staffId,
            FullName = fullName,
            Role = role,
            CreatedAt = lastActivityAt,
            LastActivityAt = lastActivityAt,
            ExpiresAt = expiresAt,
        });
        await context.SaveChangesAsync();
        return rawToken;
    }

    public async Task<int> CountSessionsForStaffAsync(string staffId)
    {
        await using var context = CreateContext();
        return await context.OpsSessions.CountAsync(s => s.StaffId == staffId);
    }

    // T-034: simulates a role change landing between an earlier page load (e.g. when a Supervisor's queue
    // was first displayed) and a later action against the same still-live session - the exact scenario the
    // reversal/customer-edit maker-checker hard rule's "re-checked live, not cached" requirement guards
    // against. Mutates the already-issued session's own cached Role directly, the same row
    // SessionAuthenticationHandler re-reads fresh from the database on every single request.
    public async Task SetSessionRoleAsync(string rawToken, string role)
    {
        var tokenHash = SessionTokenHasher.Hash(rawToken);
        await using var context = CreateContext();
        var session = await context.OpsSessions.SingleAsync(s => s.SessionTokenHash == tokenHash);
        session.Role = role;
        await context.SaveChangesAsync();
    }

    // T-034's lazy-expiry-aware listing tests: pushes a stored RequestedAt into the past, the same
    // "artificially age a row directly in the database" approach CreateSessionDirectlyAsync already uses
    // for session idle/absolute timeouts - no sleeping in a test for 24 real hours.
    public async Task AgeReversalRequestAsync(string id, DateTimeOffset requestedAt)
    {
        await using var context = CreateContext();
        var request = await context.OpsReversalRequests.SingleAsync(r => r.Id == id);
        request.RequestedAt = requestedAt;
        await context.SaveChangesAsync();
    }

    public async Task AgeCustomerEditRequestAsync(string id, DateTimeOffset requestedAt)
    {
        await using var context = CreateContext();
        var request = await context.OpsCustomerEditRequests.SingleAsync(r => r.Id == id);
        request.RequestedAt = requestedAt;
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFixture>;
