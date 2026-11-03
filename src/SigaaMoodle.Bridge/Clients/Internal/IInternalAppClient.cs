using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Clients.Internal;

public interface IInternalAppClient
{
    Task SendAlunosAsync(IReadOnlyList<AlunoPayload> payload, CancellationToken ct);
}
