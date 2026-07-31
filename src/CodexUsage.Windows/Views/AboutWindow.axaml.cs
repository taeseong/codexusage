using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CodexUsage.Windows.Updates;

namespace CodexUsage.Windows.Views;

public partial class AboutWindow : Window
{
    private const string GitHubUrl = "https://github.com/taeseong/codexusage";
    private readonly Func<CancellationToken, Task<string>> _diagnosticsProvider;
    private readonly Func<CancellationToken, Task<AppUpdateCheckResult>> _updateCheckProvider;
    private Task<string>? _diagnosticsTask;
    private Uri? _latestReleaseUri;

    public AboutWindow()
        : this("0.1.4", "local build", _ => Task.FromResult("Diagnostics unavailable"))
    {
    }

    internal AboutWindow(
        string version,
        string revision,
        Func<CancellationToken, Task<string>>? diagnosticsProvider = null,
        Func<CancellationToken, Task<AppUpdateCheckResult>>? updateCheckProvider = null)
    {
        InitializeComponent();
        _diagnosticsProvider = diagnosticsProvider ??
            (_ => Task.FromResult("Diagnostics unavailable"));
        _updateCheckProvider = updateCheckProvider ??
            (_ => Task.FromResult(new AppUpdateCheckResult(
                UpdateCheckStatus.Unavailable,
                "Update checking is unavailable.")));
        DataContext = new AboutWindowViewModel(version, revision);
        Opened += async (_, _) => await UpdateCliStatusAsync();
    }

    public void SaveRenderedContent(string path)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    private void OnOpenGitHub(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The GitHub page could not be opened: {0}", exception.GetType().Name);
        }
    }

    public async Task<string?> CopyDiagnosticsAsync()
    {
        CopyDiagnosticsButton.IsEnabled = false;
        DiagnosticsStatusText.Text = "Collecting diagnostics...";
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                DiagnosticsStatusText.Text = "Clipboard is unavailable.";
                return null;
            }

            var diagnostics = await GetDiagnosticsAsync();
            await clipboard.SetTextAsync(diagnostics);
            DiagnosticsStatusText.Text = "Diagnostics copied.";
            return diagnostics;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "Sanitized diagnostics could not be copied: {0}",
                exception.GetType().Name);
            DiagnosticsStatusText.Text = "Unable to copy diagnostics.";
            return null;
        }
        finally
        {
            CopyDiagnosticsButton.IsEnabled = true;
        }
    }

    private async void OnCopyDiagnostics(object? sender, RoutedEventArgs e) =>
        _ = await CopyDiagnosticsAsync();

    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        DiagnosticsStatusText.Text = "Checking GitHub releases...";
        OpenReleaseButton.IsVisible = false;
        try
        {
            var result = await _updateCheckProvider(CancellationToken.None);
            DiagnosticsStatusText.Text = result.Message;
            _latestReleaseUri = result.ReleaseUri;
            OpenReleaseButton.IsVisible = result.Status == UpdateCheckStatus.UpdateAvailable &&
                                          _latestReleaseUri is not null;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Update check could not be completed: {0}", exception.GetType().Name);
            DiagnosticsStatusText.Text = "Update check is temporarily unavailable.";
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void OnOpenLatestRelease(object? sender, RoutedEventArgs e)
    {
        if (_latestReleaseUri is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_latestReleaseUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("The latest release page could not be opened: {0}", exception.GetType().Name);
        }
    }

    private async Task UpdateCliStatusAsync()
    {
        try
        {
            var diagnostics = await GetDiagnosticsAsync();
            var cli = GetDiagnosticValue(diagnostics, "Codex CLI");
            var source = GetDiagnosticValue(diagnostics, "Codex source");
            CliStatusText.Text = string.IsNullOrWhiteSpace(source)
                ? $"CLI: {cli ?? "Unavailable"}"
                : $"CLI: {cli ?? "Unavailable"} · {source}";
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Codex CLI status could not be read: {0}", exception.GetType().Name);
            CliStatusText.Text = "CLI: Unavailable";
        }
    }

    private Task<string> GetDiagnosticsAsync() =>
        _diagnosticsTask ??= _diagnosticsProvider(CancellationToken.None);

    private static string? GetDiagnosticValue(string diagnostics, string name) =>
        diagnostics.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .FirstOrDefault(parts => parts.Length == 2 && string.Equals(parts[0], name, StringComparison.Ordinal))?
            .ElementAtOrDefault(1);

    private sealed record AboutWindowViewModel(string Version, string Revision);
}
