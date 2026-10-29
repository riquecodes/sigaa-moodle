using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Sigaa;

// A DEFINIR: campos reais do SIGAA. Placeholder mínimo plausível. Histórico: substituído pelo contrato do Swagger fornecido.
public sealed record SigaaNota(
    [property: JsonPropertyName("matricula")] string MatriculaAluno,
    [property: JsonPropertyName("disciplinas")] IReadOnlyList<SigaaDesempenhoDisciplina>? Disciplinas);

public sealed record SigaaDesempenhoDisciplina(
    [property: JsonPropertyName("id_turma")] int IdTurma,
    [property: JsonPropertyName("media_final")] decimal? MediaFinal,
    [property: JsonPropertyName("situacao_matricula")] string Situacao);
