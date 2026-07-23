using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;

namespace CodexUsage.Desktop.ViewModels;

public sealed class UsageViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ICodexUsageProvider _provider;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshInterval;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly AsyncRelayCommand _refreshCommand;
    private Task? _refreshLoop;
    private string _statusTitle = "Checking usage";
    private string _statusDetail = "Loading limits for the current Codex account.";
    private string _lastUpdatedText = "Not refreshed yet";
    private string _accountPlanText = "Checking plan";
    private bool _hasNotice;
    private bool _isShowingStaleData;
    private bool _isWarningNotice;
    private bool _isBusy;
    private bool _hasRefreshed;
    private int _disposeStarted;

    public UsageViewModel(
        ICodexUsageProvider provider,
        TimeProvider? timeProvider = null,
        TimeSpan? refreshInterval = null)
    {
        _provider = provider;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(60);
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
    }

    public UsageLimitItemViewModel ShortTerm { get; } = new("5-hour");

    public UsageLimitItemViewModel Weekly { get; } = new("Weekly");

    public ICommand RefreshCommand => _refreshCommand;

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
        private set => SetProperty(ref _hasNotice, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasRefreshed
    {
        get => _hasRefreshed;
        private set => SetProperty(ref _hasRefreshed, value);
    }

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
        await RefreshAsync().ConfigureAwait(true);
        if (Volatile.Read(ref _disposeStarted) == 0)
        {
            _refreshLoop ??= RunRefreshLoopAsync(_stopping.Token);
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

            if (result is { Status: CodexUsageStatus.Success, Snapshot: not null })
            {
                ApplySnapshot(result.Snapshot);
            }
            else
            {
                ApplyFailure(result.Status);
            }

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
        _refreshLock.Dispose();
    }

    private void ApplySnapshot(CodexUsageSnapshot snapshot)
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
        LastUpdatedText = $"Last updated {snapshot.RetrievedAt.ToLocalTime():HH:mm:ss}";
        HasNotice = false;
        IsShowingStaleData = false;
        IsWarningNotice = false;
        OnPropertyChanged(nameof(MenuSummary));
        OnPropertyChanged(nameof(TrayToolTip));
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

        IsShowingStaleData = ShortTerm.ShowProgress || Weekly.ShowProgress;
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

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_refreshInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true))
            {
                await RefreshAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
