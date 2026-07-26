using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Core.Abstractions;

public interface IUsageHistoryStore
{
    Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UsageHistoryState state, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
