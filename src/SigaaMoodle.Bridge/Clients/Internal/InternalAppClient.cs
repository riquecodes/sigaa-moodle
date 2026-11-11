using System.Net.Http.Json;
using System.Net;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Clients.Internal;

public sealed class InternalAppClient(HttpClient httpClient, IOptions<InternalAppOptions> options)
    : IInternalAppClient
{
    private static readonly JsonSerializerOptions UpdateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly InternalAppOptions _options = options.Value;
    private readonly HashSet<string> _inactiveStatuses = options.Value.InactiveStatuses.Select(NormalizeStatus).ToHashSet();

    public async Task<SendAlunosResult> SendAlunosAsync(IReadOnlyList<AlunoPayload> payload, CancellationToken ct)
    {
        var route = ValidateRoute(_options.Routes.Alunos);
        if (_options.StudentUserType is not > 0)
            throw new InvalidOperationException("Configure InternalApp:StudentUserType com o ID local do tipo aluno.");

        var errors = new List<RecordError>();
        var valid = new List<(AlunoPayload Source, LocalUser User)>();
        foreach (var item in payload)
        {
            var problem = TryMap(item, out var user);
            if (problem is null) valid.Add((item, user!));
            else errors.Add(Error(item, problem));
        }

        var conflicts = valid.GroupBy(v => v.User.EnrollmentNumber)
            .Where(g => g.Select(v => v.User).Distinct().Count() > 1)
            .Select(g => g.Key).ToHashSet();
        errors.AddRange(valid.Where(v => conflicts.Contains(v.User.EnrollmentNumber))
            .Select(v => Error(v.Source, "Matrícula repetida no SIGAA com dados divergentes.")));

        int created = 0, updated = 0, unchanged = 0, skipped = 0;
        foreach (var (source, user) in valid.Where(v => !conflicts.Contains(v.User.EnrollmentNumber)).DistinctBy(v => v.User))
        {
            try
            {
                switch (await UpsertAsync(route, user, ct))
                {
                    case Outcome.Created: created++; break;
                    case Outcome.Updated: updated++; break;
                    case Outcome.Unchanged: unchanged++; break;
                    case Outcome.Skipped: skipped++; break;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode is { } status)
            {
                errors.Add(Error(source, $"API local respondeu {(int)status} ({status})."));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                errors.Add(Error(source, $"Resposta inválida da API local: {ex.Message}"));
            }
        }
        return new SendAlunosResult(created, updated, unchanged, skipped, errors);
    }

    private async Task<Outcome> UpsertAsync(string route, LocalUser user, CancellationToken ct)
    {
        var userRoute = $"{route}/{user.EnrollmentNumber.ToString(CultureInfo.InvariantCulture)}";
        using var existingResponse = await httpClient.GetAsync(userRoute, ct);
        if (existingResponse.StatusCode == HttpStatusCode.NotFound)
        {
            if (user.IsActive == false)
                return Outcome.Skipped;
            using var created = await httpClient.PostAsJsonAsync(route, new[] { new
            {
                user.EnrollmentNumber, user.CourseId, user.UserType, user.CompleteName,
                LockNumber = 0, LicensePlate = "", CreatedAtUtc = DateTime.UtcNow
            } }, ct);
            created.EnsureSuccessStatusCode();
            return Outcome.Created;
        }
        existingResponse.EnsureSuccessStatusCode();
        var existing = await existingResponse.Content.ReadFromJsonAsync<StoredUser>(ct)
            ?? throw new InvalidOperationException("Consulta de usuário retornou resposta vazia.");
        var active = user.IsActive ?? existing.IsActive;
        if (existing.CourseId == user.CourseId && existing.UserType == user.UserType
            && existing.CompleteName == user.CompleteName && existing.IsActive == active)
            return Outcome.Unchanged;
        using var updated = await httpClient.PutAsJsonAsync(userRoute, new
        {
            user.EnrollmentNumber, user.CourseId, user.UserType, user.CompleteName,
            user.IsActive, UpdatedAtUtc = DateTime.UtcNow
        }, UpdateJsonOptions, ct);
        updated.EnsureSuccessStatusCode();
        return Outcome.Updated;
    }

    private string? TryMap(AlunoPayload item, out LocalUser? user)
    {
        user = null;
        if (!int.TryParse(item.Matricula, NumberStyles.None, CultureInfo.InvariantCulture, out var enrollment)
            || enrollment <= 0 || enrollment.ToString(CultureInfo.InvariantCulture) != item.Matricula)
            return "Matrícula incompatível com Int32 do destino; ajuste o contrato local.";
        if (string.IsNullOrWhiteSpace(item.Nome))
            return "Aluno sem nome_completo.";
        if (item.IdCursoSigaa is not > 0 || !_options.CourseMap.TryGetValue(item.IdCursoSigaa.Value, out var courseId) || courseId <= 0)
            return $"Curso SIGAA {item.IdCursoSigaa?.ToString(CultureInfo.InvariantCulture) ?? "ausente"} sem correspondência em InternalApp:CourseMap.";
        user = new LocalUser(enrollment, courseId, _options.StudentUserType!.Value, item.Nome, IsActive(item.Status));
        return null;
    }

    private bool? IsActive(string? status)
        => string.IsNullOrWhiteSpace(status) ? null : !_inactiveStatuses.Contains(NormalizeStatus(status));

    private static string NormalizeStatus(string status)
    {
        var decomposed = status.Trim().Normalize(NormalizationForm.FormD);
        var chars = decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).ToUpperInvariant();
    }

    private static RecordError Error(AlunoPayload item, string reason)
        => new(item.Matricula ?? "", item.Nome ?? "", reason);

    private string ValidateRoute(string route)
    {
        if (httpClient.BaseAddress is null)
            throw new InvalidOperationException("Configure InternalApp:BaseUrl com a URL da API local.");
        if (string.IsNullOrWhiteSpace(route) || route.StartsWith('/') || route.Contains('?')
            || Uri.TryCreate(route, UriKind.Absolute, out _))
            throw new InvalidOperationException("Configure rota interna relativa, sem barra inicial ou query string.");
        return route.TrimEnd('/');
    }

    private enum Outcome { Created, Updated, Unchanged, Skipped }

    private sealed record LocalUser(int EnrollmentNumber, int CourseId, int UserType, string CompleteName, bool? IsActive);

    private sealed record StoredUser(int EnrollmentNumber, int CourseId, int UserType, string CompleteName, bool IsActive);
}
