using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.Diagnostics;
using CodexUsage.Codex;
using CodexUsage.Desktop.ViewModels;
using CodexUsage.Desktop.Views;
using CodexUsage.macOS.MenuBar;

namespace CodexUsage.macOS;

public partial class App : Application
{
    private UsageViewModel? _usageViewModel;
    private UsageWindow? _usageWindow;
    private MacOSStatusItem? _statusItem;
    private bool _isQuitting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        RequestedThemeVariant = Environment.GetEnvironmentVariable("CODEX_USAGE_THEME") switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Environment.GetCommandLineArgs().Contains("--showcase", StringComparer.Ordinal))
            {
                desktop.MainWindow = new DesignShowcaseWindow();
            }
            else
            {
                ConfigureUsageApp(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureUsageApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        SetAccessoryActivationPolicy();
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _usageViewModel = new UsageViewModel(new LiveCodexUsageProvider());
        _usageWindow = new UsageWindow { DataContext = _usageViewModel };
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
            _statusItem = new MacOSStatusItem(MenuBarPresentation.From(_usageViewModel));
            _statusItem.OpenRequested += (_, _) => ShowUsageWindow();
            _statusItem.RefreshRequested += OnRefreshRequested;
            _statusItem.QuitRequested += (_, _) => Quit(desktop);
            _usageViewModel.PropertyChanged += (_, _) => UpdateMenuBar();
        }
        catch (Exception exception)
        {
            Trace.TraceError("The macOS status item could not be created: {0}", exception.GetType().Name);
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = _usageWindow;
            _usageWindow.AllowClose = true;
            ShowUsageWindow();
        }

        StartUsage(desktop);
    }

    private async void StartUsage(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await InitializeUsageAsync(desktop);
        }
        catch (Exception exception)
        {
            Trace.TraceError("Codex Usage initialization failed: {0}", exception.GetType().Name);
            ShowUsageWindow();
        }
    }

    private async Task InitializeUsageAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_usageViewModel is null || _usageWindow is null)
        {
            return;
        }

        await _usageViewModel.StartAsync();
        UpdateMenuBar();

        var capturePath = Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_PATH");
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            ShowUsageWindow();
            await Task.Delay(300);
            _usageWindow.SaveRenderedContent(capturePath);
            Quit(desktop);
        }
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
            Trace.TraceError("A menu bar refresh failed: {0}", exception.GetType().Name);
        }
    }

    private void UpdateMenuBar()
    {
        if (_usageViewModel is null || _statusItem is null || _isQuitting)
        {
            return;
        }

        _statusItem.Update(MenuBarPresentation.From(_usageViewModel));
    }

    private void ShowUsageWindow()
    {
        if (_usageWindow is null)
        {
            return;
        }

        _usageWindow.Show();
        _usageWindow.Activate();
    }

    private static void SetAccessoryActivationPolicy() =>
        ObjectiveC.SendBoolResult(
            ObjectiveC.Send(ObjectiveC.Class("NSApplication"), ObjectiveC.Selector("sharedApplication")),
            ObjectiveC.Selector("setActivationPolicy:"),
            1L);

    private async void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_isQuitting)
        {
            return;
        }

        _isQuitting = true;
        if (_usageViewModel is not null)
        {
            await _usageViewModel.DisposeAsync();
        }

        if (_usageWindow is not null)
        {
            _usageWindow.AllowClose = true;
            _usageWindow.Close();
        }

        _statusItem?.Dispose();
        desktop.Shutdown();
    }
}
