namespace SigaaMoodle.Bridge.Sync;

public interface ISyncService
{
    // domains = null => todos os handlers registrados.
    Task<SyncRunResult> RunAsync(IReadOnlyCollection<SyncDomain>? domains, CancellationToken ct);
}
