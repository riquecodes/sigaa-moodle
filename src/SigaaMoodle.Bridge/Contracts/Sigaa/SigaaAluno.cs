using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Sigaa;

// A DEFINIR: campos reais do SIGAA. Placeholder mínimo plausível. Histórico: substituído pelo contrato do Swagger fornecido.
// Contrato documentado: a matrícula pertence ao vínculo em cursos[], não à pessoa.
public sealed record SigaaAluno(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("nome_completo")] string Nome,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("cursos")] IReadOnlyList<SigaaVinculo>? Cursos);

public sealed record SigaaVinculo(
    [property: JsonPropertyName("matricula")] string Matricula,
    [property: JsonPropertyName("id_curso")] int? IdCurso,
    [property: JsonPropertyName("status_discente")] string? Status);
