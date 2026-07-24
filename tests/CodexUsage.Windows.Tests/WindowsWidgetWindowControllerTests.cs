using Avalonia;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsWidgetWindowControllerTests
{
    [Fact]
    public void WidgetPositionStore_RoundTripsTheLastWidgetPosition()
    {
        string? storedJson = null;
        string? createdDirectory = null;
        var store = new WidgetPositionStore(
            () => @"C:\test\CodexUsage\widget-position.json",
            _ => storedJson is not null,
            _ => storedJson!,
            (_, value) => storedJson = value,
            directory => createdDirectory = directory);

        store.Save(new PixelPoint(420, 240));

        Assert.Equal(@"C:\test\CodexUsage", createdDirectory);
        Assert.Equal(new PixelPoint(420, 240), store.Load());
    }

    [Fact]
    public void WidgetPositionStore_IgnoresCorruptSavedData()
    {
        var store = new WidgetPositionStore(
            () => "widget-position.json",
            _ => true,
            _ => "not-json",
            (_, _) => { },
            _ => { });

        Assert.Null(store.Load());
    }

    [Fact]
    public void WidgetPositionPlacement_ClampsAFormerMonitorPositionIntoTheWorkingArea()
    {
        var position = WidgetPositionPlacement.ClampToWorkingArea(
            new PixelPoint(5000, -300),
            new PixelSize(160, 34),
            new PixelRect(0, 0, 1920, 1080));

        Assert.Equal(new PixelPoint(1760, 0), position);
    }

    [Fact]
    public void InstallGuidancePlacement_PrefersTheSpaceBelowTheWidget()
    {
        var position = InstallGuidanceWindowPlacement.FindPosition(
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(865, 522, 190, 36),
            new PixelSize(480, 360));

        Assert.Equal(new PixelPoint(720, 570), position);
    }

    [Fact]
    public void InstallGuidancePlacement_UsesTheSpaceAboveWhenBelowWouldLeaveTheScreen()
    {
        var position = InstallGuidanceWindowPlacement.FindPosition(
            new PixelRect(0, 0, 1920, 1080),
            new PixelRect(865, 900, 190, 36),
            new PixelSize(480, 360));

        Assert.Equal(new PixelPoint(720, 528), position);
    }

    [Fact]
    public void InstallGuidancePlacement_ClampsAbovePlacementHorizontallyWithoutOverlappingAnEdgeWidget()
    {
        var widgetBounds = new PixelRect(0, 900, 190, 36);
        var position = InstallGuidanceWindowPlacement.FindPosition(
            new PixelRect(0, 0, 1920, 1080),
            widgetBounds,
            new PixelSize(480, 360));

        var guidanceBounds = new PixelRect(position, new PixelSize(480, 360));
        Assert.Equal(new PixelPoint(0, 528), position);
        Assert.False(guidanceBounds.Intersects(widgetBounds));
    }

    [Fact]
    public void CalculateWindowStyle_AddsPopupStyle()
    {
        const long popupStyle = 0x80000000L;

        var style = WindowsWidgetWindowController.CalculateWindowStyle(0x10000000L);

        Assert.Equal(popupStyle, style & popupStyle);
    }

    private const long Layered = 0x00080000L;
    private const long NoActivate = 0x08000000L;
    private const long ToolWindow = 0x00000080L;
    private const long Transparent = 0x00000020L;

    [Fact]
    public void EditingStyleExcludesClickThroughAndPreservesExistingFlags()
    {
        const long existing = 0x00000100L | Transparent;

        var style = WindowsWidgetWindowController.CalculateExtendedStyle(existing, clickThrough: false);

        Assert.Equal(existing & 0x00000100L, style & 0x00000100L);
        Assert.Equal(0, style & Transparent);
        Assert.Equal(Layered | NoActivate | ToolWindow, style & (Layered | NoActivate | ToolWindow));
    }

    [Fact]
    public void LockedStyleAddsClickThroughWithoutRemovingExistingFlags()
    {
        const long existing = 0x00000100L;

        var style = WindowsWidgetWindowController.CalculateExtendedStyle(existing, clickThrough: true);

        Assert.Equal(existing, style & existing);
        Assert.Equal(
            Layered | NoActivate | ToolWindow | Transparent,
            style & (Layered | NoActivate | ToolWindow | Transparent));
    }
}
