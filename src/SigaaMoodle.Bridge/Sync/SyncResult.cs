namespace SigaaMoodle.Bridge.Sync;

public sealed record DomainSyncResult(
    SyncDomain Domain, bool Success, int Read, int Sent, string? Error);

public sealed record SyncRunResult(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    IReadOnlyList<DomainSyncResult> Domains)
{
    public bool AllSucceeded => Domains.All(d => d.Success);
}
