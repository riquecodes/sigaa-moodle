namespace SigaaMoodle.Bridge.Sync;

// Guarda o último resultado de sincronização em memória (a ponte não tem banco).
public sealed class InMemorySyncStatusStore : ISyncStatusStore
{
    private volatile SyncRunResult? _last;
    public SyncRunResult? Last => _last;
    public void Set(SyncRunResult result) => _last = result;
}
