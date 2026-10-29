using System.Net.Http.Json;
using System.Net;
using System.Globalization;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Clients.Internal;

// Cliente da app interna da faculdade (sem autenticação — rede interna). O destino faz upsert.
// O destino observado oferece criação e atualização separadas; a ponte executa o upsert.
public sealed class InternalAppClient(HttpClient httpClient, IOptions<InternalAppOptions> options)
    : IInternalAppClient
{
    private readonly InternalAppOptions _options = options.Value;

    public async Task SendAlunosAsync(IReadOnlyList<AlunoPayload> payload, CancellationToken ct)
    {
        var route = ValidateRoute(_options.Routes.Alunos);
        if (_options.StudentUserType is not > 0)
            throw new InvalidOperationException("Configure InternalApp:StudentUserType com o ID local do tipo aluno.");
        // Valida todo o lote antes de escrever; nunca truncar matrículas.
        var users = payload.Select(p =>
        {
            if (!int.TryParse(p.Matricula, NumberStyles.None, CultureInfo.InvariantCulture, out var enrollment)
                || enrollment <= 0 || enrollment.ToString(CultureInfo.InvariantCulture) != p.Matricula)
                throw new InvalidOperationException("Matrícula incompatível com Int32 do destino; ajuste o contrato local antes de sincronizar.");
            if (string.IsNullOrWhiteSpace(p.Nome)) throw new InvalidOperationException("Aluno sem nome_completo.");
            return new LocalUser(enrollment, CourseId(p.IdCursoSigaa), _options.StudentUserType.Value, p.Nome);
        }).ToList();
        if (users.GroupBy(u => u.EnrollmentNumber).Any(g => g.Distinct().Count() > 1))
            throw new InvalidOperationException("Matrícula repetida com dados divergentes no SIGAA.");
        foreach (var user in users.Distinct())
        {
            var userRoute = $"{route}/{user.EnrollmentNumber.ToString(CultureInfo.InvariantCulture)}";
            using var existingResponse = await httpClient.GetAsync(userRoute, ct);
            if (existingResponse.StatusCode == HttpStatusCode.NotFound)
            {
                using var created = await httpClient.PostAsJsonAsync(route, new[] { new
                {
                    user.EnrollmentNumber, user.CourseId, user.UserType, user.CompleteName,
                    LockNumber = 0, LicensePlate = "", CreatedAtUtc = DateTime.UtcNow
                } }, ct);
                created.EnsureSuccessStatusCode();
                continue;
            }
            existingResponse.EnsureSuccessStatusCode();
            var existing = await existingResponse.Content.ReadFromJsonAsync<LocalUser>(ct)
                ?? throw new InvalidOperationException("Consulta de usuário retornou resposta vazia.");
            if (existing == user) continue;
            // Preserva armário, placa e estado ativo: não são gerenciados pela ponte.
            using var updated = await httpClient.PutAsJsonAsync(userRoute, new
            {
                user.EnrollmentNumber, user.CourseId, user.UserType, user.CompleteName,
                UpdatedAtUtc = DateTime.UtcNow
            }, ct);
            updated.EnsureSuccessStatusCode();
        }
    }

    public Task SendMatriculasAsync(IReadOnlyList<MatriculaPayload> payload, CancellationToken ct)
        => throw new NotSupportedException("A API local não possui contrato de matrículas. Não habilite esse domínio sem implementar o destino.");

    public async Task SendCursosAsync(IReadOnlyList<CursoPayload> payload, CancellationToken ct)
    {
        var route = ValidateRoute(_options.Routes.Cursos);
        var courses = payload.Select(p => (LocalId: CourseId(p.IdCursoSigaa), p.Nome)).ToList();
        if (courses.Any(c => string.IsNullOrWhiteSpace(c.Nome))) throw new InvalidOperationException("Curso SIGAA sem nome.");
        if (courses.GroupBy(c => c.LocalId).Any(g => g.Select(c => c.Nome).Distinct().Count() > 1))
            throw new InvalidOperationException("CourseMap associa cursos com nomes diferentes ao mesmo ID local.");
        foreach (var course in courses.Distinct())
        {
            var courseRoute = $"{route}/{course.LocalId.ToString(CultureInfo.InvariantCulture)}";
            using var existing = await httpClient.GetAsync(courseRoute, ct);
            existing.EnsureSuccessStatusCode();
            var current = await existing.Content.ReadFromJsonAsync<LocalCourse>(ct)
                ?? throw new InvalidOperationException("Consulta de curso retornou resposta vazia.");
            if (current.CourseName == course.Nome) continue;
            // Sem equivalentes documentados para coordenação/duração; preservar valores locais.
            using var updated = await httpClient.PutAsJsonAsync(courseRoute, new
            {
                CourseName = course.Nome, UpdatedAtUtc = DateTime.UtcNow
            }, ct);
            updated.EnsureSuccessStatusCode();
        }
    }

    public Task SendNotasAsync(IReadOnlyList<NotaPayload> payload, CancellationToken ct)
        => throw new NotSupportedException("A API local não possui contrato de notas. Não habilite esse domínio sem implementar o destino.");

    private int CourseId(int? sigaaId)
    {
        if (sigaaId is not > 0 || !_options.CourseMap.TryGetValue(sigaaId.Value, out var localId) || localId <= 0)
            throw new InvalidOperationException($"Configure InternalApp:CourseMap para id_curso SIGAA {sigaaId?.ToString(CultureInfo.InvariantCulture) ?? "ausente"}.");
        return localId;
    }

    private string ValidateRoute(string route)
    {
        if (httpClient.BaseAddress is null)
            throw new InvalidOperationException("Configure InternalApp:BaseUrl com a URL da API local.");
        if (string.IsNullOrWhiteSpace(route) || route.StartsWith('/') || route.Contains('?')
            || Uri.TryCreate(route, UriKind.Absolute, out _))
            throw new InvalidOperationException("Configure rota interna relativa, sem barra inicial ou query string.");
        return route.TrimEnd('/');
    }

    private sealed record LocalUser(int EnrollmentNumber, int CourseId, int UserType, string CompleteName);
    private sealed record LocalCourse(string CourseName);
}
