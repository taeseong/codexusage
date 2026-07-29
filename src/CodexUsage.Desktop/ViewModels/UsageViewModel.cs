using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;

namespace CodexUsage.Desktop.ViewModels;

public sealed class UsageViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly TimeSpan MaximumCacheAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan MinimumCacheWriteInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumCacheWriteInterval = TimeSpan.FromMinutes(15);
    private readonly ICodexUsageProvider _provider;
    private readonly IUsageSnapshotCache? _snapshotCache;
    private readonly TimeProvider _timeProvider;
    private readonly UsageRefreshSchedule _refreshSchedule;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _recoveryActionCommand;
    private Action? _codexInstallGuidanceRequested;
    private Task? _refreshLoop;
    private string _statusTitle = "Checking usage";
    private string _statusDetail = "Loading limits for the current Codex account.";
    private string _lastUpdatedText = "Not refreshed yet";
    private string _accountPlanText = "Checking plan";
    private bool _hasNotice;
    private bool _isShowingStaleData;
    private bool _isWarningNotice;
    private bool _isCodexNotInstalled;
    private bool _isBusy;
    private bool _hasRefreshed;
    private CodexUsageStatus? _lastStatus;
    private CodexUsageSnapshot? _lastCachedSnapshot;
    private DateTimeOffset? _lastCacheWriteAttemptAt;
    private int _consecutiveTransientFailures;
    private bool _cacheLoadAttempted;
    private int _disposeStarted;

    public UsageViewModel(
        ICodexUsageProvider provider,
        TimeProvider? timeProvider = null,
        TimeSpan? refreshInterval = null,
        IUsageSnapshotCache? snapshotCache = null,
        UsageRefreshSchedule? refreshSchedule = null)
    {
        _provider = provider;
        _snapshotCache = snapshotCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _refreshSchedule = refreshSchedule ?? new UsageRefreshSchedule(refreshInterval);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        _recoveryActionCommand = new AsyncRelayCommand(
            ExecuteRecoveryActionAsync,
            () => !IsBusy && HasRecoveryAction);
    }

    public UsageLimitItemViewModel ShortTerm { get; } = new("5-hour");

    public UsageLimitItemViewModel Weekly { get; } = new("Weekly");

    public UsageHistoryViewModel? History { get; set; }

    public event Action<CodexUsageSnapshot>? SnapshotRefreshed;

    public event Action? CodexInstallGuidanceRequested
    {
        add
        {
            _codexInstallGuidanceRequested += value;
            NotifyRecoveryActionStateChanged();
        }
        remove
        {
            _codexInstallGuidanceRequested -= value;
            NotifyRecoveryActionStateChanged();
        }
    }

    public ICommand RefreshCommand => _refreshCommand;

    public IAsyncRelayCommand RecoveryActionCommand => _recoveryActionCommand;

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string AccountPlanText
    {
        get => _accountPlanText;
        private set => SetProperty(ref _accountPlanText, value);
    }

    public bool HasNotice
    {
        get => _hasNotice;
        private set
        {
            if (SetProperty(ref _hasNotice, value))
            {
                NotifyRecoveryActionStateChanged();
            }
        }
    }

    public bool IsShowingStaleData
    {
        get => _isShowingStaleData;
        private set => SetProperty(ref _isShowingStaleData, value);
    }

    public bool IsWarningNotice
    {
        get => _isWarningNotice;
        private set => SetProperty(ref _isWarningNotice, value);
    }

    public bool IsCodexNotInstalled
    {
        get => _isCodexNotInstalled;
        private set => SetProperty(ref _isCodexNotInstalled, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.NotifyCanExecuteChanged();
                _recoveryActionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasRefreshed
    {
        get => _hasRefreshed;
        private set => SetProperty(ref _hasRefreshed, value);
    }

    public CodexUsageStatus? LastStatus
    {
        get => _lastStatus;
        private set
        {
            if (SetProperty(ref _lastStatus, value))
            {
                NotifyRecoveryActionStateChanged();
            }
        }
    }

    public bool HasRecoveryAction =>
        HasNotice &&
        LastStatus is not null &&
        (LastStatus is not CodexUsageStatus.CodexNotInstalled ||
         _codexInstallGuidanceRequested is not null);

    public string RecoveryActionText => LastStatus switch
    {
        CodexUsageStatus.CodexNotInstalled => "Install Codex CLI",
        CodexUsageStatus.NotAuthenticated or CodexUsageStatus.AuthenticationExpired =>
            "Check sign-in again",
        CodexUsageStatus.UsageUnsupported => "Retry after updating",
        _ => "Retry now",
    };

    public string MenuSummary
    {
        get
        {
            var parts = new[]
            {
                ShortTerm.ShowProgress ? $"5H {ShortTerm.UsedText}" : null,
                Weekly.ShowProgress ? $"W {Weekly.UsedText}" : null,
            };
            var summary = string.Join(" · ", parts.Where(static part => part is not null));
            return IsShowingStaleData && summary.Length > 0
                ? $"{summary} · Stale"
                : summary;
        }
    }

    public string TrayToolTip => string.IsNullOrEmpty(MenuSummary)
        ? "Codex Usage"
        : $"Codex Usage · {MenuSummary}";

    public async Task StartAsync()
    {
        await RestoreCachedSnapshotAsync().ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        if (Volatile.Read(ref _disposeStarted) == 0)
        {
            _refreshLoop ??= RunRefreshLoopAsync(_stopping.Token);
        }
    }

    public void RequestImmediateRefresh()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            return;
        }

        try
        {
            _refreshSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task RefreshAsync()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            return;
        }

        var lockTaken = false;
        try
        {
            await _refreshLock.WaitAsync(_stopping.Token).ConfigureAwait(true);
            lockTaken = true;
            IsBusy = true;
            var result = await _provider.GetUsageAsync(_stopping.Token).ConfigureAwait(true);
            if (Volatile.Read(ref _disposeStarted) != 0)
            {
                return;
            }

            LastStatus = result.Status;
            if (result is { Status: CodexUsageStatus.Success, Snapshot: not null })
            {
                ApplySnapshot(result.Snapshot);
                _consecutiveTransientFailures = 0;
                await TryPersistSnapshotAsync(result.Snapshot, _stopping.Token)
                    .ConfigureAwait(true);
            }
            else
            {
                _consecutiveTransientFailures = UsageRefreshSchedule.IsTransient(result.Status)
                    ? _consecutiveTransientFailures + 1
                    : 0;
                ApplyFailure(result.Status);
            }

            IsCodexNotInstalled = result.Status is CodexUsageStatus.CodexNotInstalled;
            HasRefreshed = true;
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            if (lockTaken)
            {
                IsBusy = false;
                _refreshLock.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        if (_refreshLoop is not null)
        {
            await _refreshLoop.ConfigureAwait(false);
        }

        await _refreshLock.WaitAsync().ConfigureAwait(false);
        _refreshLock.Release();
        _stopping.Dispose();
        _refreshSignal.Dispose();
        _refreshLock.Dispose();
    }

    private void ApplySnapshot(CodexUsageSnapshot snapshot, bool isCached = false)
    {
        var now = _timeProvider.GetUtcNow();
        ShortTerm.Update(snapshot.Limits.FirstOrDefault(static limit => limit.Kind is UsageLimitKind.ShortTerm), now);
        Weekly.Update(snapshot.Limits.FirstOrDefault(static limit => limit.Kind is UsageLimitKind.Weekly), now);
        var displayedPlan = string.IsNullOrWhiteSpace(snapshot.AccountPlan)
            ? snapshot.RateLimitPlan
            : snapshot.AccountPlan;
        AccountPlanText = string.IsNullOrWhiteSpace(displayedPlan)
            ? "Plan unavailable"
            : displayedPlan.ToUpperInvariant();
        LastUpdatedText = isCached
            ? $"Last successful {snapshot.RetrievedAt.ToLocalTime():MMM d HH:mm}"
            : $"Last updated {snapshot.RetrievedAt.ToLocalTime():HH:mm:ss}";
        HasNotice = isCached;
        IsShowingStaleData = isCached;
        IsWarningNotice = isCached;
        if (isCached)
        {
            StatusTitle = "Showing previous usage";
            StatusDetail = "Refreshing live usage. Cached values may be out of date.";
        }

        OnPropertyChanged(nameof(MenuSummary));
        OnPropertyChanged(nameof(TrayToolTip));
        if (!isCached)
        {
            SnapshotRefreshed?.Invoke(snapshot);
        }
    }

    private void ApplyFailure(CodexUsageStatus status)
    {
        (StatusTitle, StatusDetail) = status switch
        {
            CodexUsageStatus.CodexNotInstalled => ("Codex not found", "Check the Codex CLI installation path."),
            CodexUsageStatus.NotAuthenticated or CodexUsageStatus.AuthenticationExpired =>
                ("Sign in to Codex", "Sign in to Codex, then refresh."),
            CodexUsageStatus.UsageUnsupported => ("Usage lookup is not supported", "Update the installed Codex version."),
            CodexUsageStatus.NetworkError => ("Check your network connection", "Codex Usage will retry automatically when the connection returns."),
            CodexUsageStatus.TimedOut => ("Request timed out", "Codex Usage will retry automatically."),
            CodexUsageStatus.Cancelled => ("Usage lookup was cancelled", "Try refreshing again."),
            CodexUsageStatus.ProtocolError or CodexUsageStatus.ResponseFormatChanged =>
                ("Unable to read the usage response", "The Codex response format may have changed."),
            _ => ("Unable to load usage", "Codex Usage will retry automatically."),
        };

        var hasPreviousData = ShortTerm.ShowProgress || Weekly.ShowProgress;
        var canShowPreviousData = status is not (
            CodexUsageStatus.NotAuthenticated or
            CodexUsageStatus.AuthenticationExpired);
        IsShowingStaleData = hasPreviousData && canShowPreviousData;
        IsWarningNotice = IsShowingStaleData;
        if (IsShowingStaleData)
        {
            StatusDetail += " Showing the last successful data.";
        }
        else
        {
            var availability = status switch
            {
                CodexUsageStatus.NotAuthenticated or CodexUsageStatus.AuthenticationExpired => "Sign in required",
                CodexUsageStatus.CodexNotInstalled => "Codex not found",
                CodexUsageStatus.UsageUnsupported => "Unsupported",
                _ => "Lookup failed",
            };
            ShortTerm.MarkUnavailable(availability, "Not retrieved");
            Weekly.MarkUnavailable(availability, "Not retrieved");
            AccountPlanText = availability is "Sign in required" ? availability : "Plan unavailable";
            LastUpdatedText = $"Lookup failed {_timeProvider.GetUtcNow().ToLocalTime():HH:mm:ss}";
        }

        HasNotice = true;
        OnPropertyChanged(nameof(MenuSummary));
        OnPropertyChanged(nameof(TrayToolTip));
    }

    private async Task ExecuteRecoveryActionAsync()
    {
        if (LastStatus is CodexUsageStatus.CodexNotInstalled)
        {
            _codexInstallGuidanceRequested?.Invoke();
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private void NotifyRecoveryActionStateChanged()
    {
        OnPropertyChanged(nameof(HasRecoveryAction));
        OnPropertyChanged(nameof(RecoveryActionText));
        _recoveryActionCommand.NotifyCanExecuteChanged();
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delay = _refreshSchedule.GetNextDelay(
                    LastStatus,
                    _consecutiveTransientFailures);
                await WaitForDelayOrSignalAsync(delay, cancellationToken).ConfigureAwait(true);
                await RefreshAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RestoreCachedSnapshotAsync()
    {
        if (_cacheLoadAttempted || _snapshotCache is null || HasRefreshed)
        {
            return;
        }

        _cacheLoadAttempted = true;
        try
        {
            var cached = await _snapshotCache.LoadAsync(_stopping.Token).ConfigureAwait(true);
            var usable = GetUsableCachedSnapshot(cached);
            if (usable is null)
            {
                return;
            }

            _lastCachedSnapshot = usable;
            ApplySnapshot(usable, isCached: true);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Usage cache restore failed: {0}",
                exception.GetType().Name);
        }
    }

    private CodexUsageSnapshot? GetUsableCachedSnapshot(CodexUsageSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var age = now - snapshot.RetrievedAt;
        if (age < TimeSpan.FromMinutes(-5) || age > MaximumCacheAge)
        {
            return null;
        }

        var limits = snapshot.Limits
            .Where(limit =>
                (limit.Kind is UsageLimitKind.ShortTerm or UsageLimitKind.Weekly) &&
                (limit.ResetsAt is null || limit.ResetsAt > now))
            .ToArray();
        return limits.Length == 0
            ? null
            : snapshot with { Limits = limits };
    }

    private async Task TryPersistSnapshotAsync(
        CodexUsageSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (_snapshotCache is null || !ShouldPersistSnapshot(snapshot))
        {
            return;
        }

        _lastCacheWriteAttemptAt = _timeProvider.GetUtcNow();
        try
        {
            await _snapshotCache.SaveAsync(snapshot, cancellationToken).ConfigureAwait(true);
            _lastCachedSnapshot = snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Usage cache persistence failed: {0}",
                exception.GetType().Name);
        }
    }

    private bool ShouldPersistSnapshot(CodexUsageSnapshot snapshot)
    {
        if (_lastCachedSnapshot is null)
        {
            return true;
        }

        if (HasCacheStructureChanged(_lastCachedSnapshot, snapshot))
        {
            return true;
        }

        var now = _timeProvider.GetUtcNow();
        if (_lastCacheWriteAttemptAt is { } lastAttempt &&
            now - lastAttempt < MinimumCacheWriteInterval)
        {
            return false;
        }

        return HaveUsageValuesChanged(_lastCachedSnapshot, snapshot) ||
               snapshot.RetrievedAt - _lastCachedSnapshot.RetrievedAt >=
               MaximumCacheWriteInterval;
    }

    private static bool HasCacheStructureChanged(
        CodexUsageSnapshot previous,
        CodexUsageSnapshot current)
    {
        if (!string.Equals(previous.AccountPlan, current.AccountPlan, StringComparison.Ordinal) ||
            !string.Equals(previous.RateLimitPlan, current.RateLimitPlan, StringComparison.Ordinal))
        {
            return true;
        }

        var previousLimits = GetCacheableLimits(previous);
        var currentLimits = GetCacheableLimits(current);
        if (previousLimits.Count != currentLimits.Count)
        {
            return true;
        }

        foreach (var (kind, previousLimit) in previousLimits)
        {
            if (!currentLimits.TryGetValue(kind, out var currentLimit) ||
                previousLimit.WindowDuration != currentLimit.WindowDuration ||
                HasMeaningfulResetChange(previousLimit.ResetsAt, currentLimit.ResetsAt))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HaveUsageValuesChanged(
        CodexUsageSnapshot previous,
        CodexUsageSnapshot current)
    {
        var previousLimits = GetCacheableLimits(previous);
        var currentLimits = GetCacheableLimits(current);
        return previousLimits.Any(pair =>
            currentLimits.TryGetValue(pair.Key, out var currentLimit) &&
            Math.Abs(pair.Value.UsedPercent - currentLimit.UsedPercent) > 0.001d);
    }

    private static IReadOnlyDictionary<UsageLimitKind, UsageLimit> GetCacheableLimits(
        CodexUsageSnapshot snapshot) =>
        snapshot.Limits
            .Where(static limit =>
                limit.Kind is UsageLimitKind.ShortTerm or UsageLimitKind.Weekly)
            .GroupBy(static limit => limit.Kind)
            .ToDictionary(static group => group.Key, static group => group.First());

    private static bool HasMeaningfulResetChange(
        DateTimeOffset? previous,
        DateTimeOffset? current)
    {
        if (previous is null || current is null)
        {
            return previous != current;
        }

        return (previous.Value - current.Value).Duration() >= TimeSpan.FromMinutes(5);
    }

    private async Task WaitForDelayOrSignalAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(delay, _timeProvider, waiting.Token);
        var signalTask = _refreshSignal.WaitAsync(waiting.Token);
        await Task.WhenAny(delayTask, signalTask).ConfigureAwait(true);
        waiting.Cancel();

        try
        {
            await Task.WhenAll(delayTask, signalTask).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
