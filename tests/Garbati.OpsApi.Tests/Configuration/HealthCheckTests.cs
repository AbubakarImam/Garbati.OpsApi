using System.Net;
using Garbati.OpsApi.Tests.Fixtures;

namespace Garbati.OpsApi.Tests.Configuration;

// Hosting platforms probe these without a session cookie, so they must stay anonymous despite the
// fallback policy.
[Collection("Api")]
public class HealthCheckTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_ReturnsHealthy_WithoutASessionCookie(string path)
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
