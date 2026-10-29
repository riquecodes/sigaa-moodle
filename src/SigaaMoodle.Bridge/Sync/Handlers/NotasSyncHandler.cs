using SigaaMoodle.Bridge.Clients.Internal;
using SigaaMoodle.Bridge.Clients.Sigaa;
using SigaaMoodle.Bridge.Mapping;

namespace SigaaMoodle.Bridge.Sync.Handlers;

public sealed class NotasSyncHandler(ISigaaClient sigaa, IInternalAppClient internalApp)
    : ISyncDomainHandler
{
    public SyncDomain Domain => SyncDomain.Notas;

    public async Task<DomainSyncResult> SyncAsync(DateTimeOffset? since, CancellationToken ct)
    {
        var source = await sigaa.GetNotasAsync(since, ct);
        var payload = source.SelectMany(NotaMapper.ToPayloads).ToList();
        if (payload.Count > 0)
            await internalApp.SendNotasAsync(payload, ct);
        return new DomainSyncResult(Domain, Success: true, Read: source.Count, Sent: payload.Count, Error: null);
    }
}
