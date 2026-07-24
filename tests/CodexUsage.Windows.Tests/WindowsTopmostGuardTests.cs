using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsTopmostGuardTests
{
    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void IsTaskbarClassName_RecognizesWindowsTaskbars(string className)
    {
        Assert.True(WindowsTopmostGuard.IsTaskbarClassName(className));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Progman")]
    [InlineData("ApplicationFrameWindow")]
    public void IsTaskbarClassName_RejectsOtherWindows(string? className)
    {
        Assert.False(WindowsTopmostGuard.IsTaskbarClassName(className));
    }
}
