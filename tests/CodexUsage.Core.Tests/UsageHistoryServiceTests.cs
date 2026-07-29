using CodexUsage.Core.Usage;
using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Core.Tests;

public sealed class UsageHistoryServiceTests
{
    private readonly UsageHistoryService _service = new();

    [Fact]
    public void FirstObservationCreatesActiveWindow()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        Assert.True(_service.Observe(new UsageHistoryState(), Snapshot(now, 40, now.AddDays(7)), out var state));
        var entry = Assert.Single(state.Windows);
        Assert.Equal(40, entry.PeakObservedPercent);
        Assert.Equal(UsageWindowClosureKind.InProgress, entry.ClosureKind);
    }

    [Fact]
    public void UsageDecreaseAloneDoesNotCreateNewWindowOrLowerPeak()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 70, now.AddDays(7)), out var state);
        _service.Observe(state, Snapshot(now.AddHours(1), 20, now.AddDays(7)), out state);
        var entry = Assert.Single(state.Windows);
        Assert.Equal(70, entry.PeakObservedPercent);
        Assert.Equal(20, entry.LastObservedPercent);
    }

    [Fact]
    public void SmallResetScheduleCorrectionDoesNotCreateNewWindow()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 40, now.AddDays(7)), out var state);
        _service.Observe(state, Snapshot(now.AddMinutes(1), 41, now.AddDays(7).AddMinutes(2)), out state);
        Assert.Single(state.Windows);
        Assert.Equal(UsageWindowClosureKind.InProgress, state.Windows.Single().ClosureKind);
    }

    [Fact]
    public void EarlierCalculatedStartClosesAsEarlyReset()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 70, now.AddDays(7)), out var state);
        var rolloverAt = now.AddDays(2);
        _service.Observe(state, Snapshot(rolloverAt, 5, rolloverAt.AddDays(7)), out state);
        Assert.Equal(2, state.Windows.Count);
        Assert.Equal(UsageWindowClosureKind.EarlyResetObserved, state.Windows[0].ClosureKind);
    }

    [Fact]
    public void NormalRolloverCreatesSeparateWindow()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 80, now.AddDays(7)), out var state);
        _service.Observe(state, Snapshot(now.AddDays(7), 2, now.AddDays(14)), out state);
        Assert.Equal(UsageWindowClosureKind.NormalResetObserved, state.Windows[0].ClosureKind);
        Assert.Equal(UsageWindowClosureKind.InProgress, state.Windows[1].ClosureKind);
    }

    [Fact]
    public void FullNextScheduleRolloverIsDetectedEvenWhenUsageIsUnchanged()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 40, now.AddDays(7)), out var state);
        _service.Observe(state, Snapshot(now.AddDays(6).AddHours(23).AddMinutes(59), 40, now.AddDays(7)), out state);
        _service.Observe(state, Snapshot(now.AddDays(7), 40, now.AddDays(14)), out state);
        Assert.Equal(2, state.Windows.Count);
        Assert.Equal(UsageWindowClosureKind.NormalResetObserved, state.Windows[0].ClosureKind);
    }

    [Fact]
    public void PlanChangesArePreserved()
    {
        var now = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        _service.Observe(new UsageHistoryState(), Snapshot(now, 10, now.AddDays(7), "pro"), out var state);
        _service.Observe(state, Snapshot(now.AddHours(1), 20, now.AddDays(7), "plus"), out state);
        Assert.Equal(["PRO", "PLUS"], state.Windows.Single().ObservedPlans);
    }

    [Fact]
    public void MissingPlanUsesSafeDisplayFallback()
    {
        var entry = new WeeklyUsageWindowEntry
        {
            LimitId = "weekly",
            WindowInstanceId = "window",
            FirstObservedAt = DateTimeOffset.UtcNow,
            LastObservedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal("Plan unavailable", entry.PlanDisplayText);
        Assert.Equal("Observed 0 days", entry.ObservedDaysText);
    }

    [Fact]
    public void HistoryDisplayUsesSingularDurationAndObservationLabels()
    {
        var at = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        var entry = new WeeklyUsageWindowEntry
        {
            LimitId = "weekly",
            WindowInstanceId = "window",
            CalculatedWindowStartedAt = at,
            FirstObservedAt = at,
            LastObservedAt = at.AddHours(1),
            ObservedDayCount = 1,
        };

        Assert.Equal("Window 1 hour", entry.ObservedPeriodText);
        Assert.Equal("Observed 1 day", entry.ObservedDaysText);
    }

    [Fact]
    public void HistoryDisplay_MarksIncompleteAndNonNormalWindowsAsPartialObservations()
    {
        var at = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);
        var incompleteNormal = Entry(
            at,
            UsageWindowClosureKind.NormalResetObserved,
            observedDays: 3);
        var completeNormal = Entry(
            at,
            UsageWindowClosureKind.NormalResetObserved,
            observedDays: 7);
        var earlyReset = Entry(
            at,
            UsageWindowClosureKind.EarlyResetObserved,
            observedDays: 7);

        Assert.True(incompleteNormal.IsPartialObservation);
        Assert.Equal(
            "Observed 3 days · Partial observation",
            incompleteNormal.ObservationSummaryText);
        Assert.False(completeNormal.IsPartialObservation);
        Assert.Equal("Observed 7 days", completeNormal.ObservationSummaryText);
        Assert.True(earlyReset.IsPartialObservation);
        Assert.Contains(
            "Partial observation",
            earlyReset.AccessibilitySummaryText,
            StringComparison.Ordinal);
    }

    private static WeeklyUsageWindowEntry Entry(
        DateTimeOffset at,
        UsageWindowClosureKind closure,
        int observedDays) =>
        new()
        {
            LimitId = "weekly",
            WindowInstanceId = "window",
            CalculatedWindowStartedAt = at,
            InitialScheduledResetAt = at.AddDays(7),
            FirstObservedAt = at,
            LastObservedAt = at.AddDays(7),
            ActualResetObservedAt = at.AddDays(7),
            ClosureKind = closure,
            ObservedDayCount = observedDays,
            PeakObservedPercent = 50,
        };

    private static CodexUsageSnapshot Snapshot(DateTimeOffset at, double used, DateTimeOffset reset, string? plan = "pro") => new()
    {
        RetrievedAt = at,
        AccountPlan = plan,
        Limits = [new UsageLimit("weekly", "Weekly", UsageLimitKind.Weekly, used, TimeSpan.FromDays(7), reset)],
    };
}
