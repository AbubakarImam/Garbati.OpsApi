using System.Net;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Controllers;

// DESIGN.md Section 2.1b (decision #6): a client-side route (not a real file, not an API route) must
// still resolve to the SPA shell on a full page load, not a bare 404 - the frontend isn't built yet
// (wwwroot-console holds only a placeholder index.html), but the hosting plumbing itself must be real and
// tested (task file: "the folder can be empty for now ... just get the hosting plumbing correct and
// tested").
[Collection("Api")]
public class StaticHostingTests(ApiFixture fixture)
{
    [Fact]
    public async Task AnUnmatchedClientSideRoute_ReturnsTheFallbackFile_NotABare404()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/reversals/OPS-REV-0000000001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GARBATI Ops Console", body);
    }

    [Fact]
    public async Task TheFallback_IsReachableWithNoSessionCookie()
    {
        // The SPA shell itself must load before any login happens - the fallback route must not be
        // caught by the authorization fallback policy that protects every API endpoint.
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/any/unknown/path");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheRootPath_ServesTheIndexFileDirectly()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType!.MediaType);
    }
}
