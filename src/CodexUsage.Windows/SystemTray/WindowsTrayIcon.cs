using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.SystemTray;

internal sealed class WindowsTrayIcon : IDisposable
{
    private const string IconPath = "Assets/codex-usage.ico";
    private readonly WidgetInteractionState _interactionState;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _visibilityItem;
    private readonly ToolStripMenuItem _modeItem;
    private readonly Icon _icon;
    private bool _widgetVisible = true;
    private bool _disposed;

    public WindowsTrayIcon(WidgetInteractionState interactionState)
    {
        _interactionState = interactionState;
        _visibilityItem = CreateMenuItem("Hide widget", (_, _) =>
            VisibilityToggleRequested?.Invoke(this, EventArgs.Empty));
        _modeItem = CreateMenuItem("Lock widget", (_, _) =>
            ModeToggleRequested?.Invoke(this, EventArgs.Empty));
        var refreshItem = CreateMenuItem("Refresh now", (_, _) =>
            RefreshRequested?.Invoke(this, EventArgs.Empty));
        var detailsItem = CreateMenuItem("Open details", (_, _) =>
            DetailsRequested?.Invoke(this, EventArgs.Empty));
        var aboutItem = CreateMenuItem("About CodexUsage", (_, _) =>
            AboutRequested?.Invoke(this, EventArgs.Empty));
        var quitItem = CreateMenuItem("Quit", (_, _) =>
            QuitRequested?.Invoke(this, EventArgs.Empty));

        _menu = new ContextMenuStrip
        {
            AutoSize = true,
            BackColor = DarkTrayMenuRenderer.SurfaceColor,
            ForeColor = DarkTrayMenuRenderer.TextColor,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(6, 5, 6, 5),
            Renderer = new DarkTrayMenuRenderer(),
            ShowCheckMargin = false,
            ShowImageMargin = false,
        };
        _menu.Items.Add(_visibilityItem);
        _menu.Items.Add(_modeItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(refreshItem);
        _menu.Items.Add(detailsItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(aboutItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(quitItem);

        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The process executable path is unavailable.");
        _icon = Icon.ExtractAssociatedIcon(executablePath)
            ?? throw new InvalidOperationException("The CodexUsage application icon could not be loaded.");
        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _icon,
            Text = "CodexUsage",
            Visible = true,
        };
        _trayIcon.MouseUp += OnTrayIconMouseUp;
        _interactionState.PropertyChanged += OnInteractionStatePropertyChanged;
        UpdateHeaders();
    }

    public event EventHandler? VisibilityToggleRequested;

    public event EventHandler? ModeToggleRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? DetailsRequested;

    public event EventHandler? AboutRequested;

    public event EventHandler? QuitRequested;

    public void UpdateWidgetVisibility(bool visible)
    {
        _widgetVisible = visible;
        UpdateHeaders();
    }

    public void UpdateToolTip(string toolTip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _trayIcon.Text = toolTip.Length <= 63 ? toolTip : toolTip[..63];
    }

    internal void ShowMenuAt(Point screenPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _menu.Show(screenPosition);
    }

    internal void SaveMenuScreenshot(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bounds = _menu.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("The tray menu is not visible.");
        }

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(path);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _interactionState.PropertyChanged -= OnInteractionStatePropertyChanged;
        _trayIcon.MouseUp -= OnTrayIconMouseUp;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text)
        {
            AutoSize = true,
            ForeColor = DarkTrayMenuRenderer.TextColor,
            Padding = new Padding(12, 8, 20, 8),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        item.Click += onClick;
        return item;
    }

    private void OnTrayIconMouseUp(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            VisibilityToggleRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnInteractionStatePropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        _visibilityItem.Text = _widgetVisible ? "Hide widget" : "Show widget";
        _modeItem.Text = _interactionState.IsEditing
            ? "Lock widget (click-through)"
            : "Enter edit mode";
    }
}

internal sealed class DarkTrayMenuRenderer : ToolStripRenderer
{
    internal static readonly Color SurfaceColor = Color.FromArgb(29, 31, 37);
    internal static readonly Color HoverColor = Color.FromArgb(49, 53, 63);
    internal static readonly Color DividerColor = Color.FromArgb(57, 61, 71);
    internal static readonly Color BorderColor = Color.FromArgb(45, 49, 58);
    internal static readonly Color TextColor = Color.FromArgb(233, 236, 241);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(SurfaceColor);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs eventArgs)
    {
        using var brush = new SolidBrush(eventArgs.Item.Selected ? HoverColor : SurfaceColor);
        eventArgs.Graphics.FillRectangle(brush, eventArgs.Item.ContentRectangle);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
    {
        eventArgs.TextColor = TextColor;
        base.OnRenderItemText(eventArgs);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs eventArgs)
    {
        var bounds = eventArgs.Item.ContentRectangle;
        using var pen = new Pen(DividerColor);
        eventArgs.Graphics.DrawLine(pen, 10, bounds.Top + (bounds.Height / 2), bounds.Right - 10, bounds.Top + (bounds.Height / 2));
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
    {
        using var pen = new Pen(BorderColor);
        var bounds = eventArgs.AffectedBounds;
        eventArgs.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }
}
