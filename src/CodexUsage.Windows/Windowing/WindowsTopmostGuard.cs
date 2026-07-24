using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Threading;

namespace CodexUsage.Windows.Windowing;

internal sealed class WindowsTopmostGuard : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectReorder = 0x8004;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const uint GetRoot = 2;

    private readonly Action _reapplyTopmost;
    private readonly WinEventDelegate _callback;
    private nint _foregroundHook;
    private nint _taskbarReorderHook;
    private int _reapplyQueued;
    private bool _disposed;

    public WindowsTopmostGuard(Action reapplyTopmost)
    {
        ArgumentNullException.ThrowIfNull(reapplyTopmost);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows WinEvent hooks require Windows.");
        }

        _reapplyTopmost = reapplyTopmost;
        _callback = OnWinEvent;
        var flags = WinEventOutOfContext | WinEventSkipOwnProcess;
        _foregroundHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            0,
            _callback,
            0,
            0,
            flags);
        _taskbarReorderHook = SetWinEventHook(
            EventObjectReorder,
            EventObjectReorder,
            0,
            _callback,
            0,
            0,
            flags);

        if (_foregroundHook == 0 || _taskbarReorderHook == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            Dispose();
            throw new Win32Exception(error, "Windows foreground event hooks could not be installed.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_foregroundHook != 0)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }

        if (_taskbarReorderHook != 0)
        {
            UnhookWinEvent(_taskbarReorderHook);
            _taskbarReorderHook = 0;
        }
    }

    internal static bool IsTaskbarClassName(string? className) =>
        string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal) ||
        string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.Ordinal);

    private void OnWinEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (_disposed ||
            eventType != EventSystemForeground &&
            (eventType != EventObjectReorder || !IsTaskbarWindow(window)))
        {
            return;
        }

        QueueReapply();
    }

    private void QueueReapply()
    {
        if (Interlocked.Exchange(ref _reapplyQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                Interlocked.Exchange(ref _reapplyQueued, 0);
                if (!_disposed)
                {
                    _reapplyTopmost();
                }
            },
            DispatcherPriority.Background);
    }

    private static bool IsTaskbarWindow(nint window)
    {
        if (window == 0)
        {
            return false;
        }

        var root = GetAncestor(window, GetRoot);
        var className = new StringBuilder(64);
        return GetClassName(root == 0 ? window : root, className, className.Capacity) > 0 &&
               IsTaskbarClassName(className.ToString());
    }

    private delegate void WinEventDelegate(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint eventHook,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder className, int maxCount);
}
