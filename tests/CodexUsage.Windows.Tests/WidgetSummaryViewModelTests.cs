using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;
using CodexUsage.Desktop.ViewModels;
using CodexUsage.Windows.ViewModels;

namespace CodexUsage.Windows.Tests;

public sealed class WidgetSummaryViewModelTests
{
    [Fact]
    public void SummaryBeforeFirstRefreshShowsLoadingState()
    {
        var usageViewModel = new UsageViewModel(new StubUsageProvider());
        using var viewModel = new WidgetSummaryViewModel(usageViewModel);

        Assert.Equal("Checking usage…", viewModel.SummaryText);
    }

    [Fact]
    public async Task WeeklySnapshotWithNoShortTermLimitShowsUnlimitedShortTermSummary()
    {
        var usageViewModel = new UsageViewModel(
            new StubUsageProvider(Success(37d)));
        using var viewModel = new WidgetSummaryViewModel(usageViewModel);

        await usageViewModel.RefreshAsync();

        Assert.Equal("5H ∞  |  W 37%", viewModel.SummaryText);
        Assert.Equal("5H", viewModel.LeadingSummaryText);
        Assert.Equal("∞", viewModel.UnlimitedShortTermText);
        Assert.Equal("|", viewModel.SummaryDividerText);
        Assert.Equal("W 37%", viewModel.TrailingSummaryText);
        Assert.True(viewModel.ShowWeeklyProgress);
        Assert.Equal(10, viewModel.WeeklyProgressSegments.Count);
        Assert.Equal(3, viewModel.WeeklyProgressSegments.Count(segment => segment.IsFilled));
        Assert.DoesNotContain(viewModel.WeeklyProgressSegments, segment => segment.IsWarning || segment.IsCritical);
        Assert.Contains("W 37%", viewModel.ToolTip, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(83d, true, false)]
    [InlineData(96d, false, true)]
    public async Task WeeklyGraphUsesWarningAndCriticalStatesAtHighUsage(
        double usedPercent,
        bool expectedWarning,
        bool expectedCritical)
    {
        var usageViewModel = new UsageViewModel(new StubUsageProvider(Success(usedPercent)));
        using var viewModel = new WidgetSummaryViewModel(usageViewModel);

        await usageViewModel.RefreshAsync();

        Assert.Contains(
            viewModel.WeeklyProgressSegments.Where(segment => segment.IsFilled),
            segment => segment.IsWarning == expectedWarning && segment.IsCritical == expectedCritical);
    }

    [Fact]
    public async Task AuthenticationFailureShowsActionableStateInsteadOfZeroPercent()
    {
        var usageViewModel = new UsageViewModel(
            new StubUsageProvider(new CodexUsageResult
            {
                Status = CodexUsageStatus.NotAuthenticated,
            }));
        using var viewModel = new WidgetSummaryViewModel(usageViewModel);

        await usageViewModel.RefreshAsync();

        Assert.Equal("Sign in to Codex", viewModel.SummaryText);
        Assert.DoesNotContain("0%", viewModel.SummaryText, StringComparison.Ordinal);
        Assert.True(viewModel.HasNotice);
    }

    [Fact]
    public async Task RefreshFailureHidesGraphWhileKeepingTheLastWeeklyValueMarkedAsStale()
    {
        var usageViewModel = new UsageViewModel(
            new StubUsageProvider(
                Success(37d),
                new CodexUsageResult { Status = CodexUsageStatus.TimedOut }));
        using var viewModel = new WidgetSummaryViewModel(usageViewModel);

        await usageViewModel.RefreshAsync();
        await usageViewModel.RefreshAsync();

        Assert.Contains("W 37%", viewModel.TrailingSummaryText, StringComparison.Ordinal);
        Assert.Contains("Stale", viewModel.TrailingSummaryText, StringComparison.Ordinal);
        Assert.False(viewModel.ShowWeeklyProgress);
    }

    private static CodexUsageResult Success(double usedPercent) =>
        new()
        {
            Status = CodexUsageStatus.Success,
            Snapshot = new CodexUsageSnapshot
            {
                RetrievedAt = DateTimeOffset.UtcNow,
                Limits =
                [
                    new UsageLimit(
                        "weekly",
                        "Weekly",
                        UsageLimitKind.Weekly,
                        usedPercent,
                        TimeSpan.FromDays(7),
                        DateTimeOffset.UtcNow.AddDays(6)),
                ],
            },
        };

    private sealed class StubUsageProvider(params CodexUsageResult[] results)
        : ICodexUsageProvider
    {
        private readonly Queue<CodexUsageResult> _results = new(results);

        public Task<CodexUsageResult> GetUsageAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.Dequeue());
    }
}
