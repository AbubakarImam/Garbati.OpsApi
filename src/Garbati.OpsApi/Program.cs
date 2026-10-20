using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Implementations;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Configuration;
using Garbati.OpsApi.Data;
using Garbati.OpsApi.Middleware;
using Garbati.OpsApi.Services.Implementations;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

// Bootstrap logger: records errors that happen before configuration is loaded (e.g. a broken appsettings file).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddRouting(options => options.LowercaseUrls = true);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // This service's own inbound authentication: a staff session cookie (task file point 4), NOT the
    // ApiKeyAuthenticationHandler pattern every sibling service uses for its own inbound callers - OpsApi
    // has no API-key-authenticated callers of its own in this version, only the browser.
    builder.Services.AddOptions<OpsSessionOptions>()
        .Bind(builder.Configuration.GetSection("Session"));

    builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

    // No per-scope policies here (contrast every sibling service's AddPolicy-per-scope): Section 4's
    // matrix is enforced with ASP.NET Core's own built-in [Authorize(Roles = "...")], backed by the
    // single ClaimTypes.Role claim SessionAuthenticationHandler sets per request. The fallback policy
    // still demands at least a valid session, so an endpoint without its own [Authorize]/[AllowAnonymous]
    // is never public.
    builder.Services.AddAuthorization(options =>
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    builder.Services.AddDbContext<OpsDbContext>(options => options
        .UseNpgsql(builder.Configuration.GetConnectionString("OpsDb"))
        .UseSnakeCaseNamingConvention());

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<OpsDbContext>("database", tags: ["ready"]);

    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<IStaffManagementService, StaffManagementService>();
    builder.Services.AddScoped<ICustomerLookupService, CustomerLookupService>();
    builder.Services.AddScoped<ITestingConsoleService, TestingConsoleService>();
    builder.Services.AddScoped<IJointApprovalConsoleService, JointApprovalConsoleService>();

    // This service as a client of all four downstream services (task file: "an HTTP client setup ...
    // configured with this service's own API key for each" - Garbati.OrchestratorApi's proven pattern,
    // extended from two clients to four). BaseAddress and the outgoing X-Api-Key value come from
    // DownstreamServicesOptions, resolved once per HttpClient instance from the pool.
    builder.Services.AddOptions<DownstreamServicesOptions>()
        .Bind(builder.Configuration.GetSection("DownstreamServices"))
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<DownstreamServicesOptions>, DownstreamServicesOptionsValidator>();

    builder.Services.AddTransient<CorrelationIdPropagationHandler>();

    const string apiKeyHeaderName = "X-Api-Key";

    builder.Services.AddHttpClient<IStaffIdentityApiClient, StaffIdentityApiClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value.StaffIdentityApi;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add(apiKeyHeaderName, options.ApiKey);
    }).AddHttpMessageHandler<CorrelationIdPropagationHandler>();

    builder.Services.AddHttpClient<IIdentityApiClient, IdentityApiClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value.IdentityApi;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add(apiKeyHeaderName, options.ApiKey);
    }).AddHttpMessageHandler<CorrelationIdPropagationHandler>();

    builder.Services.AddHttpClient<IOrchestratorApiClient, OrchestratorApiClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value.OrchestratorApi;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add(apiKeyHeaderName, options.ApiKey);
    }).AddHttpMessageHandler<CorrelationIdPropagationHandler>();

    builder.Services.AddHttpClient<ILedgerApiClient, LedgerApiClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value.LedgerApi;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Add(apiKeyHeaderName, options.ApiKey);
    }).AddHttpMessageHandler<CorrelationIdPropagationHandler>();

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
            : LogEventLevel.Information;

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("UserId", httpContext.User.Identity?.Name ?? "anonymous");
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
        };
    });

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // No UseHttpsRedirection: a redirect cannot protect a session cookie that was already sent over HTTP.
    // TLS belongs to the deployment (proxy/ingress or Kestrel), same as every sibling service.

    // DESIGN.md Section 2.1b (decision #6): same-origin static hosting for the eventual ops-console
    // frontend, from a DEDICATED folder, not the implicit wwwroot root. The frontend isn't built yet
    // (garbati-ops-console, T-029, design-only) - wwwroot-console holds only a placeholder index.html so
    // this plumbing is real and testable now, ready for that repo's own independent deploy job to drop
    // its built assets into later.
    var consoleFileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot-console"));
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = consoleFileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = consoleFileProvider });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

    // Terminal, lowest-priority route: only reached when nothing above (a controller action or a real
    // static file) matched - a full-page load of a client-side route (e.g. a bookmarked deep link) still
    // resolves to the SPA's index.html instead of a bare 404 (DESIGN.md Section 2.1b).
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = consoleFileProvider }).AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ops API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// Lets the test project start the app in memory with WebApplicationFactory<Program>.
public partial class Program;
