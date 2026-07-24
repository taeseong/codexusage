using Avalonia;

namespace CodexUsage.Windows.Windowing;

internal static class InstallGuidanceWindowPlacement
{
    private const int Margin = 12;

    internal static PixelPoint FindPosition(
        PixelRect workingArea,
        PixelRect widgetBounds,
        PixelSize guidanceSize)
    {
        var centeredX = widgetBounds.X + ((widgetBounds.Width - guidanceSize.Width) / 2);
        var centeredY = widgetBounds.Y + ((widgetBounds.Height - guidanceSize.Height) / 2);
        var candidates = new[]
        {
            new PixelPoint(centeredX, widgetBounds.Bottom + Margin),
            new PixelPoint(centeredX, widgetBounds.Y - guidanceSize.Height - Margin),
            new PixelPoint(widgetBounds.Right + Margin, centeredY),
            new PixelPoint(widgetBounds.X - guidanceSize.Width - Margin, centeredY),
        };

        foreach (var candidate in candidates)
        {
            var clampedCandidate = ClampToWorkingArea(candidate, guidanceSize, workingArea);
            var candidateBounds = new PixelRect(clampedCandidate, guidanceSize);
            if (!candidateBounds.Intersects(widgetBounds))
            {
                return clampedCandidate;
            }
        }

        return ClampToWorkingArea(candidates[0], guidanceSize, workingArea);
    }

    private static PixelPoint ClampToWorkingArea(
        PixelPoint position,
        PixelSize size,
        PixelRect workingArea)
    {
        var maxX = Math.Max(workingArea.X, workingArea.Right - size.Width);
        var maxY = Math.Max(workingArea.Y, workingArea.Bottom - size.Height);
        return new PixelPoint(
            Math.Clamp(position.X, workingArea.X, maxX),
            Math.Clamp(position.Y, workingArea.Y, maxY));
    }
}
