using System.Net;
using System.Text;

namespace Garbati.OpsApi.Tests.Clients;

// A hand-rolled fake HttpMessageHandler (no mocking library, matching Garbati.OrchestratorApi's identical
// test helper) for testing each downstream client's own request-building/response-parsing in isolation,
// without a real network call.
public class FakeHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody, string contentType = "application/json")
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
        {
            response.Content = new StringContent(jsonBody, Encoding.UTF8, contentType);
        }
        return response;
    }
}
