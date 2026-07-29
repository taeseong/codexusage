using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace CodexUsage.Windows.Views;

public partial class AboutWindow : Window
{
    private const string GitHubUrl = "https://github.com/taeseong/codexusage";
    private readonly Func<CancellationToken, Task<string>> _diagnosticsProvider;

    public AboutWindow()
        : this("0.1.0", _ => Task.FromResult("Diagnostics unavailable"))
    {
    }

    public AboutWindow(
        string version,
        Func<CancellationToken, Task<string>>? diagnosticsProvider = null)
    {
        InitializeComponent();
        _diagnosticsProvider = diagnosticsProvider ??
            (_ => Task.FromResult("Diagnostics unavailable"));
        DataContext = new AboutWindowViewModel(version);
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

            var diagnostics = await _diagnosticsProvider(CancellationToken.None);
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

    private sealed record AboutWindowViewModel(string Version);
}
