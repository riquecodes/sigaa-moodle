using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Mapping;

public static class MatriculaMapper
{
    public static IEnumerable<MatriculaPayload> ToPayloads(SigaaMatricula m)
        => (m.Disciplinas ?? []).Select(d => new MatriculaPayload(
            m.MatriculaAluno, d.IdTurma.ToString(System.Globalization.CultureInfo.InvariantCulture), d.Situacao));
}
