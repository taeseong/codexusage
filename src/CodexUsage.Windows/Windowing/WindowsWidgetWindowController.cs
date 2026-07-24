using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace CodexUsage.Windows.Windowing;

internal sealed class WindowsWidgetWindowController
{
    private const int StandardStyleIndex = -16;
    private const int ExtendedStyleIndex = -20;
    private const long PopupStyle = unchecked((long)0x80000000);
    private const long LayeredStyle = 0x00080000L;
    private const long NoActivateStyle = 0x08000000L;
    private const long ToolWindowStyle = 0x00000080L;
    private const long TransparentStyle = 0x00000020L;

    private const uint NoSize = 0x0001;
    private const uint NoMove = 0x0002;
    private const uint NoActivate = 0x0010;
    private const uint FrameChanged = 0x0020;
    private const uint ShowWindow = 0x0040;
    private const uint NoOwnerZOrder = 0x0200;

    private static readonly nint Topmost = new(-1);

    public void Apply(
        Window window,
        bool clickThrough,
        Size? logicalSize = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows widget styles require Windows.");
        }

        var handle = window.TryGetPlatformHandle();
        if (handle is null || !string.Equals(handle.HandleDescriptor, "HWND", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Avalonia window does not expose a Windows HWND.");
        }

        var hwnd = handle.Handle;
        var currentWindowStyle = GetWindowLongPtr(hwnd, StandardStyleIndex).ToInt64();
        var desiredWindowStyle = CalculateWindowStyle(currentWindowStyle);
        SetWindowStyle(hwnd, StandardStyleIndex, currentWindowStyle, desiredWindowStyle);

        var currentStyle = GetWindowLongPtr(hwnd, ExtendedStyleIndex).ToInt64();
        var desiredStyle = CalculateExtendedStyle(currentStyle, clickThrough);
        SetWindowStyle(hwnd, ExtendedStyleIndex, currentStyle, desiredStyle);

        var flags = NoMove | NoActivate | FrameChanged | ShowWindow | NoOwnerZOrder;
        var width = 0;
        var height = 0;
        if (logicalSize is null)
        {
            flags |= NoSize;
        }
        else
        {
            var dpi = GetDpiForWindow(hwnd);
            var scale = dpi == 0 ? 1d : dpi / 96d;
            width = Math.Max(1, (int)Math.Round(logicalSize.Value.Width * scale));
            height = Math.Max(1, (int)Math.Round(logicalSize.Value.Height * scale));
        }

        if (!SetWindowPos(
                hwnd,
                Topmost,
                0,
                0,
                width,
                height,
                flags))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    internal static long CalculateWindowStyle(long currentStyle) =>
        currentStyle | PopupStyle;

    internal static long CalculateExtendedStyle(long currentStyle, bool clickThrough)
    {
        var style = currentStyle | LayeredStyle | NoActivateStyle | ToolWindowStyle;
        return clickThrough
            ? style | TransparentStyle
            : style & ~TransparentStyle;
    }

    private static void SetWindowStyle(
        nint hwnd,
        int index,
        long currentStyle,
        long desiredStyle)
    {
        if (desiredStyle == currentStyle)
        {
            return;
        }

        Marshal.SetLastPInvokeError(0);
        var previousStyle = SetWindowLongPtr(hwnd, index, new nint(desiredStyle));
        if (previousStyle == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newValue);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
