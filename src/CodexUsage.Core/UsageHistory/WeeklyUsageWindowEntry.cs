namespace CodexUsage.Core.UsageHistory;

/// <summary>Local observations for one server-side weekly limit window. Values are observations, not total usage.</summary>
public sealed record WeeklyUsageWindowEntry
{
    public required string LimitId { get; init; }
    public required string WindowInstanceId { get; init; }
    public DateTimeOffset? CalculatedWindowStartedAt { get; init; }
    public DateTimeOffset? InitialScheduledResetAt { get; init; }
    public DateTimeOffset? LastScheduledResetAt { get; init; }
    public DateTimeOffset? ActualResetObservedAt { get; init; }
    public UsageWindowClosureKind ClosureKind { get; init; } = UsageWindowClosureKind.InProgress;
    public double PeakObservedPercent { get; init; }
    public double LastObservedPercent { get; init; }
    public int ObservedDayCount { get; init; }
    public DateTimeOffset FirstObservedAt { get; init; }
    public DateTimeOffset LastObservedAt { get; init; }
    public IReadOnlyList<string> ObservedPlans { get; init; } = [];

    public string PlanDisplayText => ObservedPlans.Count == 0
        ? "Plan unavailable"
        : string.Join(" → ", ObservedPlans);

    public string ClosureDisplayText => ClosureKind switch
    {
        UsageWindowClosureKind.InProgress => "In progress",
        UsageWindowClosureKind.EarlyResetObserved => "Early reset observed",
        UsageWindowClosureKind.ResetTimingUncertain => "Reset timing uncertain",
        _ => "Reset observed",
    };

    public string ObservedPeriodText
    {
        get
        {
            var end = ActualResetObservedAt ?? LastObservedAt;
            var start = CalculatedWindowStartedAt ?? FirstObservedAt;
            var duration = end - start;
            return duration.TotalDays >= 1
                ? $"Window {Math.Max(1, (int)Math.Floor(duration.TotalDays))} days"
                : $"Window {Math.Max(1, (int)Math.Ceiling(duration.TotalHours))} hours";
        }
    }

    public string DateRangeText
    {
        get
        {
            var start = (CalculatedWindowStartedAt ?? FirstObservedAt).ToLocalTime();
            var end = (ActualResetObservedAt ?? LastObservedAt).ToLocalTime();
            return start.Date == end.Date
                ? start.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)
                : $"{start.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)} – {end.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
