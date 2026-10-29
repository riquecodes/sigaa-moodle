using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Sigaa;

// A DEFINIR: campos reais do SIGAA. Placeholder mínimo plausível. Histórico: substituído pelo contrato do Swagger fornecido.
public sealed record SigaaMatricula(
    [property: JsonPropertyName("matricula")] string MatriculaAluno,
    [property: JsonPropertyName("disciplinas")] IReadOnlyList<SigaaDisciplinaMatriculada>? Disciplinas);

public sealed record SigaaDisciplinaMatriculada(
    [property: JsonPropertyName("id_turma")] int IdTurma,
    [property: JsonPropertyName("situacao_matricula")] string Situacao);
