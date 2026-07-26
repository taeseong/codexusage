namespace CodexUsage.Core.UsageHistory;

public sealed record UsageHistoryState
{
    public const int CurrentVersion = 1;
    public int Version { get; init; } = CurrentVersion;
    public IReadOnlyList<WeeklyUsageWindowEntry> Windows { get; init; } = [];
}
