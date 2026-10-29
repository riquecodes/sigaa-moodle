namespace SigaaMoodle.Bridge.Sync;

// Compartilhado entre disparos manuais e worker; evita criação concorrente no mesmo processo.
public sealed class SyncExecutionGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
