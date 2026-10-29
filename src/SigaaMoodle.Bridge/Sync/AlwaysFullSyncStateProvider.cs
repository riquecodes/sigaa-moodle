namespace SigaaMoodle.Bridge.Sync;

// Implementação ATUAL: sempre traz tudo (sem estado). Trocar por uma impl de delta no futuro,
// quando o SIGAA suportar filtro 'since' e Sync:DeltaEnabled estiver true.
public sealed class AlwaysFullSyncStateProvider : ISyncStateProvider
{
    public Task<DateTimeOffset?> GetSinceAsync(SyncDomain domain, CancellationToken ct)
        => Task.FromResult<DateTimeOffset?>(null);

    public Task SetLastSyncedAtAsync(SyncDomain domain, DateTimeOffset at, CancellationToken ct)
        => Task.CompletedTask;
}
