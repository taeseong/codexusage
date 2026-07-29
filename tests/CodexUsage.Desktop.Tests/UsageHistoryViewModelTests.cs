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
        Assert.Equal(2, viewModel.MonthlyGroups.Single().EarlyResetCount);
    }

    [Fact]
    public async Task MonthCommands_NavigateOlderAndNewerGroups()
    {
        var july = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var viewModel = new UsageHistoryViewModel(new HistoryStore(new UsageHistoryState
        {
            Windows = [Entry(july, 40), Entry(july.AddMonths(-1), 20)],
        }));
        await viewModel.InitializeAsync();

        Assert.Equal("Jul 2026", viewModel.SelectedMonthGroup?.MonthDisplayText);
        viewModel.ShowOlderMonthCommand.Execute(null);
        Assert.Equal("Jun 2026", viewModel.SelectedMonthGroup?.MonthDisplayText);
        viewModel.ShowNewerMonthCommand.Execute(null);
        Assert.Equal("Jul 2026", viewModel.SelectedMonthGroup?.MonthDisplayText);
    }

    [Fact]
    public async Task SelectedMonthGroup_AllowsDirectMonthSelection()
    {
        var july = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var viewModel = new UsageHistoryViewModel(new HistoryStore(new UsageHistoryState
        {
            Windows =
            [
                Entry(july, 40),
                Entry(july.AddMonths(-1), 30),
                Entry(july.AddMonths(-2), 20),
            ],
        }));
        await viewModel.InitializeAsync();

        viewModel.SelectedMonthGroup = viewModel.MonthlyGroups[2];

        Assert.Equal("May 2026", viewModel.SelectedMonthGroup?.MonthDisplayText);
        Assert.True(viewModel.CanShowNewerMonth);
        Assert.False(viewModel.CanShowOlderMonth);
    }

    [Fact]
    public async Task RetryHistory_ReloadsAfterAnInitialStoreFailure()
    {
        var at = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var store = new RecoveringHistoryStore(new UsageHistoryState { Windows = [Entry(at, 40)] });
        var viewModel = new UsageHistoryViewModel(store);

        await viewModel.InitializeAsync();
        Assert.True(viewModel.IsUnavailable);

        await viewModel.RetryHistoryCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsUnavailable);
        Assert.Equal("Jul 2026", viewModel.SelectedMonthGroup?.MonthDisplayText);
    }

    [Fact]
    public async Task EmptyOrUnavailableHistory_DoesNotShowInsufficientComparableMessage()
    {
        var empty = new UsageHistoryViewModel(
            new HistoryStore(new UsageHistoryState()));
        await empty.InitializeAsync();

        var unavailable = new UsageHistoryViewModel(
            new AlwaysFailingHistoryStore());
        await unavailable.InitializeAsync();

        Assert.False(empty.HasInsufficientComparableHistory);
        Assert.False(unavailable.HasInsufficientComparableHistory);
        Assert.True(unavailable.IsUnavailable);
    }

    [Fact]
    public async Task ComparableMetrics_IncludeTheNumberOfEligibleWindows()
    {
        var at = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        var viewModel = new UsageHistoryViewModel(new HistoryStore(new UsageHistoryState
        {
            Windows =
            [
                Entry(at, 40, closure: UsageWindowClosureKind.NormalResetObserved),
                Entry(at.AddDays(-7), 80, closure: UsageWindowClosureKind.NormalResetObserved),
                Entry(at.AddDays(-14), 100, closure: UsageWindowClosureKind.NormalResetObserved),
            ],
        }));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasComparableHistory);
        Assert.StartsWith("3 comparable windows · ", viewModel.MetricsText, StringComparison.Ordinal);
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

    private sealed class RecoveringHistoryStore(UsageHistoryState state) : IUsageHistoryStore
    {
        private int _loadCount;

        public Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _loadCount) == 1)
            {
                throw new IOException("Simulated read failure.");
            }

            return Task.FromResult(state);
        }

        public Task SaveAsync(UsageHistoryState state, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class AlwaysFailingHistoryStore : IUsageHistoryStore
    {
        public Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated read failure.");

        public Task SaveAsync(
            UsageHistoryState state,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated write failure.");

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated clear failure.");
    }
}
