using SigaaMoodle.Bridge.Contracts.Internal;

namespace SigaaMoodle.Bridge.Sync;

public sealed record DomainSyncResult(
    SyncDomain Domain, bool Success, int Read, int Created, int Updated, int Unchanged, int Skipped,
    IReadOnlyList<RecordError> Errors, string? Error)
{
    public IReadOnlyList<RecordError> Errors { get; init; } = Errors ?? [];

    public int Failed => Errors.Count;

    public static DomainSyncResult Failure(SyncDomain domain, string error)
        => new(domain, false, 0, 0, 0, 0, 0, [], error);
}

public sealed record SyncRunResult(
    DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    IReadOnlyList<DomainSyncResult> Domains)
{
    public bool AllSucceeded => Domains.All(d => d.Success);
}
