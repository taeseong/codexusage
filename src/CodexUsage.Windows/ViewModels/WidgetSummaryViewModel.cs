using System.ComponentModel;
using System.Runtime.CompilerServices;
using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Windows.ViewModels;

internal sealed class WidgetSummaryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly UsageViewModel _usageViewModel;
    private double _widgetOpacity = 1d;
    private bool _showShortTermUsage = true;
    private bool _showWeeklyUsage = true;
    private bool _showWeeklyProgress = true;
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
            if (!CanRenderSelectedSummary)
            {
                return _usageViewModel.HasRefreshed
                    ? _usageViewModel.StatusTitle
                    : "Checking usage\u2026";
            }

            return string.Join(
                " | ",
                new[]
                {
                    _showShortTermUsage ? GetShortTermSummaryText() : null,
                    _showWeeklyUsage ? GetWeeklySummaryText() : null,
                }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        }
    }

    public string LeadingSummaryText => CanRenderSelectedSummary
        ? _showShortTermUsage
            ? _usageViewModel.ShortTerm.ShowProgress
                ? GetShortTermSummaryText()
                : "5H"
            : _showWeeklyUsage
                ? GetWeeklySummaryText()
                : SummaryText
        : SummaryText;

    public string UnlimitedShortTermText =>
        CanRenderSelectedSummary && _showShortTermUsage && !_usageViewModel.ShortTerm.ShowProgress
            ? "\u221E"
            : string.Empty;

    public string SummaryDividerText =>
        CanRenderSelectedSummary && _showShortTermUsage && _showWeeklyUsage ? "|" : string.Empty;

    public string TrailingSummaryText =>
        CanRenderSelectedSummary && _showShortTermUsage && _showWeeklyUsage
            ? GetWeeklySummaryText()
            : string.Empty;

    public bool ShowWeeklyProgress =>
        _showWeeklyProgress &&
        _showWeeklyUsage &&
        _usageViewModel.Weekly.ShowProgress &&
        !_usageViewModel.HasNotice;

    public double WidgetOpacity => _widgetOpacity;

    public IReadOnlyList<WidgetProgressSegment> WeeklyProgressSegments =>
        Enumerable.Range(1, 10)
            .Select(index => new WidgetProgressSegment(
                index * 10 <= _usageViewModel.Weekly.UsedPercent,
                _usageViewModel.Weekly.UsedPercent is >= 80d and < 95d,
                _usageViewModel.Weekly.UsedPercent >= 95d))
            .ToArray();

    public string ToolTip => _usageViewModel.HasNotice && !_usageViewModel.IsShowingStaleData
        ? $"{_usageViewModel.StatusTitle} \u00B7 {_usageViewModel.StatusDetail}"
        : string.IsNullOrWhiteSpace(SummaryText)
            ? "Codex Usage"
            : $"Codex Usage \u00B7 {SummaryText}";

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

    public void ApplyDisplayPreferences(
        int opacityPercent,
        bool showWeeklyProgress,
        bool showShortTermUsage = true,
        bool showWeeklyUsage = true)
    {
        var opacity = Math.Clamp(opacityPercent, 65, 100) / 100d;
        if (_widgetOpacity != opacity)
        {
            _widgetOpacity = opacity;
            OnPropertyChanged(nameof(WidgetOpacity));
        }

        var normalizedShowWeeklyUsage = showWeeklyUsage ||
            (!showShortTermUsage && !showWeeklyUsage);
        if (_showShortTermUsage != showShortTermUsage || _showWeeklyUsage != normalizedShowWeeklyUsage)
        {
            _showShortTermUsage = showShortTermUsage;
            _showWeeklyUsage = normalizedShowWeeklyUsage;
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(LeadingSummaryText));
            OnPropertyChanged(nameof(UnlimitedShortTermText));
            OnPropertyChanged(nameof(SummaryDividerText));
            OnPropertyChanged(nameof(TrailingSummaryText));
            OnPropertyChanged(nameof(ToolTip));
        }

        if (_showWeeklyProgress != showWeeklyProgress)
        {
            _showWeeklyProgress = showWeeklyProgress;
        }

        OnPropertyChanged(nameof(ShowWeeklyProgress));
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

    private bool CanRenderSelectedSummary =>
        _usageViewModel.HasRefreshed &&
        (!_usageViewModel.HasNotice || _usageViewModel.IsShowingStaleData);

    private string GetShortTermSummaryText() => _usageViewModel.ShortTerm.ShowProgress
        ? $"5H {_usageViewModel.ShortTerm.UsedText}"
        : "5H \u221E";

    private string GetWeeklySummaryText() =>
        _usageViewModel.Weekly.ShowProgress
            ? $"W {_usageViewModel.Weekly.UsedText}{(_usageViewModel.IsShowingStaleData ? " \u00B7 Stale" : string.Empty)}"
            : "W not reported";
}
