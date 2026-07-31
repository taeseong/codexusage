using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;
using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Desktop.ViewModels;

public sealed class UsageHistoryViewModel : ObservableObject
{
    private readonly IUsageHistoryStore _store;
    private readonly UsageHistoryService _service = new();
    private UsageHistoryState _state = new();
    private CodexUsageSnapshot? _latestSnapshot;
    private string? _currentPlan;
    private string _statusText = "History will start with the next weekly usage observation.";
    private bool _isUnavailable;
    private bool _isClearConfirmationVisible;
    private int _selectedMonthIndex;
    private int _observationFilterIndex;

    public UsageHistoryViewModel(IUsageHistoryStore store)
    {
        _store = store;
        RequestClearHistoryCommand = new RelayCommand(() => IsClearConfirmationVisible = true);
        ConfirmClearHistoryCommand = new AsyncRelayCommand(ClearAsync);
        CancelClearHistoryCommand = new RelayCommand(() => IsClearConfirmationVisible = false);
        RetryHistoryCommand = new AsyncRelayCommand(RetryAsync);
        ShowOlderMonthCommand = new RelayCommand(ShowOlderMonth, () => CanShowOlderMonth);
        ShowNewerMonthCommand = new RelayCommand(ShowNewerMonth, () => CanShowNewerMonth);
    }

    public ObservableCollection<WeeklyUsageWindowEntry> Windows { get; } = [];
    public ObservableCollection<MonthlyUsageHistoryGroup> MonthlyGroups { get; } = [];
    public IRelayCommand RequestClearHistoryCommand { get; }
    public IAsyncRelayCommand ConfirmClearHistoryCommand { get; }
    public IRelayCommand CancelClearHistoryCommand { get; }
    public IAsyncRelayCommand RetryHistoryCommand { get; }
    public IRelayCommand ShowOlderMonthCommand { get; }
    public IRelayCommand ShowNewerMonthCommand { get; }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsUnavailable
    {
        get => _isUnavailable;
        private set
        {
            if (SetProperty(ref _isUnavailable, value))
            {
                OnPropertyChanged(nameof(HasComparableHistory));
                OnPropertyChanged(nameof(HasInsufficientComparableHistory));
                OnPropertyChanged(nameof(MetricsText));
            }
        }
    }
    public bool IsClearConfirmationVisible { get => _isClearConfirmationVisible; private set => SetProperty(ref _isClearConfirmationVisible, value); }
    public bool HasWindows => Windows.Count > 0;
    public bool HasRecordedWindows => _state.Windows.Count > 0;
    public IReadOnlyList<WeeklyUsageWindowEntry> ExportableWindows => _state.Windows;
    public MonthlyUsageHistoryGroup? SelectedMonthGroup
    {
        get => MonthlyGroups.Count == 0
            ? null
            : MonthlyGroups[Math.Clamp(_selectedMonthIndex, 0, MonthlyGroups.Count - 1)];
        set
        {
            if (value is null)
            {
                return;
            }

            var index = MonthlyGroups.IndexOf(value);
            if (index >= 0 && index != _selectedMonthIndex)
            {
                _selectedMonthIndex = index;
                NotifyMonthSelectionChanged();
            }
        }
    }
    public bool CanShowOlderMonth => _selectedMonthIndex < MonthlyGroups.Count - 1;
    public bool CanShowNewerMonth => _selectedMonthIndex > 0;
    public int ObservationFilterIndex
    {
        get => _observationFilterIndex;
        set
        {
            var normalized = Math.Clamp(value, 0, 2);
            if (SetProperty(ref _observationFilterIndex, normalized))
            {
                RefreshView();
            }
        }
    }
    public string ObservationFilterSummary => ObservationFilterIndex switch
    {
        1 => "Completed observations only",
        2 => "Partial observations only",
        _ => "All observations",
    };
    public bool HasComparableHistory => !IsUnavailable && Metrics.ComparableWindowCount >= 3;
    public bool HasInsufficientComparableHistory =>
        !IsUnavailable && HasWindows && !HasComparableHistory;
    public UsageHistoryMetrics Metrics => _service.CalculateMetrics(_state, _currentPlan);
    public string MetricsText => !HasComparableHistory
        ? "Not enough comparable history"
        : $"{Metrics.ComparableWindowCount} comparable windows · Average peak {Metrics.AveragePeakObservedPercent:0}% · High {Metrics.HighestPeakObservedPercent:0}% · 80%+ {Metrics.Reached80PercentCount} · 95%+ {Metrics.Reached95PercentCount}";

    public async Task InitializeAsync()
    {
        try { _state = await _store.LoadAsync(); IsUnavailable = false; RefreshView(); }
        catch { IsUnavailable = true; StatusText = "History unavailable"; }
    }

    public async Task ObserveAsync(CodexUsageSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        if (IsUnavailable) return;
        _currentPlan = string.IsNullOrWhiteSpace(snapshot.AccountPlan) ? snapshot.RateLimitPlan : snapshot.AccountPlan;
        if (!_service.Observe(_state, snapshot, out var updated)) return;
        _state = updated;
        try { await _store.SaveAsync(_state); IsUnavailable = false; RefreshView(); }
        catch { IsUnavailable = true; StatusText = "History unavailable"; }
    }

    private async Task RetryAsync()
    {
        try
        {
            _state = await _store.LoadAsync();
            IsUnavailable = false;
            RefreshView();
            if (_latestSnapshot is not null)
            {
                await ObserveAsync(_latestSnapshot);
            }
        }
        catch
        {
            IsUnavailable = true;
            StatusText = "History unavailable";
        }
    }

    private async Task ClearAsync()
    {
        try
        {
            await _store.ClearAsync();
            _state = new UsageHistoryState();
            IsClearConfirmationVisible = false;
            RefreshView();
        }
        catch { IsUnavailable = true; StatusText = "History unavailable"; }
    }

    private void RefreshView()
    {
        var selectedMonth = SelectedMonthGroup?.Month;
        var recentEntries = _state.Windows
            .OrderByDescending(entry => entry.FirstObservedAt)
            .Take(12)
            .Where(MatchesObservationFilter)
            .ToArray();
        Windows.Clear();
        foreach (var entry in recentEntries) Windows.Add(entry);
        MonthlyGroups.Clear();
        foreach (var group in Windows
                     .GroupBy(entry => new DateTime(entry.FirstObservedAt.LocalDateTime.Year, entry.FirstObservedAt.LocalDateTime.Month, 1))
                     .OrderByDescending(group => group.Key))
        {
            MonthlyGroups.Add(new MonthlyUsageHistoryGroup(
                group.Key,
                group.OrderByDescending(entry => entry.FirstObservedAt).ToArray()));
        }
        _selectedMonthIndex = selectedMonth is null
            ? 0
            : Math.Max(0, MonthlyGroups.ToList().FindIndex(group => group.Month == selectedMonth));
        StatusText = Windows.Count == 0
            ? ObservationFilterIndex == 0
                ? "History starts after this feature is installed; peak observed values are not total usage."
                : "No observations match the selected filter."
            : "Local peak observations only. Time while the app was closed is not inferred.";
        OnPropertyChanged(nameof(HasWindows));
        OnPropertyChanged(nameof(HasRecordedWindows));
        OnPropertyChanged(nameof(ExportableWindows));
        OnPropertyChanged(nameof(Metrics));
        OnPropertyChanged(nameof(HasComparableHistory));
        OnPropertyChanged(nameof(HasInsufficientComparableHistory));
        OnPropertyChanged(nameof(MetricsText));
        OnPropertyChanged(nameof(ObservationFilterSummary));
        NotifyMonthSelectionChanged();
    }

    public void SetExportStatus(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        StatusText = message;
    }

    private void ShowOlderMonth()
    {
        if (CanShowOlderMonth)
        {
            _selectedMonthIndex++;
            NotifyMonthSelectionChanged();
        }
    }

    private void ShowNewerMonth()
    {
        if (CanShowNewerMonth)
        {
            _selectedMonthIndex--;
            NotifyMonthSelectionChanged();
        }
    }

    private void NotifyMonthSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedMonthGroup));
        OnPropertyChanged(nameof(CanShowOlderMonth));
        OnPropertyChanged(nameof(CanShowNewerMonth));
        ShowOlderMonthCommand.NotifyCanExecuteChanged();
        ShowNewerMonthCommand.NotifyCanExecuteChanged();
    }

    private bool MatchesObservationFilter(WeeklyUsageWindowEntry entry) => ObservationFilterIndex switch
    {
        1 => !entry.IsPartialObservation,
        2 => entry.IsPartialObservation,
        _ => true,
    };
}

public sealed class MonthlyUsageHistoryGroup
{
    public MonthlyUsageHistoryGroup(DateTime month, IReadOnlyList<WeeklyUsageWindowEntry> entries)
    {
        Month = month;
        Entries = entries;
    }

    public DateTime Month { get; }
    public IReadOnlyList<WeeklyUsageWindowEntry> Entries { get; }
    public string MonthDisplayText => Month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    public double AveragePeakObservedPercent => Entries.Average(entry => entry.PeakObservedPercent);
    public double HighestPeakObservedPercent => Entries.Max(entry => entry.PeakObservedPercent);
    public int EarlyResetCount => Entries.Count(entry =>
        entry.ClosureKind == UsageWindowClosureKind.EarlyResetObserved);
    public string SummaryText => string.Create(
        CultureInfo.InvariantCulture,
        $"{Entries.Count} windows · Avg {AveragePeakObservedPercent:0}% · High {HighestPeakObservedPercent:0}% · Early {EarlyResetCount}");
}
