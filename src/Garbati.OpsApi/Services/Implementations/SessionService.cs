using Garbati.OpsApi.Authorization;
using Garbati.OpsApi.Clients.Contracts.StaffIdentity;
using Garbati.OpsApi.Clients.Interfaces;
using Garbati.OpsApi.Configuration;
using Garbati.OpsApi.Data;
using Garbati.OpsApi.Entities;
using Garbati.OpsApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garbati.OpsApi.Services.Implementations;

public class SessionService(
    OpsDbContext dbContext,
    IStaffIdentityApiClient staffIdentityApiClient,
    IOptions<OpsSessionOptions> sessionOptions) : ISessionService
{
    public async Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var authenticated = await staffIdentityApiClient.AuthenticateAsync(
            new AuthenticateRequest(username, password), cancellationToken);
        if (authenticated is null)
        {
            return null;
        }

        var rawToken = SessionTokenHasher.GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(sessionOptions.Value.AbsoluteTimeoutHours);

        var session = new OpsSession
        {
            SessionTokenHash = SessionTokenHasher.Hash(rawToken),
            StaffId = authenticated.StaffId,
            FullName = authenticated.FullName,
            Role = authenticated.Role,
            CreatedAt = now,
            LastActivityAt = now,
            ExpiresAt = expiresAt,
        };

        dbContext.OpsSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(rawToken, session.StaffId, session.FullName, session.Role, session.ExpiresAt);
    }

    public async Task LogoutAsync(long sessionId, CancellationToken cancellationToken)
    {
        await dbContext.OpsSessions.Where(s => s.SessionId == sessionId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RevokeAllSessionsForStaffAsync(string staffId, CancellationToken cancellationToken)
    {
        await dbContext.OpsSessions.Where(s => s.StaffId == staffId).ExecuteDeleteAsync(cancellationToken);
    }
}
