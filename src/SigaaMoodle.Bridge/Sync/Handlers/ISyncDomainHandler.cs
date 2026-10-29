namespace SigaaMoodle.Bridge.Sync.Handlers;

public interface ISyncDomainHandler
{
    SyncDomain Domain { get; }
    Task<DomainSyncResult> SyncAsync(DateTimeOffset? since, CancellationToken ct);
}
