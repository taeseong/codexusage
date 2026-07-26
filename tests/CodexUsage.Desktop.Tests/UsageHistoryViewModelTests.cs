using CodexUsage.Core.Abstractions;
using CodexUsage.Core.UsageHistory;
using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Desktop.Tests;

public sealed class UsageHistoryViewModelTests
{
    [Fact]
    public async Task InitializeAsync_GroupsTheLatestTwelveWindowsByCalendarMonth()
    {
        var july = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var june = july.AddMonths(-1);
        var viewModel = new UsageHistoryViewModel(new HistoryStore(
            new UsageHistoryState
            {
                Windows =
                [
                    Entry(july, 40, "PRO"),
                    Entry(july.AddDays(-3), 80, "PLUS"),
                    Entry(june, 20),
                ],
            }));

        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.MonthlyGroups.Count);
        var julyGroup = viewModel.MonthlyGroups[0];
        Assert.Equal("Jul 2026", julyGroup.MonthDisplayText);
        Assert.Equal(60, julyGroup.AveragePeakObservedPercent);
        Assert.Equal(80, julyGroup.HighestPeakObservedPercent);
        Assert.Equal("Plan unavailable", viewModel.MonthlyGroups[1].Entries.Single().PlanDisplayText);
    }

    [Fact]
    public async Task InitializeAsync_KeepsMultipleEarlyResetsAsIndependentRows()
    {
        var month = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var viewModel = new UsageHistoryViewModel(new HistoryStore(new UsageHistoryState
        {
            Windows =
            [
                Entry(month, 100, closure: UsageWindowClosureKind.EarlyResetObserved),
                Entry(month.AddDays(-2), 0, closure: UsageWindowClosureKind.EarlyResetObserved),
            ],
        }));

        await viewModel.InitializeAsync();

        var entries = Assert.Single(viewModel.MonthlyGroups).Entries;
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal("Early reset observed", entry.ClosureDisplayText));
    }

    private static WeeklyUsageWindowEntry Entry(
        DateTimeOffset at,
        double peak,
        string? plan = null,
        UsageWindowClosureKind closure = UsageWindowClosureKind.InProgress)
    {
        return new WeeklyUsageWindowEntry
        {
            LimitId = "weekly",
            WindowInstanceId = Guid.NewGuid().ToString("N"),
            FirstObservedAt = at,
            LastObservedAt = at.AddDays(2),
            PeakObservedPercent = peak,
            LastObservedPercent = peak,
            ObservedDayCount = 2,
            ClosureKind = closure,
            ObservedPlans = plan is null ? [] : [plan],
        };
    }

    private sealed class HistoryStore(UsageHistoryState state) : IUsageHistoryStore
    {
        public Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task SaveAsync(UsageHistoryState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
