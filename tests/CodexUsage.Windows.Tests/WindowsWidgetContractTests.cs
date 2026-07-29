using System.Xml.Linq;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsWidgetContractTests
{
    private static readonly XNamespace ControlsNamespace = "using:CodexUsage.Desktop.Controls";

    [Fact]
    public void WidgetDeclaresCompactNonActivatingTransparentTopmostWindowContract()
    {
        var document = XDocument.Load(TestAsset("UsageWidgetWindow.axaml"));
        var window = Assert.IsType<XElement>(document.Root);

        Assert.Equal("None", window.Attribute("WindowDecorations")?.Value);
        Assert.Equal("False", window.Attribute("ShowInTaskbar")?.Value);
        Assert.Equal("False", window.Attribute("ShowActivated")?.Value);
        Assert.Equal("True", window.Attribute("Topmost")?.Value);
        Assert.Equal("Transparent", window.Attribute("Background")?.Value);
        Assert.Equal("Transparent", window.Attribute("TransparencyLevelHint")?.Value);
        Assert.Equal("160", window.Attribute("Width")?.Value);
        Assert.Equal("160", window.Attribute("MinWidth")?.Value);
        Assert.Equal("34", window.Attribute("Height")?.Value);
        Assert.Equal("34", window.Attribute("MinHeight")?.Value);
        Assert.Contains(
            window.Descendants(),
            element => element.Attributes().Any(
                           attribute => attribute.Name.LocalName == "Name" &&
                                        attribute.Value == "DragSurface") &&
                       element.Attribute("Padding")?.Value == "6,2");
        Assert.Empty(window.Descendants(ControlsNamespace + "UsageLimitCard"));
        Assert.Contains(
            window.Descendants().Attributes("Text"),
            attribute => attribute.Value.Contains("SummaryText", StringComparison.Ordinal));
        Assert.Contains(
            window.Descendants().Attributes("ColumnDefinitions"),
            attribute => attribute.Value == "24,*,6");
        Assert.Contains(
            window.Descendants().Attributes("ColumnSpacing"),
            attribute => attribute.Value == "8");
        Assert.Equal(2, window.Descendants().Count(
            element => element.Name.LocalName == "Path"));
        Assert.DoesNotContain(
            window.Descendants(),
            element => element.Attributes().Any(
                attribute => attribute.Name.LocalName == "Name" &&
                             attribute.Value == "LockButton"));
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "MenuItem" &&
                       element.Attribute("Header")?.Value == "Quit" &&
                       element.Attribute("Click")?.Value == "OnQuitClicked");

        var codeBehind = File.ReadAllText(TestAsset("UsageWidgetWindow.cs"));
        Assert.Contains("new Size(160d, 34d)", codeBehind, StringComparison.Ordinal);
        Assert.Contains(
            window.Descendants().Attributes("Text"),
            attribute => attribute.Value.Contains("SummaryDividerText", StringComparison.Ordinal));
        Assert.Contains(
            window.Descendants().Attributes("ColumnDefinitions"),
            attribute => attribute.Value == "*,Auto,*");
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "Grid" &&
                       element.Attribute("ColumnDefinitions")?.Value == "*,Auto,*" &&
                       element.Attribute("VerticalAlignment")?.Value == "Bottom" &&
                       element.Attribute("Margin")?.Value == "0,0,0,2");
        Assert.Contains(
            window.Descendants().Attributes("ColumnDefinitions"),
            attribute => attribute.Value == "Auto,*");
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "TextBlock" &&
                       element.Attribute("Text")?.Value.Contains("LeadingSummaryText", StringComparison.Ordinal) is true &&
                       element.Attribute("HorizontalAlignment")?.Value == "Left");
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "TextBlock" &&
                       element.Attribute("Text")?.Value.Contains("UnlimitedShortTermText", StringComparison.Ordinal) is true &&
                       element.Attribute("HorizontalAlignment")?.Value == "Center" &&
                       element.Attribute("Grid.Column")?.Value == "1");
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "TextBlock" &&
                       element.Attribute("Text")?.Value.Contains("TrailingSummaryText", StringComparison.Ordinal) is true &&
                       element.Attribute("HorizontalAlignment")?.Value == "Right");
        Assert.Contains(
            window.Descendants(),
            element => element.Name.LocalName == "ItemsControl" &&
                       element.Attribute("ItemsSource")?.Value.Contains("WeeklyProgressSegments", StringComparison.Ordinal) is true);
        Assert.Contains(
            window.Descendants().Attributes("Classes.warning"),
            attribute => attribute.Value.Contains("IsWarning", StringComparison.Ordinal));
        Assert.Contains(
            window.Descendants().Attributes("Classes.critical"),
            attribute => attribute.Value.Contains("IsCritical", StringComparison.Ordinal));
        Assert.Contains(
            window.Descendants().Attributes("Value"),
            attribute => attribute.Value.Contains("UsageAccentBrush", StringComparison.Ordinal));
    }

    [Fact]
    public void DetailsWindowRetainsBothUsageLimitCards()
    {
        var document = XDocument.Load(TestAsset("UsageWindow.axaml"));
        var window = Assert.IsType<XElement>(document.Root);

        Assert.Equal(
            2,
            window.Descendants(ControlsNamespace + "UsageLimitCard").Count());
    }

    [Fact]
    public void AppStartsCompactWidgetAndKeepsDetailsAsASeparateWindow()
    {
        var source = File.ReadAllText(TestAsset("WindowsApp.cs"));

        Assert.Contains("new WidgetSummaryViewModel", source, StringComparison.Ordinal);
        Assert.Contains("new UsageWidgetWindow", source, StringComparison.Ordinal);
        Assert.Contains("new UsageWindow", source, StringComparison.Ordinal);
        Assert.Contains("WidgetPositionStore", source, StringComparison.Ordinal);
        Assert.Contains("RestoreSavedPosition", source, StringComparison.Ordinal);
        Assert.Contains("GetPositionRestorePoint", source, StringComparison.Ordinal);
        Assert.Contains("DetailsRequested", source, StringComparison.Ordinal);
        Assert.Contains("QuitRequested", source, StringComparison.Ordinal);
        Assert.Contains(
            "thresholdsChanged || preferences.ResetAlertHistory",
            source,
            StringComparison.Ordinal);
        Assert.Contains("ReconcileStartupAtLaunch", source, StringComparison.Ordinal);
        Assert.Contains(
            "if (_settingsWriteGate.CanApplyAutomaticChange)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "_settings = _settings with { StartAtLogin = startupStatus.IsRegistered };",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CodexNotFoundGuidanceContainsOfficialCommandAndInProcessRetry()
    {
        var document = XDocument.Load(TestAsset("CodexCliInstallWindow.axaml"));
        var text = string.Join(
            " ",
            document.Descendants().Attributes("Text").Select(attribute => attribute.Value));
        var source = File.ReadAllText(TestAsset("WindowsApp.cs"));

        Assert.Contains("npm install --global @openai/codex", text, StringComparison.Ordinal);
        Assert.Contains("앱을 재시작하지 않고", text, StringComparison.Ordinal);
        Assert.Contains("다시 확인", text, StringComparison.Ordinal);
        Assert.Contains("IsCodexNotInstalled", source, StringComparison.Ordinal);
        Assert.Contains("_hasShownCodexInstallGuidance", source, StringComparison.Ordinal);
        Assert.Contains(
            "CodexInstallGuidanceRequested += ShowCodexInstallGuidance",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "CodexInstallGuidanceRequested -= ShowCodexInstallGuidance",
            source,
            StringComparison.Ordinal);
        Assert.Contains("RetryRequested", source, StringComparison.Ordinal);
        Assert.Contains("OnRetryCodexCliDetection", source, StringComparison.Ordinal);
        Assert.Contains("RefreshAsync", source, StringComparison.Ordinal);
        Assert.Equal(
            "avares://CodexUsage/Assets/codex-usage.ico",
            document.Root?.Attribute("Icon")?.Value);
    }

    [Fact]
    public void CodexNotFoundGuidanceAlignsTheUsageMarkSlightlyToTheLeft()
    {
        var document = XDocument.Load(TestAsset("CodexCliInstallWindow.axaml"));
        var usageMark = Assert.Single(
            document.Descendants(),
            element => element.Name.LocalName == "TextBlock" &&
                       element.Attribute("Text")?.Value == ">_");

        Assert.Equal("Left", usageMark.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("7,0,0,0", usageMark.Attribute("Margin")?.Value);
    }

    [Fact]
    public void ManifestUsesPerMonitorV2DpiAwareness()
    {
        var manifest = File.ReadAllText(TestAsset("app.manifest"));

        Assert.Contains("PerMonitorV2", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void TrayContractContainsStyledClickThroughRecoveryAndQuitActions()
    {
        var source = File.ReadAllText(TestAsset("WindowsTrayIcon.cs"));

        Assert.Contains("Enter edit mode", source, StringComparison.Ordinal);
        Assert.Contains("Lock widget (click-through)", source, StringComparison.Ordinal);
        Assert.Contains("Quit", source, StringComparison.Ordinal);
        Assert.Contains("codex-usage.ico", source, StringComparison.Ordinal);
        Assert.Contains("ContextMenuStrip", source, StringComparison.Ordinal);
        Assert.Contains("DarkTrayMenuRenderer", source, StringComparison.Ordinal);
        Assert.Contains("Segoe UI", source, StringComparison.Ordinal);
        Assert.Contains("ShowImageMargin = false", source, StringComparison.Ordinal);
        Assert.Contains("ShowMenuAt", source, StringComparison.Ordinal);
        Assert.Contains("SaveMenuScreenshot", source, StringComparison.Ordinal);
        Assert.Contains("About CodexUsage", source, StringComparison.Ordinal);
        Assert.Contains("AboutRequested", source, StringComparison.Ordinal);
        Assert.Contains("Run at sign-in", source, StringComparison.Ordinal);
        Assert.Contains("Usage alerts", source, StringComparison.Ordinal);
        Assert.Contains("BalloonTipClicked", source, StringComparison.Ordinal);
        Assert.Contains("NotificationActivated", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutWindowCentersTheBrandVersionAndGitHubAddress()
    {
        var document = XDocument.Load(TestAsset("AboutWindow.axaml"));
        var source = File.ReadAllText(TestAsset("AboutWindow.cs"));

        Assert.Equal("CenterScreen", document.Root?.Attribute("WindowStartupLocation")?.Value);
        Assert.Equal("False", document.Root?.Attribute("ShowInTaskbar")?.Value);
        Assert.Contains(document.Descendants().Attributes("Text"), attribute => attribute.Value == ">_");
        Assert.Contains(
            document.Descendants().Attributes("Text"),
            attribute => attribute.Value == "{Binding Version, StringFormat=Version {0}}");
        Assert.Contains(document.Descendants().Attributes("Content"), attribute =>
            attribute.Value == "github.com/taeseong/codexusage");
        Assert.DoesNotContain(document.Descendants().Attributes("Content"), attribute =>
            attribute.Value == "Close");
        Assert.Contains("https://github.com/taeseong/codexusage", source, StringComparison.Ordinal);
        Assert.Contains(document.Descendants().Attributes("Content"), attribute =>
            attribute.Value == "Copy diagnostics");
        Assert.Contains("OnCopyDiagnostics", source, StringComparison.Ordinal);
        Assert.Contains("Diagnostics copied.", source, StringComparison.Ordinal);

        var appSource = File.ReadAllText(TestAsset("WindowsApp.cs"));
        Assert.Contains("AboutRequested", appSource, StringComparison.Ordinal);
        Assert.Contains("ShowAbout", appSource, StringComparison.Ordinal);
        Assert.Contains("BuildDiagnosticsAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("NotificationActivated", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsExecutableIconAssetIsIncludedForTrayAndShellUse()
    {
        var iconPath = TestAsset("codex-usage.ico");

        Assert.True(File.Exists(iconPath));
        Assert.True(new FileInfo(iconPath).Length > 0);

        using var reader = new BinaryReader(File.OpenRead(iconPath));
        Assert.Equal((ushort)0, reader.ReadUInt16());
        Assert.Equal((ushort)1, reader.ReadUInt16());
        Assert.Equal((ushort)8, reader.ReadUInt16());
    }

    private static string TestAsset(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", name);
}
