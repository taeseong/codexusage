using System.Xml.Linq;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsReleaseAndSettingsContractTests
{
    [Fact]
    public void PackageScript_UsesIsolatedOutputAndWritesChecksum()
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestAssets", "package-windows.ps1"));

        Assert.Contains("-p:UseArtifactsOutput=true", script, StringComparison.Ordinal);
        Assert.Contains("ArtifactsPath=", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains(".sha256", script, StringComparison.Ordinal);
        Assert.Contains("Publish output is in use", script, StringComparison.Ordinal);
        Assert.Contains("-Filter \"*.pdb\"", script, StringComparison.Ordinal);
        Assert.Contains("[string]$ReleaseTag", script, StringComparison.Ordinal);
        Assert.Contains("Release packaging requires a clean Git working tree", script, StringComparison.Ordinal);
        Assert.Contains("SourceRevisionId", script, StringComparison.Ordinal);
        Assert.Contains("InformationalVersion", script, StringComparison.Ordinal);
        Assert.Contains("IncludeSourceRevisionInInformationalVersion", script, StringComparison.Ordinal);
        Assert.Contains("$Version+local", script, StringComparison.Ordinal);
        Assert.Contains("ValidateSet(\"win-x64\", \"win-arm64\")", script, StringComparison.Ordinal);
        Assert.Contains("[switch]$Portable", script, StringComparison.Ordinal);
        Assert.Contains("CodexUsage-$Version-$RuntimeIdentifier-portable.zip", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeQaScript_ProbesNativeStylesFocusDpiAndBothWidgetModes()
    {
        var script = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "qa-windows-runtime.ps1"));

        Assert.Contains("artifacts\\qa", script, StringComparison.Ordinal);
        Assert.Contains("Runtime probe output must remain under artifacts\\qa.", script, StringComparison.Ordinal);
        Assert.Contains("CODEX_USAGE_START_LOCKED", script, StringComparison.Ordinal);
        Assert.Contains("editing-primary", script, StringComparison.Ordinal);
        Assert.Contains("locked-secondary", script, StringComparison.Ordinal);
        Assert.Contains("Topmost", script, StringComparison.Ordinal);
        Assert.Contains("ToolWindow", script, StringComparison.Ordinal);
        Assert.Contains("Layered", script, StringComparison.Ordinal);
        Assert.Contains("NoActivate", script, StringComparison.Ordinal);
        Assert.Contains("ClickThroughMatchesMode", script, StringComparison.Ordinal);
        Assert.Contains("Visible", script, StringComparison.Ordinal);
        Assert.Contains("NotCloaked", script, StringComparison.Ordinal);
        Assert.Contains("ForegroundPreserved", script, StringComparison.Ordinal);
        Assert.Contains("StartupRegistrationPreserved", script, StringComparison.Ordinal);
        Assert.Contains("Get-CodexUsageStartupRegistration", script, StringComparison.Ordinal);
        Assert.Contains("[IO.FileShare]::None", script, StringComparison.Ordinal);
        Assert.Contains("TargetMonitor", script, StringComparison.Ordinal);
        Assert.Contains("PhysicalWidth", script, StringComparison.Ordinal);
        Assert.Contains("PhysicalHeight", script, StringComparison.Ordinal);
        Assert.Contains("DpiAtPoint", script, StringComparison.Ordinal);
        Assert.Contains("SetThreadDpiAwarenessContext", script, StringComparison.Ordinal);
        Assert.Contains("new IntPtr(-4)", script, StringComparison.Ordinal);
        Assert.Contains("did not reach its native state before the capture probe exited", script, StringComparison.Ordinal);
        Assert.Contains("[Math]::Ceiling(34 * $dpi / 96)", script, StringComparison.Ordinal);
        Assert.Contains("$targetDpi / 96.0", script, StringComparison.Ordinal);
        Assert.Contains("[int[]]$RequiredScalePercent", script, StringComparison.Ordinal);
        Assert.Contains("Required display scale percent is unavailable", script, StringComparison.Ordinal);
        Assert.Contains("RequiredScalePercent = $RequiredScalePercent", script, StringComparison.Ordinal);
        Assert.Contains("editing-scale-$scale", script, StringComparison.Ordinal);
        Assert.Contains("locked-scale-$scale", script, StringComparison.Ordinal);
        Assert.Contains("$matchingScreens[0].Screen", script, StringComparison.Ordinal);
        Assert.Contains("MoveWindowWithoutActivation", script, StringComparison.Ordinal);
        Assert.Contains("mixed-dpi-$firstScale-to-$lastScale", script, StringComparison.Ordinal);
        Assert.Contains("TransitionTargetScreen", script, StringComparison.Ordinal);
        Assert.Contains("desktop-context.png", script, StringComparison.Ordinal);
        Assert.Contains("SourceCopyWithLayeredWindows", script, StringComparison.Ordinal);
        Assert.Contains("CopyDesktop", script, StringComparison.Ordinal);
        Assert.Contains("runtime-report.json", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerUpgradeQaScript_RequiresACleanTestAccountAndPreservesLocalState()
    {
        var script = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "qa-windows-installer-upgrade.ps1"));

        Assert.Contains("BaselineInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("CandidateInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("clean test account", script, StringComparison.Ordinal);
        Assert.Contains("/VERYSILENT", script, StringComparison.Ordinal);
        Assert.Contains("DisplayVersion", script, StringComparison.Ordinal);
        Assert.Contains("win-(x64|arm64)", script, StringComparison.Ordinal);
        Assert.Contains("matching $expectedHostArchitecture Windows device", script, StringComparison.Ordinal);
        Assert.Contains("Baseline and candidate installers must target the same architecture", script, StringComparison.Ordinal);
        Assert.Contains("StateHashesPreserved", script, StringComparison.Ordinal);
        Assert.Contains("candidate-widget.png", script, StringComparison.Ordinal);
        Assert.Contains("PDB files", script, StringComparison.Ordinal);
        Assert.Contains("upgrade-report.json", script, StringComparison.Ordinal);
        Assert.Contains("$candidateProcess.Kill($true)", script, StringComparison.Ordinal);
        Assert.Contains("$probeFailure", script, StringComparison.Ordinal);
        Assert.Contains("$cleanupFailure", script, StringComparison.Ordinal);
        Assert.Contains("CleanupSucceeded", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $installDirectory -Recurse -Force", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseWorkflow_ValidatesVersionAndPublishesInstallerWithChecksum()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestAssets", "release-windows.yml"));

        Assert.Contains("must exactly match project version tag", workflow, StringComparison.Ordinal);
        Assert.Contains("RELEASE_NOTES_", workflow, StringComparison.Ordinal);
        Assert.Contains("package-windows.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("$installer.sha256", workflow, StringComparison.Ordinal);
        Assert.Contains("gh release create", workflow, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", workflow, StringComparison.Ordinal);
        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.Contains("$tag -cne \"v$projectVersion\"", workflow, StringComparison.Ordinal);
        Assert.Contains("$tag.Substring(1)", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("TrimStart", workflow, StringComparison.Ordinal);
        Assert.Contains("$tag = $env:RELEASE_TAG", workflow, StringComparison.Ordinal);
        Assert.Contains("$version = $env:RELEASE_TAG.Substring(1)", workflow, StringComparison.Ordinal);
        Assert.Contains("fetch-depth: 0", workflow, StringComparison.Ordinal);
        Assert.Contains("Release tag $tag must resolve to the checked-out commit.", workflow, StringComparison.Ordinal);
        Assert.Contains("Release packaging requires a clean Git working tree.", workflow, StringComparison.Ordinal);
        Assert.Contains("-ReleaseTag $env:RELEASE_TAG", workflow, StringComparison.Ordinal);
        Assert.Contains("-RuntimeIdentifier win-arm64", workflow, StringComparison.Ordinal);
        Assert.Contains("-Portable", workflow, StringComparison.Ordinal);
        Assert.Contains("win-arm64.exe", workflow, StringComparison.Ordinal);
        Assert.Contains("win-x64-portable.zip", workflow, StringComparison.Ordinal);
        var directRefExpressions = workflow
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("${{ github.ref_name }}", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, directRefExpressions.Length);
        Assert.All(
            directRefExpressions,
            line => Assert.Contains("RELEASE_TAG:", line, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("double-prefix", false)]
    [InlineData("uppercase-prefix", false)]
    [InlineData("missing-prefix", false)]
    public void ReleaseTagContract_RequiresExactlyOneLowercaseVPrefix(
        string form,
        bool expected)
    {
        var project = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "CodexUsage.Windows.csproj"));
        var version = project
            .Descendants("Version")
            .Select(element => element.Value)
            .Single();
        var expectedTag = $"v{version}";
        var candidate = form switch
        {
            "valid" => expectedTag,
            "double-prefix" => $"v{expectedTag}",
            "uppercase-prefix" => $"V{version}",
            "missing-prefix" => version,
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };

        Assert.Equal("v0.1.5", expectedTag);
        Assert.Equal(expected, string.Equals(candidate, expectedTag, StringComparison.Ordinal));
    }

    [Fact]
    public void SettingsWindow_ExposesStartupAlertsAndThresholds()
    {
        var window = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "SettingsWindow.axaml"));
        XNamespace avalonia = "https://github.com/avaloniaui";

        var contents = window
            .Descendants(avalonia + "CheckBox")
            .Select(element => (string?)element.Attribute("Content"))
            .ToArray();
        Assert.Contains("Run CodexUsage at Windows sign-in", contents);
        Assert.Contains("Show usage alerts", contents);
        Assert.Contains("5-hour", contents);
        Assert.Contains("Weekly", contents);
        Assert.Contains("Notify before a limit resets", contents);
        Assert.Contains("Quiet hours", contents);
        Assert.Equal(8, window.Descendants(avalonia + "NumericUpDown").Count());
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Manage");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Test notification");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Repair");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element =>
                (string?)element.Attribute("Content") == "Restore defaults" &&
                (string?)element.Attribute("Command") == "{Binding RestoreDefaultsCommand}" &&
                (string?)element.Attribute("AutomationProperties.Name")
                    == "Restore startup and notification defaults");
        Assert.Contains(
            window.Descendants(avalonia + "Border"),
            element =>
                (string?)element.Attribute("IsVisible") == "{Binding HasRecoveryNotice}" &&
                (string?)element.Attribute("AutomationProperties.Name")
                    == "Settings recovery notice");
        Assert.Contains(
            window.Descendants(avalonia + "NumericUpDown"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Widget size percent");
        Assert.Contains(
            window.Descendants(avalonia + "NumericUpDown"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Widget opacity percent");
        Assert.Contains(
            window.Descendants(avalonia + "ComboBox"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Color mode");
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Pause usage alerts");
        Assert.Contains(
            window.Descendants(avalonia + "CheckBox"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Show 5-hour widget usage");
        Assert.Contains(
            window.Descendants(avalonia + "CheckBox"),
            element => (string?)element.Attribute("AutomationProperties.Name") == "Show weekly widget usage");
        Assert.Contains(
            window.Descendants(avalonia + "TextBlock"),
            element => (string?)element.Attribute("IsVisible") == "{Binding HasWidgetContentValidationError}" &&
                       (string?)element.Attribute("Text") == "{Binding ValidationMessage}");
        Assert.Contains(
            window.Descendants(avalonia + "TextBlock"),
            element => (string?)element.Attribute("IsVisible") == "{Binding HasNonWidgetValidationError}" &&
                       (string?)element.Attribute("Text") == "{Binding ValidationMessage}");
        Assert.All(
            window.Descendants(avalonia + "NumericUpDown"),
            element => Assert.False(string.IsNullOrWhiteSpace(
                (string?)element.Attribute("AutomationProperties.Name"))));
        Assert.Contains("windows-surface", (string?)window.Root?.Attribute("Classes"));
        Assert.Contains(
            window.Descendants(avalonia + "Border"),
            element => ((string?)element.Attribute("Classes"))?.Contains(
                "windows-page-header",
                StringComparison.Ordinal) is true);
        Assert.Contains(
            window.Descendants(avalonia + "Border"),
            element => ((string?)element.Attribute("Classes"))?.Contains(
                "windows-command-bar",
                StringComparison.Ordinal) is true);
        Assert.Contains(
            window.Descendants(avalonia + "Button"),
            element => (string?)element.Attribute("Content") == "Save" &&
                       ((string?)element.Attribute("Classes"))?.Contains(
                           "windows-primary",
                           StringComparison.Ordinal) is true);
    }

    [Fact]
    public void WindowsApp_LoadsTheWindowsFluentSurfaceOverrides()
    {
        var app = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestAssets", "App.axaml"));
        var styles = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "WindowsFluentOverrides.axaml"));

        Assert.Contains("WindowsFluentOverrides.axaml", app, StringComparison.Ordinal);
        Assert.Contains("Window.windows-surface", styles, StringComparison.Ordinal);
        Assert.Contains("WindowsPageHeaderBrush", styles, StringComparison.Ordinal);
        Assert.Contains("WindowsAccentForegroundBrush", styles, StringComparison.Ordinal);
        Assert.Contains("Button.windows-primary", styles, StringComparison.Ordinal);
    }
}
