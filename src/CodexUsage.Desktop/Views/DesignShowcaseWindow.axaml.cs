using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace CodexUsage.Desktop.Views;

public partial class DesignShowcaseWindow : Window
{
    public DesignShowcaseWindow()
    {
        InitializeComponent();

        var capturePath = Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_PATH");
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            Opened += async (_, _) =>
            {
                await Task.Delay(300);
                var bitmap = new RenderTargetBitmap(
                    new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
                bitmap.Render(this);
                bitmap.Save(capturePath, PngBitmapEncoderOptions.Default);
                Close();
            };
        }
    }
}
