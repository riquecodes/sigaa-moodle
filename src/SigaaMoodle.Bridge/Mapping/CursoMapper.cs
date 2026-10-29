using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Mapping;

public static class CursoMapper
{
    // descarta 'Modalidade' até o destino pedir.
    public static CursoPayload ToPayload(SigaaCurso c)
        => new(c.Codigo, c.Nome, c.Id);
}
