namespace CodexUsage.Core.UsageHistory;

public sealed record UsageHistoryMetrics(
    int ComparableWindowCount,
    double? AveragePeakObservedPercent,
    double? HighestPeakObservedPercent,
    int Reached80PercentCount,
    int Reached95PercentCount,
    int EarlyResetCount);
