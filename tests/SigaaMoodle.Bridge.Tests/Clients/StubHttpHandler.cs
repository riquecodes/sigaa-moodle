using System.Net;
using System.Text;

namespace SigaaMoodle.Bridge.Tests.Clients;

internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => send(request, ct);

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
