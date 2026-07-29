using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Abstractions;

public interface IUsageSnapshotCache
{
    Task<CodexUsageSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        CodexUsageSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
