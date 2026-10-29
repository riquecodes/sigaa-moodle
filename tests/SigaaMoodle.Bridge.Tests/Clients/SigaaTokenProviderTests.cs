using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Configuration;

namespace SigaaMoodle.Bridge.Tests.Clients;

public class SigaaTokenProviderTests
{
    [Fact]
    public async Task OAuth_EnviaJsonDocumentado_ECacheiaToken()
    {
        var calls = 0;
        using var handler = new StubHttpHandler(async (request, ct) =>
        {
            calls++;
            request.RequestUri!.AbsoluteUri.ShouldBe("https://sigaa.test/oauth/token");
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            json.RootElement.GetProperty("grant_type").GetString().ShouldBe("client_credentials");
            json.RootElement.GetProperty("client_id").GetString().ShouldBe("client");
            json.RootElement.GetProperty("client_secret").GetString().ShouldBe("test-secret");
            return StubHttpHandler.Json("""{"access_token":"test-token","expires_in":3600}""");
        });
        using var http = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("sigaa-token").Returns(http);
        var provider = new SigaaTokenProvider(factory, Options.Create(new SigaaOptions
        {
            TokenUrl = "https://sigaa.test/oauth/token", ClientId = "client", ClientSecret = "test-secret"
        }));
        var tokens = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => provider.GetAccessTokenAsync(default)));
        tokens.ShouldAllBe(t => t == "test-token");
        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData("{\"access_token\":\"\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"token\",\"expires_in\":0}")]
    public async Task OAuth_RejeitaTokenInvalido(string response)
    {
        using var handler = new StubHttpHandler((_, _) => Task.FromResult(StubHttpHandler.Json(response)));
        using var http = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("sigaa-token").Returns(http);
        var provider = new SigaaTokenProvider(factory, Options.Create(new SigaaOptions
        {
            TokenUrl = "https://sigaa.test/oauth/token", ClientId = "client", ClientSecret = "test-secret"
        }));
        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAccessTokenAsync(default));
    }
}
