using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Clients.Internal;

public interface IInternalAppClient
{
    Task<SendAlunosResult> SendAlunosAsync(IReadOnlyList<AlunoPayload> payload, CancellationToken ct);
}
