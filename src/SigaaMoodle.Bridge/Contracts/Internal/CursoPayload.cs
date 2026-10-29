using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Internal;

// Payload mínimo enviado à app interna. JSON em pt-BR.
public sealed record CursoPayload(
    [property: JsonPropertyName("codigo")] string Codigo,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("id_curso_sigaa")] int IdCursoSigaa);
