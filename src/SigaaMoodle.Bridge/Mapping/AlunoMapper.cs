using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Mapping;

public static class AlunoMapper
{
    // Envia só o necessário: descarta 'Ativo' (não usado pelo destino — A DEFINIR se precisar).
    public static IEnumerable<AlunoPayload> ToPayloads(SigaaAluno aluno)
        => (aluno.Cursos ?? []).Select(v => new AlunoPayload(v.Matricula, aluno.Nome, aluno.Email, v.IdCurso));
}
