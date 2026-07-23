using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CodexUsage.Desktop.ViewModels;
using static CodexUsage.macOS.MenuBar.ObjectiveC;

namespace CodexUsage.macOS.MenuBar;

public sealed class MacOSStatusItem : IDisposable
{
    private const double VariableStatusItemLength = -1d;
    private const long ImageLeft = 2;
    private const double StatusIconSize = 18d;

    private static readonly ConcurrentDictionary<nint, WeakReference<MacOSStatusItem>> Instances = new();
    private static readonly ActionCallback ToggleCallback = OnToggle;
    private static readonly ActionCallback OpenCallback = OnOpen;
    private static readonly ActionCallback RefreshCallback = OnRefresh;
    private static readonly ActionCallback QuitCallback = OnQuit;
    private static readonly object TargetClassLock = new();

    private nint _statusBar;
    private nint _statusItem;
    private nint _button;
    private nint _target;
    private NativeUsagePopover? _popover;
    private string? _statusItemTitle;
    private bool _disposed;

    public MacOSStatusItem(MenuBarPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The native status item is available only on macOS.");
        }

        try
        {
            var targetClass = GetOrCreateTargetClass();
            _target = Send(Send(targetClass, Selector("alloc")), Selector("init"));
            Instances[_target] = new WeakReference<MacOSStatusItem>(this);

            _statusBar = Send(Class("NSStatusBar"), Selector("systemStatusBar"));
            _statusItem = Send(_statusBar, Selector("statusItemWithLength:"), VariableStatusItemLength);
            SendVoid(_statusItem, Selector("retain"));
            _button = Send(_statusItem, Selector("button"));
            ConfigureButton();
            _popover = new NativeUsagePopover(_target, presentation);
            Update(presentation);
        }
        catch
        {
            DisposeNativeResources();
            throw;
        }
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? QuitRequested;

    public void Update(MenuBarPresentation presentation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(presentation);

        var titleChanged = !string.Equals(_statusItemTitle, presentation.StatusItemTitle, StringComparison.Ordinal);
        if (titleChanged)
        {
            SetTitle(_button, presentation.StatusItemTitle);
            _statusItemTitle = presentation.StatusItemTitle;
        }

        SendVoid(_button, Selector("setToolTip:"), String(presentation.ToolTip));
        SendVoid(_button, Selector("setAccessibilityLabel:"), String(presentation.ToolTip));
        _popover?.Update(presentation);
        if (titleChanged)
        {
            _popover?.Reposition(_button);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeNativeResources();
    }

    private void ConfigureButton()
    {
        var image = LoadCommandPromptMark();
        if (image == 0)
        {
            image = Send(
                Class("NSImage"),
                Selector("imageWithSystemSymbolName:accessibilityDescription:"),
                String("terminal.fill"),
                String("Codex"));
        }

        if (image != 0)
        {
            SendVoid(image, Selector("setSize:"), new Size(StatusIconSize, StatusIconSize));
            SendVoid(_button, Selector("setImage:"), image);
            SendVoid(_button, Selector("setImagePosition:"), ImageLeft);
        }

        SendVoid(_button, Selector("setTarget:"), _target);
        SendVoid(_button, Selector("setAction:"), Selector("togglePopover:"));
    }

    private static nint LoadCommandPromptMark()
    {
        var bundle = Send(Class("NSBundle"), Selector("mainBundle"));
        var path = Send(
            bundle,
            Selector("pathForResource:ofType:"),
            String("codex-terminal-mark"),
            String("png"));
        if (path == 0)
        {
            return 0;
        }

        var image = Send(
            Send(Class("NSImage"), Selector("alloc")),
            Selector("initWithContentsOfFile:"),
            path);
        if (image == 0)
        {
            return 0;
        }

        SendBool(image, Selector("setTemplate:"), true);
        return Send(image, Selector("autorelease"));
    }

    private static void SetTitle(nint item, string title) =>
        SendVoid(item, Selector("setTitle:"), String(title));

    private static nint GetOrCreateTargetClass()
    {
        const string className = "CodexUsageMenuActionTarget";
        lock (TargetClassLock)
        {
            var registeredClass = RegisteredClass(className);
            if (registeredClass != 0)
            {
                return registeredClass;
            }

            var targetClass = AllocateClass(className);
            if (targetClass == 0)
            {
                throw new InvalidOperationException("Could not allocate the native action target.");
            }

            RegisterAction(targetClass, "togglePopover:", ToggleCallback);
            RegisterAction(targetClass, "openUsage:", OpenCallback);
            RegisterAction(targetClass, "refreshUsage:", RefreshCallback);
            RegisterAction(targetClass, "quitUsage:", QuitCallback);
            RegisterClass(targetClass);
            return targetClass;
        }
    }

    private static void RegisterAction(nint targetClass, string selector, ActionCallback callback)
    {
        if (!ObjectiveC.AddMethod(targetClass, selector, callback))
        {
            throw new InvalidOperationException($"Could not register the native action {selector}.");
        }
    }

    private static void OnToggle(nint target, nint selector, nint sender) =>
        Dispatch(target, instance => instance._popover?.Toggle(instance._button));

    private static void OnOpen(nint target, nint selector, nint sender) =>
        Dispatch(target, static instance => instance.OpenRequested?.Invoke(instance, EventArgs.Empty));

    private static void OnRefresh(nint target, nint selector, nint sender) =>
        Dispatch(target, static instance => instance.RefreshRequested?.Invoke(instance, EventArgs.Empty));

    private static void OnQuit(nint target, nint selector, nint sender) =>
        Dispatch(target, static instance => instance.QuitRequested?.Invoke(instance, EventArgs.Empty));

    private static void Dispatch(nint target, Action<MacOSStatusItem> action)
    {
        try
        {
            if (Instances.TryGetValue(target, out var reference) && reference.TryGetTarget(out var instance))
            {
                action(instance);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError("A macOS status item action failed: {0}", exception.GetType().Name);
        }
    }

    private void DisposeNativeResources()
    {
        _popover?.Dispose();
        _popover = null;
        if (_target != 0)
        {
            Instances.TryRemove(_target, out _);
        }

        if (_statusItem != 0)
        {
            if (_statusBar != 0)
            {
                SendVoid(_statusBar, Selector("removeStatusItem:"), _statusItem);
            }

            SendVoid(_statusItem, Selector("release"));
            _statusItem = 0;
        }

        if (_target != 0)
        {
            SendVoid(_target, Selector("release"));
            _target = 0;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActionCallback(nint target, nint selector, nint sender);
}
