using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Views;

public partial class CodexCliInstallWindow : Window
{
    internal const string InstallCommand = "npm install --global @openai/codex";

    public CodexCliInstallWindow()
    {
        InitializeComponent();
    }

    public void SaveRenderedContent(string path)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    public void PositionNextTo(Window widget)
    {
        var screen = widget.Screens.ScreenFromWindow(widget) ?? widget.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var scale = Math.Max(1d, widget.RenderScaling);
        var widgetSize = ToPixelSize(widget.ClientSize, scale);
        var guidanceSize = ToPixelSize(new Size(Width, Height), scale);
        Position = InstallGuidanceWindowPlacement.FindPosition(
            screen.WorkingArea,
            new PixelRect(widget.Position, widgetSize),
            guidanceSize);
    }

    private async void OnCopyInstallCommand(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            CopyStatusText.Text = "클립보드를 사용할 수 없습니다.";
            return;
        }

        try
        {
            await clipboard.SetTextAsync(InstallCommand);
            CopyStatusText.Text = "설치 명령을 복사했습니다.";
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "The Codex CLI install command could not be copied: {0}",
                exception.GetType().Name);
            CopyStatusText.Text = "복사하지 못했습니다. 명령을 직접 선택해 주세요.";
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private static PixelSize ToPixelSize(Size size, double scale) =>
        new(
            Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
}
