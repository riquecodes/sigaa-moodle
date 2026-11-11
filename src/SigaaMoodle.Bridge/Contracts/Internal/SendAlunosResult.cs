using System.Text.Json.Serialization;

namespace SigaaMoodle.Bridge.Contracts.Internal;

public sealed record SendAlunosResult(int Created, int Updated, int Unchanged, int Skipped, IReadOnlyList<RecordError> Errors)
{
    public static readonly SendAlunosResult Empty = new(0, 0, 0, 0, []);
}

public sealed record RecordError(
    [property: JsonPropertyName("matricula")] string Matricula,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("motivo")] string Motivo);
