using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Mapping;

public static class NotaMapper
{
    // descarta 'Situacao' até o destino pedir.
    public static IEnumerable<NotaPayload> ToPayloads(SigaaNota n)
        => (n.Disciplinas ?? []).Where(d => d.MediaFinal.HasValue).Select(d => new NotaPayload(
            n.MatriculaAluno, d.IdTurma.ToString(System.Globalization.CultureInfo.InvariantCulture), d.MediaFinal!.Value));
}
