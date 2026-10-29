using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Internal;

// Payload mínimo enviado à app interna. JSON em pt-BR.
public sealed record NotaPayload(
    [property: JsonPropertyName("matricula_aluno")] string MatriculaAluno,
    [property: JsonPropertyName("codigo_turma")] string CodigoTurma,
    [property: JsonPropertyName("valor")] decimal Valor);
