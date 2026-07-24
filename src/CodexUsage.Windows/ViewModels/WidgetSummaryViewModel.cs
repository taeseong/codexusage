using System.ComponentModel;
using System.Runtime.CompilerServices;
using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Windows.ViewModels;

internal sealed class WidgetSummaryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly UsageViewModel _usageViewModel;
    private bool _disposed;

    public WidgetSummaryViewModel(UsageViewModel usageViewModel)
    {
        _usageViewModel = usageViewModel;
        _usageViewModel.PropertyChanged += OnUsagePropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SummaryText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_usageViewModel.MenuSummary))
            {
                return _usageViewModel.Weekly.ShowProgress && !_usageViewModel.ShortTerm.ShowProgress
                    ? $"5H ∞  |  {_usageViewModel.MenuSummary}"
                    : _usageViewModel.MenuSummary;
            }

            return _usageViewModel.HasRefreshed
                ? _usageViewModel.StatusTitle
                : "Checking usage…";
        }
    }

    public string LeadingSummaryText => ShowsWeeklySummary
        ? _usageViewModel.ShortTerm.ShowProgress
            ? $"5H {_usageViewModel.ShortTerm.UsedText}"
            : "5H"
        : SummaryText;

    public string UnlimitedShortTermText =>
        ShowsWeeklySummary && !_usageViewModel.ShortTerm.ShowProgress ? "∞" : string.Empty;

    public string SummaryDividerText => ShowsWeeklySummary ? "|" : string.Empty;

    public string TrailingSummaryText => ShowsWeeklySummary
        ? $"W {_usageViewModel.Weekly.UsedText}{(_usageViewModel.IsShowingStaleData ? " · Stale" : string.Empty)}"
        : string.Empty;

    public bool ShowWeeklyProgress => ShowsWeeklySummary && !_usageViewModel.HasNotice;

    public IReadOnlyList<WidgetProgressSegment> WeeklyProgressSegments =>
        Enumerable.Range(1, 10)
            .Select(index => new WidgetProgressSegment(
                index * 10 <= _usageViewModel.Weekly.UsedPercent,
                _usageViewModel.Weekly.UsedPercent is >= 80d and < 95d,
                _usageViewModel.Weekly.UsedPercent >= 95d))
            .ToArray();

    public string ToolTip => _usageViewModel.HasNotice
        ? $"{_usageViewModel.StatusTitle} · {_usageViewModel.StatusDetail}"
        : _usageViewModel.TrayToolTip;

    public bool IsBusy => _usageViewModel.IsBusy;

    public bool HasNotice => _usageViewModel.HasNotice;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _usageViewModel.PropertyChanged -= OnUsagePropertyChanged;
    }

    private void OnUsagePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(LeadingSummaryText));
        OnPropertyChanged(nameof(UnlimitedShortTermText));
        OnPropertyChanged(nameof(SummaryDividerText));
        OnPropertyChanged(nameof(TrailingSummaryText));
        OnPropertyChanged(nameof(ShowWeeklyProgress));
        OnPropertyChanged(nameof(WeeklyProgressSegments));
        OnPropertyChanged(nameof(ToolTip));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasNotice));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool ShowsWeeklySummary => _usageViewModel.Weekly.ShowProgress;
}
