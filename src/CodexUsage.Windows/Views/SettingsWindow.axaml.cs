using Avalonia.Controls;
using Avalonia;
using Avalonia.Media.Imaging;

namespace CodexUsage.Windows.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
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
}
