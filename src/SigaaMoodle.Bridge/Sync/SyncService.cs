using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SigaaMoodle.Bridge.Configuration;
using SigaaMoodle.Bridge.Sync.Handlers;

namespace SigaaMoodle.Bridge.Sync;

public sealed class SyncService(
    IEnumerable<ISyncDomainHandler> handlers,
    ISyncStateProvider stateProvider,
    ISyncStatusStore statusStore,
    ILogger<SyncService> logger,
    IOptions<SyncOptions> options,
    SyncExecutionGate executionGate) : ISyncService
{
    public async Task<SyncRunResult> RunAsync(
        IReadOnlyCollection<SyncDomain>? domains, CancellationToken ct)
    {
        var enabled = options.Value.EnabledDomains.Select(d => Enum.Parse<SyncDomain>(d, true)).ToHashSet();
        if (domains is not null && domains.Any(d => !enabled.Contains(d)))
            throw new ArgumentException("Domínio desabilitado em Sync:EnabledDomains.", nameof(domains));
        await executionGate.Semaphore.WaitAsync(ct);
        try
        {
            return await RunSelectedAsync(domains ?? enabled, ct);
        }
        finally
        {
            executionGate.Semaphore.Release();
        }
    }

    private async Task<SyncRunResult> RunSelectedAsync(IReadOnlyCollection<SyncDomain> domains, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var selected = handlers
            .Where(h => domains.Contains(h.Domain))
            .ToList();

        var results = new List<DomainSyncResult>(selected.Count);
        foreach (var handler in selected)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var since = await stateProvider.GetSinceAsync(handler.Domain, ct);
                var result = await handler.SyncAsync(since, ct);
                results.Add(result);
                if (result.Success)
                    await stateProvider.SetLastSyncedAtAsync(handler.Domain, startedAt, ct);
                logger.LogInformation("Sync {Domain}: lidos={Read} enviados={Sent}",
                    handler.Domain, result.Read, result.Sent);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao sincronizar domínio {Domain}", handler.Domain);
                results.Add(new DomainSyncResult(handler.Domain, false, 0, 0, ex.Message));
            }
        }

        var runResult = new SyncRunResult(startedAt, DateTimeOffset.UtcNow, results);
        statusStore.Set(runResult);
        return runResult;
    }
}
