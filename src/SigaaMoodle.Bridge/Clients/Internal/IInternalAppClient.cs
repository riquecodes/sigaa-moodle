using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Clients.Internal;

public interface IInternalAppClient
{
    Task SendAlunosAsync(IReadOnlyList<AlunoPayload> payload, CancellationToken ct);
    Task SendMatriculasAsync(IReadOnlyList<MatriculaPayload> payload, CancellationToken ct);
    Task SendCursosAsync(IReadOnlyList<CursoPayload> payload, CancellationToken ct);
    Task SendNotasAsync(IReadOnlyList<NotaPayload> payload, CancellationToken ct);
}
