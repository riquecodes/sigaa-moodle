using System.Net.Http.Headers;

namespace SigaaMoodle.Bridge.Clients.Sigaa;

// DelegatingHandler que injeta o Bearer token em cada requisição ao SIGAA.
public sealed class SigaaTokenHandler(ISigaaTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
