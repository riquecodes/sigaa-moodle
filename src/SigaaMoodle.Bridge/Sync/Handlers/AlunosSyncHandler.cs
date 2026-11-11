using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Contracts.Internal;
using SigaaMoodle.Bridge.Mapping;

namespace SigaaMoodle.Bridge.Sync.Handlers;

public sealed class AlunosSyncHandler(ISigaaClient sigaa, IInternalAppClient internalApp)
    : ISyncDomainHandler
{
    public SyncDomain Domain => SyncDomain.Alunos;

    public async Task<DomainSyncResult> SyncAsync(DateTimeOffset? since, CancellationToken ct)
    {
        var source = await sigaa.GetAlunosAsync(since, ct);
        var payload = source.SelectMany(AlunoMapper.ToPayloads).ToList();
        var sent = payload.Count > 0 ? await internalApp.SendAlunosAsync(payload, ct) : SendAlunosResult.Empty;
        var error = sent.Errors.Count == 0 ? null
            : $"{sent.Errors.Count} registro(s) recusado(s); os demais foram sincronizados.";
        return new DomainSyncResult(Domain, sent.Errors.Count == 0, source.Count,
            sent.Created, sent.Updated, sent.Unchanged, sent.Skipped, sent.Errors, error);
    }
}
