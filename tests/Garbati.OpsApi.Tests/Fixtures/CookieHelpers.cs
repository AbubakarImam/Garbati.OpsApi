namespace Garbati.OpsApi.Tests.Fixtures;

// Manual Set-Cookie/Cookie handling for tests (see ApiFixture.CreateClient's comment for why this
// service's own automatic cookie jar is not used here).
public static class CookieHelpers
{
    // Extracts just the "name=value" pair from a Set-Cookie response header, dropping every attribute
    // (Path, Secure, HttpOnly, SameSite, Expires) - exactly what a browser would send back as the Cookie
    // request header on a later request.
    public static string? ExtractCookiePair(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            var firstSegment = value.Split(';')[0].Trim();
            if (firstSegment.StartsWith($"{cookieName}=", StringComparison.Ordinal))
            {
                return firstSegment;
            }
        }

        return null;
    }

    // True when the Set-Cookie header for cookieName carries an empty value and a past Expires - what
    // Response.Cookies.Delete(...) produces (used to assert logout/expiry actually clears the cookie).
    public static bool CookieWasCleared(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return false;
        }

        return values.Any(v => v.StartsWith($"{cookieName}=", StringComparison.Ordinal)
            && (v.Contains($"{cookieName}=;") || v.Contains($"{cookieName}= ;") || v.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)));
    }
}
