using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexUsage.Windows.Settings;
using CodexUsage.Windows.Startup;

namespace CodexUsage.Windows.ViewModels;

internal sealed class WindowsSettingsViewModel : ObservableObject
{
    private const string WidgetContentValidationMessage = "Choose at least one widget usage limit.";
    private bool _startAtLogin;
    private bool _usageAlertsEnabled;
    private bool _shortTermAlertsEnabled;
    private bool _weeklyAlertsEnabled;
    private decimal _warningThresholdPercent;
    private decimal _criticalThresholdPercent;
    private bool _quietHoursEnabled;
    private decimal _quietHoursStart;
    private decimal _quietHoursEnd;
    private bool _resetReminderEnabled;
    private decimal _resetReminderMinutes;
    private decimal _widgetScalePercent;
    private decimal _widgetOpacityPercent;
    private bool _showWidgetShortTermUsage;
    private bool _showWidgetWeeklyUsage;
    private bool _showWidgetWeeklyProgress;
    private int _themePreferenceIndex;
    private string _validationMessage = string.Empty;
    private string _feedbackMessage = string.Empty;
    private StartupRegistrationStatus? _startupStatus;
    private readonly bool _persistedStartAtLogin;
    private bool _resetAlertHistoryOnSave;
    private decimal _pauseAlertsHours = 1;
    private DateTimeOffset? _alertsPausedUntil;

    public WindowsSettingsViewModel(
        WindowsAppSettings settings,
        StartupRegistrationStatus? startupStatus = null,
        string? recoveryNotice = null)
    {
        _startAtLogin = settings.StartAtLogin;
        _persistedStartAtLogin = settings.StartAtLogin;
        _usageAlertsEnabled = settings.UsageAlertsEnabled;
        _shortTermAlertsEnabled = settings.ShortTermAlertsEnabled;
        _weeklyAlertsEnabled = settings.WeeklyAlertsEnabled;
        _warningThresholdPercent = settings.WarningThresholdPercent;
        _criticalThresholdPercent = settings.CriticalThresholdPercent;
        _quietHoursEnabled = settings.QuietHoursEnabled;
        _quietHoursStart = settings.QuietHoursStart;
        _quietHoursEnd = settings.QuietHoursEnd;
        _resetReminderEnabled = settings.ResetReminderEnabled;
        _resetReminderMinutes = settings.ResetReminderMinutes;
        _widgetScalePercent = settings.WidgetScalePercent;
        _widgetOpacityPercent = settings.WidgetOpacityPercent;
        _showWidgetShortTermUsage = settings.ShowWidgetShortTermUsage;
        _showWidgetWeeklyUsage = settings.ShowWidgetWeeklyUsage;
        _showWidgetWeeklyProgress = settings.ShowWidgetWeeklyProgress;
        _themePreferenceIndex = (int)settings.ThemePreference;
        _alertsPausedUntil = settings.AlertsPausedUntil is { } pausedUntil && pausedUntil > DateTimeOffset.Now
            ? pausedUntil
            : null;
        _startupStatus = startupStatus;
        RecoveryNotice = recoveryNotice ?? string.Empty;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        ManageHistoryCommand = new RelayCommand(() => ManageHistoryRequested?.Invoke());
        TestNotificationCommand = new RelayCommand(() => TestNotificationRequested?.Invoke());
        PauseAlertsCommand = new RelayCommand(PauseAlerts, () => PauseAlertsHours is >= 1 and <= 24);
        ResumeAlertsCommand = new RelayCommand(ResumeAlerts, () => AlertsPausedUntil is not null);
        RepairStartupCommand = new RelayCommand(
            RepairStartup,
            () => CanRepairStartup);
    }

    public event Action<WindowsSettingsPreferences>? SaveRequested;
    public event Action? CancelRequested;
    public event Action? ManageHistoryRequested;
    public event Action? TestNotificationRequested;
    public event Action<int>? PauseAlertsRequested;
    public event Action? ResumeAlertsRequested;
    public event Action<bool>? RepairStartupRequested;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand RestoreDefaultsCommand { get; }
    public IRelayCommand ManageHistoryCommand { get; }
    public IRelayCommand TestNotificationCommand { get; }
    public IRelayCommand PauseAlertsCommand { get; }
    public IRelayCommand ResumeAlertsCommand { get; }
    public IRelayCommand RepairStartupCommand { get; }

    public bool StartAtLogin
    {
        get => _startAtLogin;
        set
        {
            if (SetProperty(ref _startAtLogin, value))
            {
                NotifyStartupStatusChanged();
            }
        }
    }

    public bool UsageAlertsEnabled
    {
        get => _usageAlertsEnabled;
        set => SetProperty(ref _usageAlertsEnabled, value);
    }

    public bool ShortTermAlertsEnabled
    {
        get => _shortTermAlertsEnabled;
        set => SetProperty(ref _shortTermAlertsEnabled, value);
    }

    public bool WeeklyAlertsEnabled
    {
        get => _weeklyAlertsEnabled;
        set => SetProperty(ref _weeklyAlertsEnabled, value);
    }

    public decimal WarningThresholdPercent
    {
        get => _warningThresholdPercent;
        set => SetProperty(ref _warningThresholdPercent, value);
    }

    public decimal CriticalThresholdPercent
    {
        get => _criticalThresholdPercent;
        set => SetProperty(ref _criticalThresholdPercent, value);
    }

    public bool QuietHoursEnabled
    {
        get => _quietHoursEnabled;
        set => SetProperty(ref _quietHoursEnabled, value);
    }

    public decimal QuietHoursStart
    {
        get => _quietHoursStart;
        set => SetProperty(ref _quietHoursStart, value);
    }

    public decimal QuietHoursEnd
    {
        get => _quietHoursEnd;
        set => SetProperty(ref _quietHoursEnd, value);
    }

    public bool ResetReminderEnabled
    {
        get => _resetReminderEnabled;
        set => SetProperty(ref _resetReminderEnabled, value);
    }

    public decimal ResetReminderMinutes
    {
        get => _resetReminderMinutes;
        set => SetProperty(ref _resetReminderMinutes, value);
    }

    public decimal WidgetScalePercent
    {
        get => _widgetScalePercent;
        set => SetProperty(ref _widgetScalePercent, value);
    }

    public decimal WidgetOpacityPercent
    {
        get => _widgetOpacityPercent;
        set => SetProperty(ref _widgetOpacityPercent, value);
    }

    public bool ShowWidgetWeeklyProgress
    {
        get => _showWidgetWeeklyProgress;
        set => SetProperty(ref _showWidgetWeeklyProgress, value);
    }

    public bool ShowWidgetShortTermUsage
    {
        get => _showWidgetShortTermUsage;
        set => SetProperty(ref _showWidgetShortTermUsage, value);
    }

    public bool ShowWidgetWeeklyUsage
    {
        get => _showWidgetWeeklyUsage;
        set
        {
            if (SetProperty(ref _showWidgetWeeklyUsage, value))
            {
                OnPropertyChanged(nameof(CanShowWidgetWeeklyProgress));
            }
        }
    }

    public bool CanShowWidgetWeeklyProgress => ShowWidgetWeeklyUsage;

    public int ThemePreferenceIndex
    {
        get => _themePreferenceIndex;
        set => SetProperty(ref _themePreferenceIndex, value);
    }

    public decimal PauseAlertsHours
    {
        get => _pauseAlertsHours;
        set
        {
            if (SetProperty(ref _pauseAlertsHours, value))
            {
                PauseAlertsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DateTimeOffset? AlertsPausedUntil
    {
        get => _alertsPausedUntil;
        private set
        {
            if (SetProperty(ref _alertsPausedUntil, value))
            {
                OnPropertyChanged(nameof(AlertPauseStatusText));
                OnPropertyChanged(nameof(CanResumeAlerts));
                ResumeAlertsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanResumeAlerts => AlertsPausedUntil is not null;

    public string AlertPauseStatusText => AlertsPausedUntil is { } pausedUntil
        ? $"Alerts paused until {pausedUntil.ToLocalTime():HH:mm}."
        : "Alerts are active.";

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(HasWidgetContentValidationError));
                OnPropertyChanged(nameof(HasNonWidgetValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    public bool HasWidgetContentValidationError =>
        string.Equals(ValidationMessage, WidgetContentValidationMessage, StringComparison.Ordinal);

    public bool HasNonWidgetValidationError =>
        HasValidationError && !HasWidgetContentValidationError;

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set
        {
            if (SetProperty(ref _feedbackMessage, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }

    public bool HasFeedback => !string.IsNullOrEmpty(FeedbackMessage);

    public string RecoveryNotice { get; }

    public bool HasRecoveryNotice => !string.IsNullOrEmpty(RecoveryNotice);

    public string StartupStatusText => _startupStatus switch
    {
        null => "Startup status unavailable",
        _ when StartAtLogin != _persistedStartAtLogin => "Save changes to update startup registration",
        { IsRegistered: false } when _persistedStartAtLogin => "Startup entry is missing",
        { MatchesCurrentExecutable: false } when _persistedStartAtLogin => "Startup path needs repair",
        { MatchesCurrentExecutable: true } when _persistedStartAtLogin => "Registered correctly",
        { IsRegistered: true } => "A startup entry is still registered",
        _ => "Not registered",
    };

    public bool CanRepairStartup => _startupStatus is not null &&
        StartAtLogin == _persistedStartAtLogin &&
        (_persistedStartAtLogin
            ? !_startupStatus.MatchesCurrentExecutable
            : _startupStatus.IsRegistered);

    public void ShowExternalError(string message)
    {
        FeedbackMessage = string.Empty;
        ValidationMessage = message;
    }

    public void ShowFeedback(string message)
    {
        ValidationMessage = string.Empty;
        FeedbackMessage = message;
    }

    public void UpdateStartupStatus(StartupRegistrationStatus status)
    {
        _startupStatus = status;
        NotifyStartupStatusChanged();
    }

    public void UpdateAlertsPausedUntil(DateTimeOffset? pausedUntil) =>
        AlertsPausedUntil = pausedUntil is { } value && value > DateTimeOffset.Now
            ? value
            : null;

    private void Save()
    {
        var warning = decimal.ToInt32(WarningThresholdPercent);
        var critical = decimal.ToInt32(CriticalThresholdPercent);
        var quietStart = decimal.ToInt32(QuietHoursStart);
        var quietEnd = decimal.ToInt32(QuietHoursEnd);
        var resetMinutes = decimal.ToInt32(ResetReminderMinutes);
        var widgetScale = decimal.ToInt32(WidgetScalePercent);
        var widgetOpacity = decimal.ToInt32(WidgetOpacityPercent);
        if (warning is < 1 or > 99 ||
            critical is < 2 or > 100 ||
            warning >= critical ||
            quietStart is < 0 or > 23 ||
            quietEnd is < 0 or > 23 ||
            QuietHoursEnabled && quietStart == quietEnd ||
            resetMinutes is < 5 or > 240 ||
            widgetScale is < 75 or > 150 ||
            widgetOpacity is < 65 or > 100 ||
            !ShowWidgetShortTermUsage && !ShowWidgetWeeklyUsage ||
            ThemePreferenceIndex is < 0 or > 2)
        {
            ValidationMessage = !ShowWidgetShortTermUsage && !ShowWidgetWeeklyUsage
                ? WidgetContentValidationMessage
                : "Check appearance, thresholds, quiet hours, and reset reminder values.";
            return;
        }

        ValidationMessage = string.Empty;
        FeedbackMessage = string.Empty;
        SaveRequested?.Invoke(new WindowsSettingsPreferences(
            StartAtLogin,
            UsageAlertsEnabled,
            ShortTermAlertsEnabled,
            WeeklyAlertsEnabled,
            warning,
            critical,
            QuietHoursEnabled,
            quietStart,
            quietEnd,
            ResetReminderEnabled,
            resetMinutes,
            widgetScale,
            widgetOpacity,
            ShowWidgetShortTermUsage,
            ShowWidgetWeeklyUsage,
            ShowWidgetWeeklyProgress,
            (WindowsThemePreference)ThemePreferenceIndex,
            _resetAlertHistoryOnSave));
    }

    private void RestoreDefaults()
    {
        var defaults = new WindowsAppSettings();
        StartAtLogin = defaults.StartAtLogin;
        UsageAlertsEnabled = defaults.UsageAlertsEnabled;
        ShortTermAlertsEnabled = defaults.ShortTermAlertsEnabled;
        WeeklyAlertsEnabled = defaults.WeeklyAlertsEnabled;
        WarningThresholdPercent = defaults.WarningThresholdPercent;
        CriticalThresholdPercent = defaults.CriticalThresholdPercent;
        QuietHoursEnabled = defaults.QuietHoursEnabled;
        QuietHoursStart = defaults.QuietHoursStart;
        QuietHoursEnd = defaults.QuietHoursEnd;
        ResetReminderEnabled = defaults.ResetReminderEnabled;
        ResetReminderMinutes = defaults.ResetReminderMinutes;
        _resetAlertHistoryOnSave = true;
        ValidationMessage = string.Empty;
        FeedbackMessage = "Defaults loaded. Select Save to apply.";
    }

    private void RepairStartup()
    {
        if (CanRepairStartup)
        {
            RepairStartupRequested?.Invoke(_persistedStartAtLogin);
        }
    }

    private void PauseAlerts()
    {
        if (PauseAlertsHours is >= 1 and <= 24)
        {
            PauseAlertsRequested?.Invoke(decimal.ToInt32(PauseAlertsHours));
        }
    }

    private void ResumeAlerts() => ResumeAlertsRequested?.Invoke();

    private void NotifyStartupStatusChanged()
    {
        OnPropertyChanged(nameof(StartupStatusText));
        OnPropertyChanged(nameof(CanRepairStartup));
        RepairStartupCommand.NotifyCanExecuteChanged();
    }
}

internal sealed record WindowsSettingsPreferences(
    bool StartAtLogin,
    bool UsageAlertsEnabled,
    bool ShortTermAlertsEnabled,
    bool WeeklyAlertsEnabled,
    int WarningThresholdPercent,
    int CriticalThresholdPercent,
    bool QuietHoursEnabled,
    int QuietHoursStart,
    int QuietHoursEnd,
    bool ResetReminderEnabled,
    int ResetReminderMinutes,
    int WidgetScalePercent,
    int WidgetOpacityPercent,
    bool ShowWidgetShortTermUsage,
    bool ShowWidgetWeeklyUsage,
    bool ShowWidgetWeeklyProgress,
    WindowsThemePreference ThemePreference,
    bool ResetAlertHistory);
