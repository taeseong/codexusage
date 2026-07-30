using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CodexUsage.Codex;
using CodexUsage.Core.Usage;
using CodexUsage.Desktop.ViewModels;
using CodexUsage.Desktop.Views;
using CodexUsage.Windows.Diagnostics;
using CodexUsage.Windows.Notifications;
using CodexUsage.Windows.Recovery;
using CodexUsage.Windows.Settings;
using CodexUsage.Windows.Startup;
using CodexUsage.Windows.SystemTray;
using CodexUsage.Windows.ViewModels;
using CodexUsage.Windows.Views;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows;

public partial class App : Application
{
    private UsageViewModel? _usageViewModel;
    private WidgetSummaryViewModel? _widgetSummaryViewModel;
    private UsageWidgetWindow? _widgetWindow;
    private UsageWindow? _detailsWindow;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private CodexCliInstallWindow? _codexCliInstallWindow;
    private WidgetInteractionState? _interactionState;
    private WindowsTrayIcon? _trayIcon;
    private readonly WidgetPositionStore _widgetPositionStore = new();
    private readonly WindowsAppSettingsStore _settingsStore = new();
    private readonly WindowsStartupService _startupService = new();
    private readonly UsageThresholdNotifier _usageThresholdNotifier = new();
    private readonly JsonUsageHistoryStore _usageHistoryStore = new();
    private readonly JsonUsageSnapshotCache _usageSnapshotCache = new();
    private readonly WindowsDiagnosticsService _diagnosticsService = new();
    private readonly WindowsDiagnosticsLog _diagnosticsLog = new();
    private WindowsRefreshRecoveryService? _refreshRecoveryService;
    private UsageHistoryViewModel? _usageHistoryViewModel;
    private WindowsAppSettings _settings = new();
    private WindowsSettingsWriteGate _settingsWriteGate =
        new(WindowsSettingsRecoveryStatus.None);
    private string? _settingsRecoveryNotice;
    private bool _hasShownCodexInstallGuidance;
    private bool _isQuitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConfigureWindowsApp(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureWindowsApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _diagnosticsLog.Record(WindowsDiagnosticEventKind.AppStarted);
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _settings = _settingsStore.Load();
        ApplyThemePreference(_settings.ThemePreference);
        _settingsWriteGate = new WindowsSettingsWriteGate(
            _settingsStore.LastRecoveryStatus);
        _settingsRecoveryNotice = _settingsStore.LastRecoveryStatus switch
        {
            WindowsSettingsRecoveryStatus.CorruptFilePreserved =>
                "Damaged settings were preserved and safe defaults are active.",
            WindowsSettingsRecoveryStatus.CorruptFilePreservationFailed =>
                "Damaged settings could not be preserved. Automatic writes are paused until you review and save.",
            WindowsSettingsRecoveryStatus.ReadFailed =>
                "Settings could not be read. Automatic writes are paused until you review and save.",
            _ => null,
        };
        _usageViewModel = new UsageViewModel(
            new LiveCodexUsageProvider(),
            snapshotCache: _usageSnapshotCache);
        _usageHistoryViewModel = new UsageHistoryViewModel(_usageHistoryStore);
        _usageViewModel.History = _usageHistoryViewModel;
        _usageViewModel.PropertyChanged += OnUsagePropertyChanged;
        _usageViewModel.SnapshotRefreshed += OnSnapshotRefreshed;
        _usageViewModel.CodexInstallGuidanceRequested += ShowCodexInstallGuidance;
        _widgetSummaryViewModel = new WidgetSummaryViewModel(_usageViewModel);
        _interactionState = new WidgetInteractionState();
        if (string.Equals(
                Environment.GetEnvironmentVariable("CODEX_USAGE_START_LOCKED"),
                "1",
                StringComparison.Ordinal))
        {
            _interactionState.EnterLockedMode();
        }
        else if (_settings.WidgetMode is WidgetInteractionMode.Locked)
        {
            _interactionState.EnterLockedMode();
        }

        _widgetWindow = new UsageWidgetWindow(_interactionState)
        {
            DataContext = _widgetSummaryViewModel,
        };
        ApplyWidgetDisplayPreferences(_settings);
        if (_widgetPositionStore.Load() is { } savedPosition)
        {
            _widgetWindow.RestoreSavedPosition(savedPosition);
        }
        _widgetWindow.DetailsRequested += (_, _) => ShowDetails();
        _widgetWindow.QuitRequested += (_, _) => Quit(desktop);
        _detailsWindow = new UsageWindow
        {
            DataContext = _usageViewModel,
        };
        _detailsWindow.RestoreState(new UsageWindowRestoreState(
            _settings.DetailsWindow.X,
            _settings.DetailsWindow.Y,
            _settings.DetailsWindow.Width,
            _settings.DetailsWindow.Height,
            _settings.DetailsWindow.SelectedTabIndex));
        _detailsWindow.StateChanged += (_, _) => PersistSettings();
        desktop.MainWindow = _widgetWindow;
        desktop.ShutdownRequested += (_, args) =>
        {
            if (!_isQuitting)
            {
                args.Cancel = true;
                Quit(desktop);
            }
        };

        try
        {
            ConfigureTray(desktop);
            ReconcileStartupAtLaunch();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Windows tray or startup configuration failed: {0}", exception.GetType().Name);
            if (_trayIcon is null)
            {
                _widgetWindow.SetLockingAvailable(
                    false,
                    "The system tray is unavailable. Click-through is disabled so the widget remains recoverable.");
            }
        }

        try
        {
            _refreshRecoveryService = new WindowsRefreshRecoveryService(
                _usageViewModel.RequestImmediateRefresh);
            _refreshRecoveryService.Start();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Windows refresh recovery monitoring failed: {0}",
                exception.GetType().Name);
            _refreshRecoveryService?.Dispose();
            _refreshRecoveryService = null;
        }

        if (_settings.IsWidgetVisible)
        {
            _widgetWindow.ShowWithoutActivation();
        }
        else
        {
            _trayIcon?.UpdateWidgetVisibility(false);
        }
        StartUsage(desktop);
    }

    private void ConfigureTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_interactionState is null || _usageViewModel is null)
        {
            return;
        }

        _trayIcon = new WindowsTrayIcon(_interactionState);
        _trayIcon.VisibilityToggleRequested += (_, _) => ToggleWidgetVisibility();
        _trayIcon.ModeToggleRequested += (_, _) => ToggleInteractionMode();
        _trayIcon.RefreshRequested += OnRefreshRequested;
        _trayIcon.StartupToggleRequested += (_, _) => ToggleStartupAtLogin();
        _trayIcon.UsageAlertsToggleRequested += (_, _) => ToggleUsageAlerts();
        _trayIcon.DetailsRequested += (_, _) => ShowDetails();
        _trayIcon.SettingsRequested += (_, _) => ShowSettings();
        _trayIcon.AboutRequested += (_, _) => ShowAbout();
        _trayIcon.NotificationActivated += (_, _) => ShowDetails();
        _trayIcon.QuitRequested += (_, _) => Quit(desktop);
        _trayIcon.UpdateStartAtLogin(_settings.StartAtLogin);
        _trayIcon.UpdateUsageAlerts(_settings.UsageAlertsEnabled);
    }

    private async void StartUsage(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            if (_usageHistoryViewModel is not null)
            {
                await _usageHistoryViewModel.InitializeAsync();
            }
            if (_usageViewModel is not null)
            {
                await _usageViewModel.StartAsync();
            }

            var capturePath = Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_PATH");
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                await Task.Delay(GetCaptureDelay());
                if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_TRAY_MENU"),
                        "1",
                        StringComparison.Ordinal))
                {
                    if (_trayIcon is null)
                    {
                        throw new InvalidOperationException("The Windows tray icon was not created.");
                    }

                    _trayIcon.ShowMenuAt(new System.Drawing.Point(32, 32));
                    await Task.Delay(150);
                    _trayIcon.SaveMenuScreenshot(capturePath);
                }
                else if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_INSTALL_GUIDANCE"),
                        "1",
                        StringComparison.Ordinal))
                {
                    if (_codexCliInstallWindow is null)
                    {
                        throw new InvalidOperationException(
                            "The Codex CLI installation guidance was not shown.");
                    }

                    if (string.Equals(
                            Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_CLI_RETRY"),
                            "1",
                            StringComparison.Ordinal))
                    {
                        await RetryCodexCliDetectionAsync();
                    }

                    await Task.Delay(100);
                    _codexCliInstallWindow.SaveRenderedContent(capturePath);
                }
                else if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_DETAILS"),
                        "1",
                        StringComparison.Ordinal))
                {
                    if (string.Equals(
                            Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_HISTORY"),
                            "1",
                            StringComparison.Ordinal))
                    {
                        _detailsWindow?.SelectHistoryTab();
                    }
                    ShowDetails();
                    await Task.Delay(300);
                    _detailsWindow?.SaveRenderedContent(capturePath);
                }
                else if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_ABOUT"),
                        "1",
                        StringComparison.Ordinal))
                {
                    ShowAbout();
                    if (string.Equals(
                            Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_DIAGNOSTICS"),
                            "1",
                            StringComparison.Ordinal) &&
                        _aboutWindow is not null)
                    {
                        var diagnostics = await _aboutWindow.CopyDiagnosticsAsync();
                        var diagnosticsPath = Environment.GetEnvironmentVariable(
                            "CODEX_USAGE_CAPTURE_DIAGNOSTICS_TEXT_PATH");
                        if (diagnostics is not null &&
                            !string.IsNullOrWhiteSpace(diagnosticsPath))
                        {
                            await File.WriteAllTextAsync(diagnosticsPath, diagnostics);
                        }
                    }

                    await Task.Delay(100);
                    _aboutWindow?.SaveRenderedContent(capturePath);
                }
                else if (string.Equals(
                             Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_SETTINGS"),
                             "1",
                             StringComparison.Ordinal))
                {
                    ShowSettings();
                    if (string.Equals(
                            Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_TEST_NOTIFICATION"),
                            "1",
                            StringComparison.Ordinal))
                    {
                        await Task.Delay(100);
                        ShowTestNotification();
                    }

                    await Task.Delay(300);
                    _settingsWindow?.SaveRenderedContent(capturePath);
                }
                else
                {
                    _widgetWindow?.SaveRenderedContent(capturePath);
                }

                Quit(desktop);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("CodexUsage initialization failed: {0}", exception.GetType().Name);
            _diagnosticsLog.Record(WindowsDiagnosticEventKind.InitializationFailed);
            ShowDetails();
        }
    }

    private static TimeSpan GetCaptureDelay()
    {
        var value = Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_DELAY_MS");
        return int.TryParse(value, out var milliseconds)
            ? TimeSpan.FromMilliseconds(Math.Clamp(milliseconds, 100, 30_000))
            : TimeSpan.FromMilliseconds(300);
    }

    private async void OnRefreshRequested(object? sender, EventArgs args)
    {
        try
        {
            if (_usageViewModel is not null)
            {
                await _usageViewModel.RefreshAsync();
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("A tray refresh failed: {0}", exception.GetType().Name);
        }
    }

    private void OnUsagePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_usageViewModel is null || _isQuitting)
        {
            return;
        }

        _trayIcon?.UpdateToolTip(_usageViewModel.TrayToolTip);
        if (args.PropertyName == nameof(UsageViewModel.LastStatus) &&
            _usageViewModel.LastStatus is { } usageStatus)
        {
            _diagnosticsLog.Record(
                usageStatus is CodexUsageStatus.Success
                    ? WindowsDiagnosticEventKind.UsageLookupSucceeded
                    : WindowsDiagnosticEventKind.UsageLookupFailed,
                usageStatus);
        }

        if (args.PropertyName != nameof(UsageViewModel.IsCodexNotInstalled))
        {
            return;
        }

        if (_usageViewModel.IsCodexNotInstalled)
        {
            if (!_hasShownCodexInstallGuidance)
            {
                _hasShownCodexInstallGuidance = true;
                ShowCodexInstallGuidance();
            }

            return;
        }

        _hasShownCodexInstallGuidance = false;
        if (_codexCliInstallWindow?.IsVisible is true)
        {
            _codexCliInstallWindow.Close();
        }
    }

    private async void OnSnapshotRefreshed(CodexUsageSnapshot snapshot)
    {
        var alerts = _usageThresholdNotifier.Evaluate(snapshot, _settings, out var history);
        _settings = _settings with { AlertHistory = history };
        PersistSettings();

        foreach (var alert in alerts)
        {
            _trayIcon?.ShowUsageAlert(alert.Title, alert.Message);
        }

        if (_usageHistoryViewModel is not null)
        {
            await _usageHistoryViewModel.ObserveAsync(snapshot);
        }
    }

    private void ShowCodexInstallGuidance()
    {
        if (_codexCliInstallWindow is null)
        {
            _codexCliInstallWindow = new CodexCliInstallWindow();
            _codexCliInstallWindow.RetryRequested += OnRetryCodexCliDetection;
            _codexCliInstallWindow.Closed += (_, _) => _codexCliInstallWindow = null;
        }

        if (!_codexCliInstallWindow.IsVisible)
        {
            if (_widgetWindow is not null && _widgetWindow.IsVisible)
            {
                _codexCliInstallWindow.PositionNextTo(_widgetWindow);
            }

            _codexCliInstallWindow.Show();
        }

        _codexCliInstallWindow.Activate();
    }

    private async void OnRetryCodexCliDetection(object? sender, EventArgs args) =>
        await RetryCodexCliDetectionAsync();

    private async Task RetryCodexCliDetectionAsync()
    {
        var window = _codexCliInstallWindow;
        if (window is null || _usageViewModel is null)
        {
            return;
        }

        window.SetRetryState(isBusy: true, "Codex CLI를 다시 확인하는 중입니다.");
        try
        {
            await _usageViewModel.RefreshAsync();
            if (_usageViewModel.IsCodexNotInstalled)
            {
                window.SetRetryState(
                    isBusy: false,
                    "아직 Codex CLI를 찾지 못했습니다. 설치가 끝났는지 확인해 주세요.");
            }
            else
            {
                _hasShownCodexInstallGuidance = false;
                if (window.IsVisible)
                {
                    window.Close();
                }
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("Codex CLI retry failed: {0}", exception.GetType().Name);
            if (window.IsVisible)
            {
                window.SetRetryState(
                    isBusy: false,
                    "다시 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.");
            }
        }
    }

    private void ToggleWidgetVisibility()
    {
        if (_widgetWindow is null)
        {
            return;
        }

        if (_widgetWindow.IsVisible)
        {
            _widgetWindow.Hide();
        }
        else
        {
            _widgetWindow.ShowWithoutActivation();
        }

        _trayIcon?.UpdateWidgetVisibility(_widgetWindow.IsVisible);
        PersistSettings();
    }

    private void ToggleInteractionMode()
    {
        if (_interactionState is null || _widgetWindow is null)
        {
            return;
        }

        _interactionState.Toggle();
        if (_interactionState.IsEditing && !_widgetWindow.IsVisible)
        {
            _widgetWindow.ShowWithoutActivation();
            _trayIcon?.UpdateWidgetVisibility(true);
        }

        PersistSettings();
    }

    private void ToggleStartupAtLogin()
    {
        if (!_settingsWriteGate.CanApplyAutomaticChange)
        {
            ShowSettings();
            return;
        }

        var enabled = !_settings.StartAtLogin;
        try
        {
            _startupService.SetEnabled(enabled);
            _settings = _settings with { StartAtLogin = enabled };
            _trayIcon?.UpdateStartAtLogin(enabled);
            PersistSettings();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Windows startup registration failed: {0}", exception.GetType().Name);
        }
    }

    private void ToggleUsageAlerts()
    {
        if (!_settingsWriteGate.CanApplyAutomaticChange)
        {
            ShowSettings();
            return;
        }

        var enabled = !_settings.UsageAlertsEnabled;
        _settings = _settings with { UsageAlertsEnabled = enabled };
        _trayIcon?.UpdateUsageAlerts(enabled);
        PersistSettings();
    }

    private void ReconcileStartupAtLaunch()
    {
        if (_settingsWriteGate.CanApplyAutomaticChange)
        {
            _startupService.SetEnabled(_settings.StartAtLogin);
            return;
        }

        var startupStatus = _startupService.GetStatus();
        _settings = _settings with { StartAtLogin = startupStatus.IsRegistered };
        _trayIcon?.UpdateStartAtLogin(_settings.StartAtLogin);
    }

    private void ShowDetails()
    {
        if (_detailsWindow is null)
        {
            return;
        }

        _detailsWindow.Show();
        _detailsWindow.Activate();
    }

    private void ShowAbout()
    {
        if (_aboutWindow is null)
        {
            var provenance = BuildProvenance.FromEntryAssembly();
            _aboutWindow = new AboutWindow(
                provenance.Version,
                provenance.Revision,
                BuildDiagnosticsAsync);
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }

        if (!_aboutWindow.IsVisible)
        {
            _aboutWindow.Show();
        }

        _aboutWindow.Activate();
    }

    private async Task<string> BuildDiagnosticsAsync(CancellationToken cancellationToken)
    {
        StartupRegistrationStatus? startupStatus = null;
        try
        {
            startupStatus = _startupService.GetStatus();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Startup status was unavailable for diagnostics: {0}",
                exception.GetType().Name);
        }

        var provenance = BuildProvenance.FromEntryAssembly();
        return await _diagnosticsService.BuildAsync(
            provenance.Version,
            provenance.Revision,
            _usageViewModel?.LastStatus,
            startupStatus,
            _diagnosticsLog.ReadRecent(),
            cancellationToken);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            StartupRegistrationStatus? startupStatus = null;
            try
            {
                startupStatus = _startupService.GetStatus();
            }
            catch (Exception exception)
            {
                Trace.TraceError("Windows startup status inspection failed: {0}", exception.GetType().Name);
            }

            var viewModel = new WindowsSettingsViewModel(
                _settings,
                startupStatus,
                _settingsRecoveryNotice);
            viewModel.SaveRequested += ApplySettings;
            viewModel.CancelRequested += CloseSettings;
            viewModel.ManageHistoryRequested += OpenHistorySettings;
            viewModel.TestNotificationRequested += ShowTestNotification;
            viewModel.PauseAlertsRequested += PauseUsageAlerts;
            viewModel.ResumeAlertsRequested += ResumeUsageAlerts;
            viewModel.RepairStartupRequested += RepairStartupRegistration;
            _settingsWindow = new SettingsWindow
            {
                DataContext = viewModel,
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        _settingsWindow.Activate();
    }

    private void ApplySettings(WindowsSettingsPreferences preferences)
    {
        var previousSettings = _settings;
        try
        {
            _startupService.SetEnabled(preferences.StartAtLogin);
            var thresholdsChanged =
                _settings.WarningThresholdPercent != preferences.WarningThresholdPercent ||
                _settings.CriticalThresholdPercent != preferences.CriticalThresholdPercent;
            _settings = _settings with
            {
                StartAtLogin = preferences.StartAtLogin,
                UsageAlertsEnabled = preferences.UsageAlertsEnabled,
                ShortTermAlertsEnabled = preferences.ShortTermAlertsEnabled,
                WeeklyAlertsEnabled = preferences.WeeklyAlertsEnabled,
                WarningThresholdPercent = preferences.WarningThresholdPercent,
                CriticalThresholdPercent = preferences.CriticalThresholdPercent,
                QuietHoursEnabled = preferences.QuietHoursEnabled,
                QuietHoursStart = preferences.QuietHoursStart,
                QuietHoursEnd = preferences.QuietHoursEnd,
                ResetReminderEnabled = preferences.ResetReminderEnabled,
                ResetReminderMinutes = preferences.ResetReminderMinutes,
                WidgetScalePercent = preferences.WidgetScalePercent,
                WidgetOpacityPercent = preferences.WidgetOpacityPercent,
                ShowWidgetShortTermUsage = preferences.ShowWidgetShortTermUsage,
                ShowWidgetWeeklyUsage = preferences.ShowWidgetWeeklyUsage,
                ShowWidgetWeeklyProgress = preferences.ShowWidgetWeeklyProgress,
                ThemePreference = preferences.ThemePreference,
                AlertHistory = thresholdsChanged || preferences.ResetAlertHistory
                    ? new UsageAlertHistory()
                    : _settings.AlertHistory,
            };
            ApplyThemePreference(_settings.ThemePreference);
            ApplyWidgetDisplayPreferences(_settings);
            _trayIcon?.UpdateStartAtLogin(_settings.StartAtLogin);
            _trayIcon?.UpdateUsageAlerts(_settings.UsageAlertsEnabled);
            if (!PersistSettings(allowRecoveryOverwrite: true))
            {
                _settings = previousSettings;
                _startupService.SetEnabled(previousSettings.StartAtLogin);
                ApplyThemePreference(previousSettings.ThemePreference);
                ApplyWidgetDisplayPreferences(previousSettings);
                _trayIcon?.UpdateStartAtLogin(previousSettings.StartAtLogin);
                _trayIcon?.UpdateUsageAlerts(previousSettings.UsageAlertsEnabled);
                if (_settingsWindow?.DataContext is WindowsSettingsViewModel saveViewModel)
                {
                    saveViewModel.ShowExternalError("Unable to save settings. Your previous settings were restored.");
                }

                return;
            }

            _settingsRecoveryNotice = null;
            CloseSettings();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Windows settings update failed: {0}", exception.GetType().Name);
            if (_settingsWindow?.DataContext is WindowsSettingsViewModel viewModel)
            {
                viewModel.ShowExternalError("Unable to update Windows startup settings.");
            }
        }
    }

    private void ShowTestNotification()
    {
        if (_settingsWindow?.DataContext is not WindowsSettingsViewModel viewModel)
        {
            return;
        }

        try
        {
            if (_trayIcon is null)
            {
                throw new InvalidOperationException("The system tray is unavailable.");
            }

            _trayIcon.ShowTestNotification();
            viewModel.ShowFeedback("Test notification sent.");
        }
        catch (Exception exception)
        {
            Trace.TraceError("Windows test notification failed: {0}", exception.GetType().Name);
            viewModel.ShowExternalError("Unable to show a Windows notification.");
        }
    }

    private void PauseUsageAlerts(int hours)
    {
        if (_settingsWindow?.DataContext is not WindowsSettingsViewModel viewModel)
        {
            return;
        }

        if (!_settingsWriteGate.CanApplyAutomaticChange)
        {
            viewModel.ShowExternalError("Review and save settings before pausing alerts.");
            return;
        }

        var previousSettings = _settings;
        _settings = _settings with { AlertsPausedUntil = DateTimeOffset.Now.AddHours(hours) };
        if (!PersistSettings())
        {
            _settings = previousSettings;
            viewModel.ShowExternalError("Unable to pause alerts.");
            return;
        }

        viewModel.UpdateAlertsPausedUntil(_settings.AlertsPausedUntil);
        viewModel.ShowFeedback($"Alerts paused for {hours} {(hours == 1 ? "hour" : "hours")}.");
    }

    private void ResumeUsageAlerts()
    {
        if (_settingsWindow?.DataContext is not WindowsSettingsViewModel viewModel)
        {
            return;
        }

        if (!_settingsWriteGate.CanApplyAutomaticChange)
        {
            viewModel.ShowExternalError("Review and save settings before resuming alerts.");
            return;
        }

        var previousSettings = _settings;
        _settings = _settings with { AlertsPausedUntil = null };
        if (!PersistSettings())
        {
            _settings = previousSettings;
            viewModel.ShowExternalError("Unable to resume alerts.");
            return;
        }

        viewModel.UpdateAlertsPausedUntil(null);
        viewModel.ShowFeedback("Alerts resumed.");
    }

    private void RepairStartupRegistration(bool enabled)
    {
        if (_settingsWindow?.DataContext is not WindowsSettingsViewModel viewModel)
        {
            return;
        }

        try
        {
            _startupService.SetEnabled(enabled);
            viewModel.UpdateStartupStatus(_startupService.GetStatus());
            viewModel.ShowFeedback(enabled
                ? "Windows startup registration repaired."
                : "Windows startup registration removed.");
        }
        catch (Exception exception)
        {
            Trace.TraceError("Windows startup repair failed: {0}", exception.GetType().Name);
            viewModel.ShowExternalError("Unable to repair Windows startup registration.");
        }
    }

    private void CloseSettings() => _settingsWindow?.Close();

    private void ApplyWidgetDisplayPreferences(WindowsAppSettings settings)
    {
        _widgetSummaryViewModel?.ApplyDisplayPreferences(
            settings.WidgetOpacityPercent,
            settings.ShowWidgetWeeklyProgress,
            settings.ShowWidgetShortTermUsage,
            settings.ShowWidgetWeeklyUsage);
        _widgetWindow?.ApplyDisplayScale(settings.WidgetScalePercent);
    }

    private void ApplyThemePreference(WindowsThemePreference preference)
    {
        RequestedThemeVariant = preference switch
        {
            WindowsThemePreference.Light => ThemeVariant.Light,
            WindowsThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void OpenHistorySettings()
    {
        CloseSettings();
        _detailsWindow?.SelectHistoryTab();
        ShowDetails();
    }

    private async void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        _diagnosticsLog.Record(WindowsDiagnosticEventKind.AppStopped);
        _refreshRecoveryService?.Dispose();
        _refreshRecoveryService = null;
        if (_usageViewModel is not null)
        {
            _usageViewModel.PropertyChanged -= OnUsagePropertyChanged;
            _usageViewModel.SnapshotRefreshed -= OnSnapshotRefreshed;
            _usageViewModel.CodexInstallGuidanceRequested -= ShowCodexInstallGuidance;
            await _usageViewModel.DisposeAsync();
        }

        _detailsWindow?.Hide();
        if (_detailsWindow is not null)
        {
            _detailsWindow.AllowClose = true;
            _detailsWindow.Close();
        }

        _codexCliInstallWindow?.Close();
        _codexCliInstallWindow = null;

        _aboutWindow?.Close();
        _aboutWindow = null;

        _settingsWindow?.Close();
        _settingsWindow = null;

        if (_widgetWindow is not null)
        {
            _widgetPositionStore.Save(_widgetWindow.GetPositionRestorePoint());
            PersistSettings();
            _widgetWindow.AllowClose();
            _widgetWindow.Close();
        }

        _widgetSummaryViewModel?.Dispose();
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }

    private bool PersistSettings(bool allowRecoveryOverwrite = false)
    {
        var detailsState = _detailsWindow is { HasBeenOpened: true }
            ? _detailsWindow.CaptureRestoreState()
            : null;
        _settings = _settings with
        {
            IsWidgetVisible = _widgetWindow?.IsVisible ?? _settings.IsWidgetVisible,
            WidgetMode = _interactionState?.Mode ?? _settings.WidgetMode,
            DetailsWindow = detailsState is null
                ? _settings.DetailsWindow
                : new DetailsWindowSettings
                {
                    X = detailsState.X,
                    Y = detailsState.Y,
                    Width = detailsState.Width,
                    Height = detailsState.Height,
                    SelectedTabIndex = detailsState.SelectedTabIndex,
                },
        };
        if (!_settingsWriteGate.CanWrite(allowRecoveryOverwrite))
        {
            return false;
        }

        var saved = _settingsStore.Save(_settings);
        if (saved)
        {
            _settingsWriteGate.OnWriteSucceeded(allowRecoveryOverwrite);
        }

        if (!saved)
        {
            Trace.TraceError("Windows settings persistence failed.");
        }

        return saved;
    }
}
