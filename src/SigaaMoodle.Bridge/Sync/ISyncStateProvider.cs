namespace SigaaMoodle.Bridge.Sync;

// Define a janela de leitura por domínio. Null = traz tudo (sem filtro 'since').
public interface ISyncStateProvider
{
    Task<DateTimeOffset?> GetSinceAsync(SyncDomain domain, CancellationToken ct);
    Task SetLastSyncedAtAsync(SyncDomain domain, DateTimeOffset at, CancellationToken ct);
}
