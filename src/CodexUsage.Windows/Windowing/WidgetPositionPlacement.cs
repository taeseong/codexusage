using Avalonia;

namespace CodexUsage.Windows.Windowing;

internal static class WidgetPositionPlacement
{
    internal static PixelPoint ClampToWorkingArea(
        PixelPoint position,
        PixelSize widgetSize,
        PixelRect workingArea)
    {
        var maxX = Math.Max(workingArea.X, workingArea.Right - widgetSize.Width);
        var maxY = Math.Max(workingArea.Y, workingArea.Bottom - widgetSize.Height);
        return new PixelPoint(
            Math.Clamp(position.X, workingArea.X, maxX),
            Math.Clamp(position.Y, workingArea.Y, maxY));
    }
}
