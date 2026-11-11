using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Mapping;

public static class AlunoMapper
{
    public static IEnumerable<AlunoPayload> ToPayloads(SigaaAluno aluno)
        => (aluno.Cursos ?? []).Select(v => new AlunoPayload(v.Matricula, aluno.Nome, aluno.Email, v.IdCurso, v.Status));
}
