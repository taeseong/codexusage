using Avalonia;

namespace CodexUsage.Windows.Windowing;

internal sealed record WidgetPositionRestorePoint(
    PixelPoint Position,
    WidgetScreenHint? Screen);

internal sealed record WidgetScreenHint(PixelRect Bounds, double Scaling);

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

    internal static int FindBestScreenIndex(
        WidgetScreenHint savedScreen,
        IReadOnlyList<WidgetScreenHint> availableScreens)
    {
        ArgumentNullException.ThrowIfNull(savedScreen);
        ArgumentNullException.ThrowIfNull(availableScreens);

        if (availableScreens.Count == 0)
        {
            return -1;
        }

        var exactMatch = availableScreens
            .Select((screen, index) => (screen, index))
            .FirstOrDefault(candidate => candidate.screen.Bounds == savedScreen.Bounds);
        if (exactMatch.screen is not null)
        {
            return exactMatch.index;
        }

        return availableScreens
            .Select((screen, index) => new
            {
                Index = index,
                BoundsSizeDifference = Math.Abs(screen.Bounds.Width - savedScreen.Bounds.Width) +
                    Math.Abs(screen.Bounds.Height - savedScreen.Bounds.Height),
                ScalingDifference = Math.Abs(screen.Scaling - savedScreen.Scaling),
                OriginDistance = Math.Abs(screen.Bounds.X - savedScreen.Bounds.X) +
                    Math.Abs(screen.Bounds.Y - savedScreen.Bounds.Y),
            })
            .OrderBy(candidate => candidate.BoundsSizeDifference)
            .ThenBy(candidate => candidate.ScalingDifference)
            .ThenBy(candidate => candidate.OriginDistance)
            .First()
            .Index;
    }
}
