using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CodexUsage.Windows.Windowing;
using AvaloniaScreen = Avalonia.Platform.Screen;

namespace CodexUsage.Windows.Views;

public partial class UsageWidgetWindow : Window
{
    private readonly WidgetInteractionState _interactionState;
    private readonly WindowsWidgetWindowController _windowController = new();
    private WindowsTopmostGuard? _topmostGuard;
    private WidgetPositionRestorePoint? _restoredPosition;
    private int _nativeStateApplyQueued;
    private bool _allowClose;
    private double _logicalWidth = 160d;
    private double _logicalHeight = 34d;

    public UsageWidgetWindow()
        : this(new WidgetInteractionState())
    {
    }

    internal UsageWidgetWindow(WidgetInteractionState interactionState)
    {
        _interactionState = interactionState;
        InitializeComponent();
        Opened += OnOpened;
        PositionChanged += OnPositionChanged;
        _interactionState.PropertyChanged += OnInteractionStateChanged;
    }

    public void ShowWithoutActivation()
    {
        if (!IsVisible)
        {
            Show();
        }

        ApplyNativeState();
    }

    public void ApplyDisplayScale(int scalePercent)
    {
        var scale = Math.Clamp(scalePercent, 75, 150) / 100d;
        _logicalWidth = 160d * scale;
        _logicalHeight = 34d * scale;
        ApplyWindowDimensionsForCurrentMonitor();
        ApplyNativeState();
    }

    internal void RestoreSavedPosition(WidgetPositionRestorePoint restorePoint)
    {
        _restoredPosition = restorePoint;
        Position = restorePoint.Position;
    }

    internal WidgetPositionRestorePoint GetPositionRestorePoint()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.ScreenFromPoint(Position);
        return new WidgetPositionRestorePoint(
            Position,
            screen is null ? null : new WidgetScreenHint(screen.Bounds, screen.Scaling));
    }

    public void AllowClose() => _allowClose = true;

    public void SaveRenderedContent(string path)
    {
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)ClientSize.Width, (int)ClientSize.Height));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    public void SetLockingAvailable(bool available, string? reason = null)
    {
        if (!available)
        {
            _interactionState.EnterEditingMode();
            Trace.TraceWarning(
                "Widget locking remains disabled: {0}",
                reason ?? "Native window controls are unavailable.");
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostGuard?.Dispose();
        _topmostGuard = null;
        _interactionState.PropertyChanged -= OnInteractionStateChanged;
        Opened -= OnOpened;
        PositionChanged -= OnPositionChanged;
        base.OnClosed(e);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_restoredPosition is { } restorePoint &&
            FindRestoreScreen(restorePoint) is { } targetScreen)
        {
            Position = WidgetPositionPlacement.ClampToWorkingArea(
                restorePoint.Position,
                new PixelSize((int)ClientSize.Width, (int)ClientSize.Height),
                targetScreen.WorkingArea);
        }

        ApplyWindowDimensionsForCurrentMonitor();
        ApplyNativeState();
        QueueApplyNativeState();
        try
        {
            _topmostGuard ??= new WindowsTopmostGuard(ApplyNativeState);
        }
        catch (Exception exception) when (
            exception is Win32Exception or PlatformNotSupportedException)
        {
            Trace.TraceError(
                "Windows topmost event monitoring failed: {0}",
                exception.GetType().Name);
        }
    }

    private void OnInteractionStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyNativeState();
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        ApplyWindowDimensionsForCurrentMonitor();
        QueueApplyNativeState();
    }

    private void ApplyWindowDimensionsForCurrentMonitor()
    {
        var screen = Screens.ScreenFromWindow(this) ??
                     Screens.ScreenFromPoint(Position) ??
                     Screens.Primary;
        var scaling = Math.Max(1d, screen?.Scaling ?? RenderScaling);
        Width = _logicalWidth * scaling;
        Height = _logicalHeight * scaling;
        MinWidth = Width;
        MinHeight = Height;
    }

    private void QueueApplyNativeState()
    {
        if (Interlocked.Exchange(ref _nativeStateApplyQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                Interlocked.Exchange(ref _nativeStateApplyQueued, 0);
                ApplyNativeState();
            },
            DispatcherPriority.Background);
    }

    private AvaloniaScreen? FindRestoreScreen(WidgetPositionRestorePoint restorePoint)
    {
        if (restorePoint.Screen is { } savedScreen && Screens.All.Count > 0)
        {
            var availableScreens = Screens.All
                .Select(screen => new WidgetScreenHint(screen.Bounds, screen.Scaling))
                .ToArray();
            var bestIndex = WidgetPositionPlacement.FindBestScreenIndex(savedScreen, availableScreens);
            if (bestIndex >= 0)
            {
                return Screens.All[bestIndex];
            }
        }

        return Screens.ScreenFromPoint(restorePoint.Position) ?? Screens.Primary;
    }

    private void OnDragSurfacePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_interactionState.IsEditing &&
            e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount >= 2)
            {
                DetailsRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
        }
    }

    public event EventHandler? DetailsRequested;

    public event EventHandler? QuitRequested;

    private void OnQuitClicked(object? sender, RoutedEventArgs e) =>
        QuitRequested?.Invoke(this, EventArgs.Empty);

    private void ApplyNativeState()
    {
        if (!IsVisible)
        {
            return;
        }

        try
        {
            _windowController.Apply(
                this,
                _interactionState.IsClickThrough,
                new Size(_logicalWidth, _logicalHeight));
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Trace.TraceError(
                "Windows widget native controls failed: {0}",
                exception.GetType().Name);
            SetLockingAvailable(
                false,
                "Topmost or click-through could not be applied. The widget remains editable.");
        }
    }
}
