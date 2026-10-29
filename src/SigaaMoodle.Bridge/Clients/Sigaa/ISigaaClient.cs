using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Clients.Sigaa;

public interface ISigaaClient
{
    Task<IReadOnlyList<SigaaAluno>> GetAlunosAsync(DateTimeOffset? since, CancellationToken ct);
    Task<IReadOnlyList<SigaaMatricula>> GetMatriculasAsync(DateTimeOffset? since, CancellationToken ct);
    Task<IReadOnlyList<SigaaCurso>> GetCursosAsync(DateTimeOffset? since, CancellationToken ct);
    Task<IReadOnlyList<SigaaNota>> GetNotasAsync(DateTimeOffset? since, CancellationToken ct);
}
