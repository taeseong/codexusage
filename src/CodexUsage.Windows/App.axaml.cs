using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodexUsage.Codex;
using CodexUsage.Desktop.ViewModels;
using CodexUsage.Desktop.Views;
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
    private AboutWindow? _aboutWindow;
    private CodexCliInstallWindow? _codexCliInstallWindow;
    private WidgetInteractionState? _interactionState;
    private WindowsTrayIcon? _trayIcon;
    private readonly WidgetPositionStore _widgetPositionStore = new();
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
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _usageViewModel = new UsageViewModel(new LiveCodexUsageProvider());
        _usageViewModel.PropertyChanged += OnUsagePropertyChanged;
        _widgetSummaryViewModel = new WidgetSummaryViewModel(_usageViewModel);
        _interactionState = new WidgetInteractionState();
        if (string.Equals(
                Environment.GetEnvironmentVariable("CODEX_USAGE_START_LOCKED"),
                "1",
                StringComparison.Ordinal))
        {
            _interactionState.EnterLockedMode();
        }

        _widgetWindow = new UsageWidgetWindow(_interactionState)
        {
            DataContext = _widgetSummaryViewModel,
        };
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
        }
        catch (Exception exception)
        {
            Trace.TraceError("The Windows tray icon could not be created: {0}", exception.GetType().Name);
            _widgetWindow.SetLockingAvailable(
                false,
                "The system tray is unavailable. Click-through is disabled so the widget remains recoverable.");
        }

        _widgetWindow.ShowWithoutActivation();
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
        _trayIcon.DetailsRequested += (_, _) => ShowDetails();
        _trayIcon.AboutRequested += (_, _) => ShowAbout();
        _trayIcon.QuitRequested += (_, _) => Quit(desktop);
    }

    private async void StartUsage(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
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

                    await Task.Delay(100);
                    _codexCliInstallWindow.SaveRenderedContent(capturePath);
                }
                else if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_DETAILS"),
                        "1",
                        StringComparison.Ordinal))
                {
                    ShowDetails();
                    await Task.Delay(100);
                    _detailsWindow?.SaveRenderedContent(capturePath);
                }
                else if (string.Equals(
                        Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_ABOUT"),
                        "1",
                        StringComparison.Ordinal))
                {
                    ShowAbout();
                    await Task.Delay(100);
                    _aboutWindow?.SaveRenderedContent(capturePath);
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
        if (args.PropertyName == nameof(UsageViewModel.IsCodexNotInstalled) &&
            _usageViewModel.IsCodexNotInstalled &&
            !_hasShownCodexInstallGuidance)
        {
            _hasShownCodexInstallGuidance = true;
            ShowCodexInstallGuidance();
        }
    }

    private void ShowCodexInstallGuidance()
    {
        if (_codexCliInstallWindow is null)
        {
            _codexCliInstallWindow = new CodexCliInstallWindow();
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
            _aboutWindow = new AboutWindow(GetAppVersion());
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }

        if (!_aboutWindow.IsVisible)
        {
            _aboutWindow.Show();
        }

        _aboutWindow.Activate();
    }

    private static string GetAppVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";

    private async void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        if (_usageViewModel is not null)
        {
            _usageViewModel.PropertyChanged -= OnUsagePropertyChanged;
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

        if (_widgetWindow is not null)
        {
            _widgetPositionStore.Save(_widgetWindow.Position);
            _widgetWindow.AllowClose();
            _widgetWindow.Close();
        }

        _widgetSummaryViewModel?.Dispose();
        _trayIcon?.Dispose();
        desktop.Shutdown();
    }
}
