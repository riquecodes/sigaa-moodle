using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Clients.Sigaa;

public sealed class SigaaClient(HttpClient httpClient, IOptions<SigaaOptions> options) : ISigaaClient
{
    private readonly SigaaOptions _options = options.Value;

    public Task<IReadOnlyList<SigaaAluno>> GetAlunosAsync(DateTimeOffset? since, CancellationToken ct)
        => GetAsync<SigaaAluno>(_options.Routes.Alunos, since, paginated: true, period: false, ct);

    public Task<IReadOnlyList<SigaaMatricula>> GetMatriculasAsync(DateTimeOffset? since, CancellationToken ct)
        => GetAsync<SigaaMatricula>(_options.Routes.Matriculas, since, paginated: false, period: true, ct);

    public Task<IReadOnlyList<SigaaCurso>> GetCursosAsync(DateTimeOffset? since, CancellationToken ct)
        => GetAsync<SigaaCurso>(_options.Routes.Cursos, since, paginated: false, period: false, ct);

    public Task<IReadOnlyList<SigaaNota>> GetNotasAsync(DateTimeOffset? since, CancellationToken ct)
        => GetAsync<SigaaNota>(_options.Routes.Notas, since, paginated: false, period: true, ct);

    private async Task<IReadOnlyList<T>> GetAsync<T>(
        string route, DateTimeOffset? since, bool paginated, bool period, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(route))
            throw new InvalidOperationException("Rota do SIGAA não configurada (A DEFINIR).");

        // A DEFINIR: formato real de paginação/filtro do SIGAA. Quando DeltaEnabled e a API suportar,
        // 'since' é anexado como filtro (ex.: ?since=...). Hoje, se 'since' for null, traz tudo.
        // A documentação atual não oferece filtro de última alteração. Nunca simular delta com data_cadastro.
        if (since is not null)
            throw new NotSupportedException("O SIGAA documentado não suporta sincronização incremental (since).");
        if (_options.PageSize <= 0 || _options.MaxPages <= 0)
            throw new InvalidOperationException("Sigaa:PageSize e Sigaa:MaxPages devem ser positivos.");
        if (period && !System.Text.RegularExpressions.Regex.IsMatch(_options.PeriodoLetivo, @"^\d{4}/[012]$"))
            throw new InvalidOperationException("Configure Sigaa:PeriodoLetivo como YYYY/0, YYYY/1 ou YYYY/2.");
        if (route.StartsWith('/') || Uri.TryCreate(route, UriKind.Absolute, out _) || route.Contains('?'))
            throw new InvalidOperationException("Rotas SIGAA devem ser relativas, sem barra inicial ou query string.");

        var query = new Dictionary<string, string?>();
        if (_options.Unidade is { } unidade)
            query["unidade"] = unidade.ToString(CultureInfo.InvariantCulture);
        if (period) query["periodo_letivo"] = _options.PeriodoLetivo;
        if (paginated) query["per_page"] = _options.PageSize.ToString(CultureInfo.InvariantCulture);

        var all = new List<T>();
        for (var page = 1; page <= _options.MaxPages; page++)
        {
            if (paginated || page > 1) query["page"] = page.ToString(CultureInfo.InvariantCulture);
            using var response = await httpClient.GetAsync(QueryHelpers.AddQueryString(route, query), ct);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (paginated || page > 1)
                    throw new JsonException("Esperado envelope paginado do SIGAA; recebido array sem metadados.");
                all.AddRange(root.Deserialize<List<T>>(JsonOptions) ?? throw new JsonException("Lista SIGAA inválida."));
                return all;
            }
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("current_page", out var current) || current.GetInt32() != page)
                throw new JsonException("Envelope de paginação SIGAA inválido ou página repetida.");
            all.AddRange(data.Deserialize<List<T>>(JsonOptions) ?? throw new JsonException("Lista SIGAA inválida."));
            if (root.TryGetProperty("last_page", out var last))
            {
                if (last.GetInt32() < page) throw new JsonException("last_page inválido.");
                if (page == last.GetInt32()) return Complete(root, all);
            }
            else if (root.TryGetProperty("total", out var total) && root.TryGetProperty("per_page", out var size))
            {
                if (total.GetInt32() < 0 || size.GetInt32() <= 0) throw new JsonException("Totais de paginação inválidos.");
                if ((long)page * size.GetInt32() >= total.GetInt32()) return Complete(root, all);
            }
            else if (root.TryGetProperty("next_page_url", out var next))
            {
                // Não seguir URLs arbitrárias do servidor: preserva host, prefixo e filtros originais.
                if (next.ValueKind == JsonValueKind.Null) return Complete(root, all);
                if (next.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(next.GetString()))
                    throw new JsonException("next_page_url inválido.");
            }
            else throw new JsonException("Envelope SIGAA sem metadados para determinar próxima página.");
            if (data.GetArrayLength() == 0) throw new JsonException("Página vazia antes do fim da paginação.");
        }
        throw new InvalidOperationException("Sigaa:MaxPages excedido; sincronização interrompida para evitar dados incompletos.");
    }

    private static IReadOnlyList<T> Complete<T>(JsonElement root, List<T> items)
    {
        if (root.TryGetProperty("total", out var total) && items.Count != total.GetInt32())
            throw new JsonException("Total SIGAA não corresponde aos registros coletados; execute novamente para obter um conjunto consistente.");
        return items;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
