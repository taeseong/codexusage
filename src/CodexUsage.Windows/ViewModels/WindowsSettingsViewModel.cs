using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexUsage.Windows.Settings;
using CodexUsage.Windows.Startup;

namespace CodexUsage.Windows.ViewModels;

internal sealed class WindowsSettingsViewModel : ObservableObject
{
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
    private string _validationMessage = string.Empty;
    private string _feedbackMessage = string.Empty;
    private StartupRegistrationStatus? _startupStatus;
    private readonly bool _persistedStartAtLogin;
    private bool _resetAlertHistoryOnSave;

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
        _startupStatus = startupStatus;
        RecoveryNotice = recoveryNotice ?? string.Empty;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        ManageHistoryCommand = new RelayCommand(() => ManageHistoryRequested?.Invoke());
        TestNotificationCommand = new RelayCommand(() => TestNotificationRequested?.Invoke());
        RepairStartupCommand = new RelayCommand(
            RepairStartup,
            () => CanRepairStartup);
    }

    public event Action<WindowsSettingsPreferences>? SaveRequested;
    public event Action? CancelRequested;
    public event Action? ManageHistoryRequested;
    public event Action? TestNotificationRequested;
    public event Action<bool>? RepairStartupRequested;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand RestoreDefaultsCommand { get; }
    public IRelayCommand ManageHistoryCommand { get; }
    public IRelayCommand TestNotificationCommand { get; }
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

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

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

    private void Save()
    {
        var warning = decimal.ToInt32(WarningThresholdPercent);
        var critical = decimal.ToInt32(CriticalThresholdPercent);
        var quietStart = decimal.ToInt32(QuietHoursStart);
        var quietEnd = decimal.ToInt32(QuietHoursEnd);
        var resetMinutes = decimal.ToInt32(ResetReminderMinutes);
        if (warning is < 1 or > 99 ||
            critical is < 2 or > 100 ||
            warning >= critical ||
            quietStart is < 0 or > 23 ||
            quietEnd is < 0 or > 23 ||
            QuietHoursEnabled && quietStart == quietEnd ||
            resetMinutes is < 5 or > 240)
        {
            ValidationMessage = "Check thresholds, quiet hours, and reset reminder values.";
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
    bool ResetAlertHistory);
