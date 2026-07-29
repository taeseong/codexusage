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
        Assert.Equal(CodexUsageStatus.CodexNotInstalled, viewModel.LastStatus);

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.False(viewModel.IsCodexNotInstalled);
        Assert.Equal(CodexUsageStatus.NotAuthenticated, viewModel.LastStatus);
    }

    [Theory]
    [InlineData(CodexUsageStatus.CodexNotInstalled, "Install Codex CLI")]
    [InlineData(CodexUsageStatus.NotAuthenticated, "Check sign-in again")]
    [InlineData(CodexUsageStatus.AuthenticationExpired, "Check sign-in again")]
    [InlineData(CodexUsageStatus.UsageUnsupported, "Retry after updating")]
    [InlineData(CodexUsageStatus.NetworkError, "Retry now")]
    [InlineData(CodexUsageStatus.TimedOut, "Retry now")]
    public async Task RefreshAsync_ProvidesAStatusSpecificRecoveryAction(
        CodexUsageStatus status,
        string expectedText)
    {
        var provider = new QueueUsageProvider(new CodexUsageResult { Status = status });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        if (status is CodexUsageStatus.CodexNotInstalled)
        {
            viewModel.CodexInstallGuidanceRequested += static () => { };
        }

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasRecoveryAction);
        Assert.Equal(expectedText, viewModel.RecoveryActionText);
        Assert.True(viewModel.RecoveryActionCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingCliRecovery_IsHiddenWhenThePlatformHasNoInstallGuidance()
    {
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.CodexNotInstalled });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));

        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasRecoveryAction);
        Assert.False(viewModel.RecoveryActionCommand.CanExecute(null));
    }

    [Fact]
    public async Task RecoveryAction_ForMissingCli_RequestsInstallGuidanceWithoutAnotherLookup()
    {
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.CodexNotInstalled });
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        var guidanceRequests = 0;
        viewModel.CodexInstallGuidanceRequested += () => guidanceRequests++;
        await viewModel.RefreshAsync();

        await viewModel.RecoveryActionCommand.ExecuteAsync(null);

        Assert.Equal(1, guidanceRequests);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task RecoveryAction_ForTransientFailure_RetriesAndClearsTheNotice()
    {
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.NetworkError },
            Success(37d));
        await using var viewModel = new UsageViewModel(provider, new FixedTimeProvider(Now));
        await viewModel.RefreshAsync();

        await viewModel.RecoveryActionCommand.ExecuteAsync(null);

        Assert.Equal(CodexUsageStatus.Success, viewModel.LastStatus);
        Assert.False(viewModel.HasNotice);
        Assert.False(viewModel.HasRecoveryAction);
        Assert.Equal("37%", viewModel.Weekly.UsedText);
        Assert.Equal(2, provider.CallCount);
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
    public async Task StartAsync_RestoresLastGoodSnapshotWhenLiveLookupFails()
    {
        // Given
        var cache = new RecordingSnapshotCache(Success(37d).Snapshot);
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.NetworkError });
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);
        var refreshedSnapshots = 0;
        viewModel.SnapshotRefreshed += _ => refreshedSnapshots++;

        // When
        await viewModel.StartAsync();

        // Then
        Assert.Equal("37%", viewModel.Weekly.UsedText);
        Assert.Equal("W 37% · Stale", viewModel.MenuSummary);
        Assert.True(viewModel.IsShowingStaleData);
        Assert.Contains("last successful data", viewModel.StatusDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, refreshedSnapshots);
    }

    [Fact]
    public async Task StartAsync_DoesNotRestoreAWindowThatAlreadyReset()
    {
        // Given
        var expired = Success(37d).Snapshot! with
        {
            Limits =
            [
                new UsageLimit(
                    "weekly",
                    "Weekly",
                    UsageLimitKind.Weekly,
                    37d,
                    TimeSpan.FromDays(7),
                    Now.AddMinutes(-1)),
            ],
        };
        var cache = new RecordingSnapshotCache(expired);
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.NotAuthenticated });
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.StartAsync();

        // Then
        Assert.False(viewModel.IsShowingStaleData);
        Assert.Equal("Sign in required", viewModel.Weekly.AvailabilityText);
    }

    [Fact]
    public async Task StartAsync_DoesNotShowAnotherSessionsCacheWhenSignedOut()
    {
        // Given
        var cache = new RecordingSnapshotCache(Success(37d).Snapshot);
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.NotAuthenticated });
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.StartAsync();

        // Then
        Assert.False(viewModel.IsShowingStaleData);
        Assert.Equal("Sign in required", viewModel.Weekly.AvailabilityText);
        Assert.Equal("-", viewModel.Weekly.UsedText);
    }

    [Fact]
    public async Task RefreshAsync_PersistsOnlySuccessfulSnapshots()
    {
        // Given
        var cache = new RecordingSnapshotCache();
        var provider = new QueueUsageProvider(
            new CodexUsageResult { Status = CodexUsageStatus.TimedOut },
            Success(37d));
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        // Then
        Assert.Single(cache.SavedSnapshots);
        Assert.Equal(37d, cache.SavedSnapshots[0].Limits.Single().UsedPercent);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRewriteAnUnchangedCacheEveryRefresh()
    {
        // Given
        var cache = new RecordingSnapshotCache();
        var provider = new QueueUsageProvider(Success(37d), Success(37d));
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        // Then
        Assert.Single(cache.SavedSnapshots);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRewriteCacheForSmallResetJitter()
    {
        // Given
        var initial = Success(37d);
        var corrected = initial with
        {
            Snapshot = initial.Snapshot! with
            {
                Limits =
                [
                    new UsageLimit(
                        "weekly",
                        "Weekly",
                        UsageLimitKind.Weekly,
                        37d,
                        TimeSpan.FromDays(7),
                        initial.Snapshot.Limits.Single().ResetsAt!.Value.AddMinutes(2)),
                ],
            },
        };
        var cache = new RecordingSnapshotCache();
        var provider = new QueueUsageProvider(initial, corrected);
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        // Then
        Assert.Single(cache.SavedSnapshots);
    }

    [Fact]
    public async Task RefreshAsync_RewritesCacheImmediatelyForANewResetWindow()
    {
        // Given
        var initial = Success(37d);
        var nextWindow = initial with
        {
            Snapshot = initial.Snapshot! with
            {
                Limits =
                [
                    new UsageLimit(
                        "weekly",
                        "Weekly",
                        UsageLimitKind.Weekly,
                        0d,
                        TimeSpan.FromDays(7),
                        initial.Snapshot.Limits.Single().ResetsAt!.Value.AddDays(7)),
                ],
            },
        };
        var cache = new RecordingSnapshotCache();
        var provider = new QueueUsageProvider(initial, nextWindow);
        await using var viewModel = new UsageViewModel(
            provider,
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal(2, cache.SavedSnapshots.Count);
    }

    [Fact]
    public async Task RefreshAsync_CacheFailureDoesNotHideLiveUsage()
    {
        // Given
        var cache = new RecordingSnapshotCache
        {
            SaveException = new IOException("cache unavailable"),
        };
        await using var viewModel = new UsageViewModel(
            new QueueUsageProvider(Success(37d)),
            new FixedTimeProvider(Now),
            snapshotCache: cache);

        // When
        await viewModel.RefreshAsync();

        // Then
        Assert.Equal(CodexUsageStatus.Success, viewModel.LastStatus);
        Assert.Equal("37%", viewModel.Weekly.UsedText);
        Assert.False(viewModel.HasNotice);
    }

    [Fact]
    public async Task RequestImmediateRefresh_WakesAWaitingRefreshLoop()
    {
        // Given
        var provider = new CountingUsageProvider();
        await using var viewModel = new UsageViewModel(
            provider,
            refreshInterval: TimeSpan.FromHours(1));
        await viewModel.StartAsync();

        // When
        viewModel.RequestImmediateRefresh();
        await provider.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Then
        Assert.True(provider.CallCount >= 2);
    }

    [Fact]
    public async Task RequestImmediateRefresh_CoalescesConcurrentSignals()
    {
        // Given
        await using var viewModel = new UsageViewModel(new CountingUsageProvider());

        // When
        var exception = Record.Exception(
            () => Parallel.For(0, 1000, _ => viewModel.RequestImmediateRefresh()));

        // Then
        Assert.Null(exception);
    }

    [Fact]
    public void RefreshSchedule_UsesBackoffOnlyForTransientFailures()
    {
        var schedule = new UsageRefreshSchedule();

        Assert.Equal(
            TimeSpan.FromSeconds(5),
            schedule.GetNextDelay(CodexUsageStatus.NetworkError, 1));
        Assert.Equal(
            TimeSpan.FromSeconds(15),
            schedule.GetNextDelay(CodexUsageStatus.TimedOut, 2));
        Assert.Equal(
            TimeSpan.FromSeconds(30),
            schedule.GetNextDelay(CodexUsageStatus.ProtocolError, 3));
        Assert.Equal(
            TimeSpan.FromMinutes(1),
            schedule.GetNextDelay(CodexUsageStatus.UnknownError, 4));
        Assert.Equal(
            TimeSpan.FromMinutes(5),
            schedule.GetNextDelay(CodexUsageStatus.UnknownError, 10));
        Assert.Equal(
            TimeSpan.FromMinutes(1),
            schedule.GetNextDelay(CodexUsageStatus.NotAuthenticated, 10));
        Assert.Equal(
            TimeSpan.FromMinutes(1),
            schedule.GetNextDelay(CodexUsageStatus.Success, 0));
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
        private int _callCount;

        public int CallCount => _callCount;

        public Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            _callCount++;
            return Task.FromResult(_results.Dequeue());
        }
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

    private sealed class RecordingSnapshotCache(CodexUsageSnapshot? snapshot = null)
        : IUsageSnapshotCache
    {
        public List<CodexUsageSnapshot> SavedSnapshots { get; } = [];

        public Exception? SaveException { get; init; }

        public Task<CodexUsageSnapshot?> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task SaveAsync(
            CodexUsageSnapshot value,
            CancellationToken cancellationToken = default)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSnapshots.Add(value);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUsageProvider : ICodexUsageProvider
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public TaskCompletionSource SecondCall { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CodexUsageResult> GetUsageAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) >= 2)
            {
                SecondCall.TrySetResult();
            }

            return Task.FromResult(Success(37d));
        }
    }
}
