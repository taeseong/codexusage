using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CodexUsage.Desktop.UsageHistory;
using CodexUsage.Desktop.ViewModels;

namespace CodexUsage.Desktop.Views;

public partial class UsageWindow : Window
{
    private UsageWindowRestoreState? _pendingRestoreState;

    public UsageWindow()
    {
        InitializeComponent();
        DetailsTabs.SelectionChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool AllowClose { get; set; }

    public bool HasBeenOpened { get; private set; }

    public event EventHandler? StateChanged;

    public UsageWindowRestoreState CaptureRestoreState() =>
        new(
            Position.X,
            Position.Y,
            Width,
            Height,
            DetailsTabs.SelectedIndex);

    public void RestoreState(UsageWindowRestoreState state)
    {
        _pendingRestoreState = state;
        Width = Math.Max(MinWidth, state.Width);
        Height = Math.Max(MinHeight, state.Height);
        DetailsTabs.SelectedIndex = state.SelectedTabIndex is 1 ? 1 : 0;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    public void SelectHistoryTab() => DetailsTabs.SelectedIndex = 1;

    public void SaveRenderedContent(string path)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private async void OnExportHistory(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not UsageViewModel { History: { HasRecordedWindows: true } history })
        {
            return;
        }

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export CodexUsage history",
                SuggestedFileName = $"codexusage-history-{DateTime.Now:yyyyMMdd}.csv",
                FileTypeChoices = [new FilePickerFileType("CSV file") { Patterns = ["*.csv"] }],
            });
            if (file is null)
            {
                return;
            }

            await using var destination = await file.OpenWriteAsync();
            await new UsageHistoryCsvExporter().ExportAsync(history.ExportableWindows, destination);
            history.SetExportStatus("History exported locally as CSV.");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceWarning("Usage history export failed: {0}", exception.GetType().Name);
            history.SetExportStatus("History export could not be completed.");
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.R when DataContext is UsageViewModel viewModel &&
                                     viewModel.RefreshCommand.CanExecute(null):
                    viewModel.RefreshCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.D1 or Key.NumPad1:
                    DetailsTabs.SelectedIndex = 0;
                    e.Handled = true;
                    break;
                case Key.D2 or Key.NumPad2:
                    DetailsTabs.SelectedIndex = 1;
                    e.Handled = true;
                    break;
            }
        }

        base.OnKeyDown(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        HasBeenOpened = true;
        if (_pendingRestoreState is not { X: { } x, Y: { } y } state)
        {
            return;
        }

        var requested = new PixelPoint(x, y);
        var screen = Screens.ScreenFromPoint(requested) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var workingArea = screen.WorkingArea;
        var scaling = screen.Scaling;
        Width = Math.Min(Width, workingArea.Width / scaling);
        Height = Math.Min(Height, workingArea.Height / scaling);
        var pixelWidth = (int)Math.Ceiling(Width * scaling);
        var pixelHeight = (int)Math.Ceiling(Height * scaling);
        Position = new PixelPoint(
            Math.Clamp(requested.X, workingArea.X, Math.Max(workingArea.X, workingArea.Right - pixelWidth)),
            Math.Clamp(requested.Y, workingArea.Y, Math.Max(workingArea.Y, workingArea.Bottom - pixelHeight)));
    }
}

public sealed record UsageWindowRestoreState(
    int? X,
    int? Y,
    double Width,
    double Height,
    int SelectedTabIndex);
