using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;
using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Desktop.Tests;

public sealed class UsageViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 22, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshAsync_ShowsWeeklyLimitAndMarksMissingShortTerm()
    {
        // Given
        var provider = new QueueUsageProvider(Success(1d));
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal("1%", viewModel.Weekly.UsedText);
        Assert.Equal("99%", viewModel.Weekly.RemainingText);
        Assert.Equal("Not reported", viewModel.ShortTerm.AvailabilityText);
        Assert.Equal("W 1%", viewModel.MenuSummary);
    }

    [Fact]
    public async Task RefreshAsync_PreservesLastGoodValuesWhenNextLookupFails()
    {
        // Given
        var provider = new QueueUsageProvider(
            Success(37d),
            new CodexUsageResult { Status = CodexUsageStatus.TimedOut });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        await viewModel.RefreshAsync();

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal("37%", viewModel.Weekly.UsedText);
        Assert.True(viewModel.HasNotice);
        Assert.True(viewModel.IsShowingStaleData);
        Assert.True(viewModel.IsWarningNotice);
        Assert.Equal("W 37% · Stale", viewModel.MenuSummary);
        Assert.Contains("Stale", viewModel.TrayToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForActiveRefreshBeforeDisposingSynchronization()
    {
        // Given
        var provider = new CancellationPausingProvider();
        var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        var refresh = viewModel.RefreshAsync();
        await provider.Started.Task;

        // When
        var dispose = viewModel.DisposeAsync().AsTask();
        provider.AllowCancellationResult.SetResult();

        // Then
        await Task.WhenAll(refresh, dispose);
    }

    [Fact]
    public async Task RefreshAsync_MarksInitialAuthenticationFailureAsCompletedState()
    {
        // Given
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.NotAuthenticated });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal("Sign in required", viewModel.ShortTerm.AvailabilityText);
        Assert.Equal("Sign in required", viewModel.Weekly.AvailabilityText);
        Assert.Equal("Sign in required", viewModel.AccountPlanText);
        Assert.StartsWith("Lookup failed ", viewModel.LastUpdatedText, StringComparison.Ordinal);
        Assert.False(viewModel.IsWarningNotice);
    }

    [Fact]
    public async Task RefreshAsync_ExposesOnlyCodexNotInstalledAsInstallGuidanceState()
    {
        // Given
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.CodexNotInstalled },
            new CodexUsageResult { Status = CodexUsageStatus.NotAuthenticated });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.True(viewModel.IsCodexNotInstalled);

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.False(viewModel.IsCodexNotInstalled);
    }

    [Fact]
    public async Task RefreshCommand_DisablesWhileARefreshIsActive()
    {
        // Given
        var provider = new BlockingSuccessProvider();
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        var refresh = viewModel.RefreshAsync();
        await provider.Started.Task;

        // Then
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.RefreshCommand.CanExecute(null));

        provider.Release.SetResult();
        await refresh;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public void MenuBarPresentation_BeforeInitialRefresh_ShowsCompactLoadingState()
    {
        // Given
        var viewModel = new UsageViewModel(new QueueUsageProvider());

        // When
        var presentation = MenuBarPresentation.From(viewModel);

        // Then
        Assert.Equal("Codex …", presentation.StatusItemTitle);
        Assert.Equal("5-hour · Checking", presentation.ShortTermSummary);
        Assert.Equal("Weekly · Checking", presentation.WeeklySummary);
        Assert.Equal("5-hour", presentation.PrimaryLimit.Title);
        Assert.Equal("-", presentation.PrimaryLimit.UsedText);
        Assert.True(presentation.IsLoading);
        Assert.True(presentation.ShowsUnavailableState);
    }

    [Fact]
    public async Task MenuBarPresentation_LiveSnapshot_FormatsLimitAndResetDetails()
    {
        // Given
        var snapshot = Success(37d).Snapshot! with
        {
            Limits =
            [
                new UsageLimit(
                    "short",
                    "Five hour",
                    UsageLimitKind.ShortTerm,
                    64d,
                    TimeSpan.FromHours(5),
                    Now.AddHours(2).AddMinutes(18)),
                .. Success(37d).Snapshot!.Limits,
            ],
        };
        var provider = new QueueUsageProvider(new CodexUsageResult
        {
            Status = CodexUsageStatus.Success,
            Snapshot = snapshot,
        });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        await viewModel.RefreshAsync();

        // When
        var presentation = MenuBarPresentation.From(viewModel);

        // Then
        Assert.Equal("5H 64%", presentation.StatusItemTitle);
        Assert.Equal("5-hour 64% used · 36% remaining", presentation.ShortTermSummary);
        Assert.Equal("5-hour reset · in 2h 18m", presentation.ShortTermReset);
        Assert.Equal("Weekly 37% used · 63% remaining", presentation.WeeklySummary);
        Assert.Equal("5-hour", presentation.PrimaryLimit.Title);
        Assert.Equal("64%", presentation.PrimaryLimit.UsedText);
        Assert.Equal("36%", presentation.PrimaryLimit.RemainingText);
        Assert.Equal("in 2h 18m", presentation.PrimaryLimit.ResetText);
        Assert.NotNull(presentation.SecondaryLimit);
        Assert.Equal("Weekly", presentation.SecondaryLimit.Title);
        Assert.Equal("37%", presentation.SecondaryLimit.UsedText);
        Assert.False(presentation.ShowsUnavailableState);
        Assert.Contains("PLUS", presentation.AccountAndRefreshStatus, StringComparison.Ordinal);
        Assert.Contains("Updated", presentation.AccountAndRefreshStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("Last updated", presentation.AccountAndRefreshStatus, StringComparison.Ordinal);
        Assert.Null(presentation.NoticeTitle);
        Assert.Null(presentation.NoticeDetail);
    }

    [Fact]
    public async Task MenuBarPresentation_WeeklyOnlySnapshot_ShowsPercentWithoutWindowPrefix()
    {
        // Given
        var provider = new QueueUsageProvider(Success(37d));
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        await viewModel.RefreshAsync();

        // When
        var presentation = MenuBarPresentation.From(viewModel);

        // Then
        Assert.Equal("37%", presentation.StatusItemTitle);
    }

    [Fact]
    public async Task RefreshAsync_PrefersCurrentAccountPlanOverPreviousRateLimitBucketPlan()
    {
        // Given
        var snapshot = Success(37d).Snapshot! with
        {
            AccountPlan = "pro",
            RateLimitPlan = "plus",
        };
        var provider = new QueueUsageProvider(new CodexUsageResult
        {
            Status = CodexUsageStatus.Success,
            Snapshot = snapshot,
        });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal("PRO", viewModel.AccountPlanText);
    }

    [Fact]
    public async Task RefreshAsync_UsesRateLimitPlanWhenAccountPlanIsUnavailable()
    {
        // Given
        var snapshot = Success(37d).Snapshot! with
        {
            AccountPlan = null,
            RateLimitPlan = "plus",
        };
        var provider = new QueueUsageProvider(new CodexUsageResult
        {
            Status = CodexUsageStatus.Success,
            Snapshot = snapshot,
        });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal("PLUS", viewModel.AccountPlanText);
    }

    [Fact]
    public async Task MenuBarPresentation_FailedRefresh_MarksPreservedValuesAsPrevious()
    {
        // Given
        var provider = new QueueUsageProvider(
            Success(37d),
            new CodexUsageResult { Status = CodexUsageStatus.TimedOut });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        // When
        var presentation = MenuBarPresentation.From(viewModel);

        // Then
        Assert.Equal("37% · Stale", presentation.StatusItemTitle);
        Assert.Contains("Request timed out", presentation.NoticeTitle, StringComparison.Ordinal);
        Assert.Contains("last successful data", presentation.NoticeDetail, StringComparison.Ordinal);
        Assert.True(presentation.NoticeIsWarning);
    }

    private static CodexUsageResult Success(double weeklyUsedPercent) =>
        new()
        {
            Status = CodexUsageStatus.Success,
            Snapshot = new CodexUsageSnapshot
            {
                RetrievedAt = Now,
                AccountPlan = "plus",
                Limits =
                [
                    new UsageLimit(
                        "weekly",
                        "Weekly",
                        UsageLimitKind.Weekly,
                        weeklyUsedPercent,
                        TimeSpan.FromDays(7),
                        Now.AddDays(6).AddHours(23)),
                ],
            },
        };

    private sealed class QueueUsageProvider(params CodexUsageResult[] results) : ICodexUsageProvider
    {
        private readonly Queue<CodexUsageResult> _results = new(results);

        public Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.Dequeue());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CancellationPausingProvider : ICodexUsageProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowCancellationResult { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await AllowCancellationResult.Task;
            }

            return new CodexUsageResult { Status = CodexUsageStatus.Cancelled };
        }
    }

    private sealed class BlockingSuccessProvider : ICodexUsageProvider
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(37d);
        }
    }
}
