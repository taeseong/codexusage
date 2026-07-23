using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace CodexUsage.Desktop.Views;

public partial class UsageWindow : Window
{
    public UsageWindow()
    {
        InitializeComponent();
    }

    public bool AllowClose { get; set; }

    public void SaveRenderedContent(string path)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
