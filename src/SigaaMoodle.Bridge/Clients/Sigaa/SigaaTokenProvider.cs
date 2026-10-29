using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;

namespace SigaaMoodle.Bridge.Clients.Sigaa;

// Obtém e cacheia o token OAuth2 (client credentials) do SIGAA até expirar.
public sealed class SigaaTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SigaaOptions> options) : ISigaaTokenProvider
{
    private readonly SigaaOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cachedToken;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cachedToken;

            if (string.IsNullOrWhiteSpace(_options.TokenUrl))
                throw new InvalidOperationException("Sigaa:TokenUrl não configurada (A DEFINIR).");
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                throw new InvalidOperationException("Configure Sigaa:ClientId e Sigaa:ClientSecret.");

            var client = httpClientFactory.CreateClient("sigaa-token");
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
            };
            if (!string.IsNullOrWhiteSpace(_options.Scope))
                form["scope"] = _options.Scope;

            using var response = await client.PostAsJsonAsync(_options.TokenUrl, form, ct);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
                ?? throw new InvalidOperationException("Resposta de token vazia do SIGAA.");

            if (string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresIn <= 0)
                throw new InvalidOperationException("Resposta OAuth inválida: access_token e expires_in são obrigatórios.");
            _cachedToken = token.AccessToken;
            // margem de 60s para evitar usar token quase expirado.
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
