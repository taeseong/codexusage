namespace CodexUsage.Desktop.Tests;

public sealed class MenuBarIconContractTests
{
    [Fact]
    public void StatusItem_UsesPackagedCommandPromptMarkInsteadOfAppArtwork()
    {
        var source = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "MacOSStatusItem.cs"));
        var packageScript = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "package-macos.sh"));

        Assert.DoesNotContain("chart.bar.fill", source, StringComparison.Ordinal);
        Assert.DoesNotContain("codex-mark", source, StringComparison.Ordinal);
        Assert.Contains("codex-terminal-mark", source, StringComparison.Ordinal);
        Assert.Contains(
            "private const double StatusIconSize = 18d;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Size(StatusIconSize, StatusIconSize)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SendBool(image, Selector(\"setTemplate:\"), true);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Contents/Resources/codex-terminal-mark.png",
            packageScript,
            StringComparison.Ordinal);
    }
}
