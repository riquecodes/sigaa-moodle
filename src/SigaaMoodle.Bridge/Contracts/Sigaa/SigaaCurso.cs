using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Sigaa;

// A DEFINIR: campos reais do SIGAA. Placeholder mínimo plausível. Histórico: substituído pelo contrato do Swagger fornecido.
public sealed record SigaaCurso(
    [property: JsonPropertyName("id_curso")] int Id,
    [property: JsonPropertyName("codigo")] string Codigo,
    [property: JsonPropertyName("curso")] string Nome,
    [property: JsonPropertyName("modalidade_educacao")] string? Modalidade);
