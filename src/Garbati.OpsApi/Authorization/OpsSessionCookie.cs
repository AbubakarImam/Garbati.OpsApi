namespace Garbati.OpsApi.Authorization;

// DESIGN.md Section 2.1/2.1b: "a cookie containing only the opaque, random session token - never the
// staffId or role directly", HttpOnly/Secure/SameSite=Strict. One place both Controllers/SessionController
// (issuing it) and SessionAuthenticationHandler (reading it) agree on the name and shape.
public static class OpsSessionCookie
{
    public const string Name = "ops_session";

    public static CookieOptions Options(DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = expiresAt,
        IsEssential = true,
    };

    // Clearing uses the exact same attributes used to set it (Secure/SameSite/HttpOnly) - a browser
    // ignores a Delete call whose attributes do not match how the cookie was originally scoped.
    public static CookieOptions DeleteOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
    };
}
