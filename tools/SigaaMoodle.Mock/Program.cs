using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

const string MockClientId = "mock-client";
const string MockClientSecret = "mock-secret";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dataDir = Path.Combine(app.Environment.ContentRootPath, "data");
var alunos = new SigaaAlunosFile(Path.Combine(dataDir, "sigaa-alunos.json"));
var usuarios = new LocalUsersFile(Path.Combine(dataDir, "usuarios.json"));
var tokens = new ConcurrentDictionary<string, DateTimeOffset>();
var resumo = new MockSummary();

bool Authorized(HttpRequest request)
{
    var header = request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.Ordinal)
        && tokens.TryGetValue(header["Bearer ".Length..], out var expiresAt)
        && expiresAt > DateTimeOffset.UtcNow;
}

app.MapPost("/oauth/token", (TokenRequest request) =>
{
    if (request.GrantType != "client_credentials" || request.ClientId != MockClientId || request.ClientSecret != MockClientSecret)
        return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

    var token = $"mock-{Guid.NewGuid():N}";
    tokens[token] = DateTimeOffset.UtcNow.AddHours(1);
    Interlocked.Increment(ref resumo.TokensIssued);
    return Results.Ok(new { token_type = "Bearer", expires_in = 3600, access_token = token });
});

app.MapGet("/api/v1/sig/sigaa/alunos/paginate", (
    HttpRequest request,
    [FromQuery] int? page,
    [FromQuery(Name = "per_page")] int? perPage) =>
{
    if (!Authorized(request))
        return Results.Json(new { message = "Unauthenticated." }, statusCode: StatusCodes.Status401Unauthorized);

    var current = Math.Max(page ?? 1, 1);
    var size = perPage is > 0 ? perPage.Value : 15;
    var all = alunos.Load();
    Interlocked.Increment(ref resumo.SigaaPagesServed);
    return Results.Ok(new
    {
        current_page = current,
        data = all.Skip((current - 1) * size).Take(size).Select(n => n?.DeepClone()),
        per_page = size,
        total = all.Count,
    });
});

app.MapGet("/api/User/{enrollmentNumber:int}", (int enrollmentNumber) =>
    usuarios.Find(enrollmentNumber) is { } user ? Results.Ok(user) : Results.NotFound());

app.MapPost("/api/User", (List<CreateUserRequest> request) =>
{
    var created = usuarios.Create(request);
    if (created is null)
        return Results.Conflict(new { erro = "Matrícula já cadastrada." });
    Interlocked.Add(ref resumo.UsersCreated, created.Count);
    return Results.Created($"/api/User/{created[0].EnrollmentNumber}", created);
});

app.MapPut("/api/User/{enrollmentNumber:int}", (int enrollmentNumber, UpdateUserRequest request) =>
{
    if (!usuarios.Update(enrollmentNumber, request))
        return Results.NotFound();
    Interlocked.Increment(ref resumo.UsersUpdated);
    return Results.NoContent();
});

app.MapGet("/mock/resumo", () => Results.Ok(new
{
    tokensEmitidos = resumo.TokensIssued,
    paginasSigaaServidas = resumo.SigaaPagesServed,
    usuariosCriados = resumo.UsersCreated,
    usuariosAtualizados = resumo.UsersUpdated,
    usuariosNoArquivo = usuarios.Count,
    arquivoAlunosSigaa = alunos.Path,
    arquivoUsuarios = usuarios.Path,
}));

app.Run();

sealed record TokenRequest(
    [property: JsonPropertyName("grant_type")] string? GrantType,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret,
    [property: JsonPropertyName("scope")] string? Scope);

sealed record CreateUserRequest(
    int EnrollmentNumber, int CourseId, int UserType, string CompleteName,
    int LockNumber, string? LicensePlate, DateTime? CreatedAtUtc);

sealed record UpdateUserRequest(
    int? EnrollmentNumber, int? CourseId, int? UserType, string? CompleteName,
    bool? IsActive, DateTime? UpdatedAtUtc);

sealed record LocalUser(
    int EnrollmentNumber, int CourseId, int UserType, string CompleteName,
    int LockNumber, string LicensePlate, bool IsActive, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

sealed class MockSummary
{
    public int TokensIssued;
    public int SigaaPagesServed;
    public int UsersCreated;
    public int UsersUpdated;
}

sealed class SigaaAlunosFile(string path)
{
    private readonly Lock _lock = new();
    public string Path { get; } = path;

    public JsonArray Load()
    {
        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.WriteAllText(Path, Seed().ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            return JsonNode.Parse(File.ReadAllText(Path))?.AsArray()
                ?? throw new InvalidOperationException($"Arquivo de alunos inválido: {Path}");
        }
    }

    private static JsonArray Seed()
    {
        string[] nomes =
        [
            "Ana Souza", "Bruno Lima", "Carla Mendes", "Diego Rocha", "Eduarda Alves", "Felipe Costa", "Gabriela Nunes",
            "Henrique Dias", "Isabela Ramos", "João Pereira", "Karina Martins", "Lucas Ferreira", "Mariana Gomes",
            "Nicolas Barbosa", "Olívia Cardoso", "Pedro Teixeira", "Quésia Moraes", "Rafael Pinto", "Sofia Ribeiro",
            "Tiago Araújo", "Úrsula Freitas", "Vitor Castro", "Yasmin Correia", "Wagner Duarte", "Xavier Lopes",
            "Beatriz Fonseca", "Caio Moreira", "Daniela Rezende", "Enzo Batista", "Fernanda Vieira", "Gustavo Campos",
            "Helena Machado", "Igor Santana", "Júlia Monteiro", "Leonardo Prado",
        ];
        string[] status = ["ATIVO", "ATIVO", "ATIVO", "ATIVO", "TRANCADO", "ATIVO", "ATIVO"];
        var array = new JsonArray();
        for (var i = 0; i < nomes.Length; i++)
        {
            var cursos = new JsonArray { Vinculo($"{2024300001 + i}", i % 3 == 0 ? 102 : 101, status[i % status.Length]) };
            if (i == 6)
                cursos.Add(Vinculo("2023200007", 101, "ATIVO"));
            array.Add(new JsonObject
            {
                ["login"] = $"{90000000000L + i}",
                ["nome_completo"] = nomes[i],
                ["email"] = $"aluno{i + 1}@exemplo.edu.br",
                ["data_nascimento"] = null,
                ["sexo"] = null,
                ["foto"] = null,
                ["data_cadastro"] = "2024-02-15",
                ["cursos"] = cursos,
            });
        }
        return array;
    }

    private static JsonObject Vinculo(string matricula, int idCurso, string status) => new()
    {
        ["matricula"] = matricula,
        ["nome_curso"] = idCurso == 101 ? "Tecnologia em Análise e Desenvolvimento de Sistemas" : "Técnico em Informática",
        ["cod_curso"] = idCurso == 101 ? "ADS" : "TINF",
        ["nivel"] = idCurso == 101 ? "G" : "T",
        ["nivel_descricao"] = idCurso == 101 ? "GRADUAÇÃO" : "TÉCNICO",
        ["id_curso"] = idCurso,
        ["status_discente"] = status,
    };
}

sealed class LocalUsersFile(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _lock = new();
    private List<LocalUser>? _users;
    public string Path { get; } = path;

    public int Count
    {
        get { lock (_lock) return Users().Count; }
    }

    public LocalUser? Find(int enrollmentNumber)
    {
        lock (_lock) return Users().FirstOrDefault(u => u.EnrollmentNumber == enrollmentNumber);
    }

    public List<LocalUser>? Create(IReadOnlyList<CreateUserRequest> request)
    {
        lock (_lock)
        {
            var users = Users();
            if (request.Count == 0 || request.Any(r => users.Any(u => u.EnrollmentNumber == r.EnrollmentNumber)))
                return null;
            var created = request.Select(r => new LocalUser(r.EnrollmentNumber, r.CourseId, r.UserType, r.CompleteName,
                r.LockNumber, r.LicensePlate ?? "", true, r.CreatedAtUtc ?? DateTime.UtcNow, null)).ToList();
            users.AddRange(created);
            Save(users);
            return created;
        }
    }

    public bool Update(int enrollmentNumber, UpdateUserRequest request)
    {
        lock (_lock)
        {
            var users = Users();
            var index = users.FindIndex(u => u.EnrollmentNumber == enrollmentNumber);
            if (index < 0)
                return false;
            var current = users[index];
            users[index] = current with
            {
                CourseId = request.CourseId ?? current.CourseId,
                UserType = request.UserType ?? current.UserType,
                CompleteName = request.CompleteName ?? current.CompleteName,
                IsActive = request.IsActive ?? current.IsActive,
                UpdatedAtUtc = request.UpdatedAtUtc ?? DateTime.UtcNow,
            };
            Save(users);
            return true;
        }
    }

    private List<LocalUser> Users()
        => _users ??= File.Exists(Path)
            ? JsonSerializer.Deserialize<List<LocalUser>>(File.ReadAllText(Path), JsonOptions) ?? []
            : [];

    private void Save(List<LocalUser> users)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(users, JsonOptions));
        File.Move(temp, Path, overwrite: true);
    }
}
