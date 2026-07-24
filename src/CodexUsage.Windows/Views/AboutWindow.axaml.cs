using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace CodexUsage.Windows.Views;

public partial class AboutWindow : Window
{
    private const string GitHubUrl = "https://github.com/taeseong/codexusage";

    public AboutWindow()
        : this("0.1.0")
    {
    }

    public AboutWindow(string version)
    {
        InitializeComponent();
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

    private sealed record AboutWindowViewModel(string Version);
}
