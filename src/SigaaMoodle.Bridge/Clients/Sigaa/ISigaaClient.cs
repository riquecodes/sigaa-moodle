using SigaaMoodle.Bridge.Contracts.Sigaa;

namespace SigaaMoodle.Bridge.Clients.Sigaa;

public interface ISigaaClient
{
    Task<IReadOnlyList<SigaaAluno>> GetAlunosAsync(DateTimeOffset? since, CancellationToken ct);
}
