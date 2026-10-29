namespace SigaaMoodle.Bridge.Sync;

public interface ISyncStatusStore
{
    SyncRunResult? Last { get; }
    void Set(SyncRunResult result);
}
