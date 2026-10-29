using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Internal;

// Payload mínimo enviado à app interna. JSON em pt-BR.
public sealed record AlunoPayload(
    [property: JsonPropertyName("matricula")] string Matricula,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("id_curso_sigaa")] int? IdCursoSigaa);
