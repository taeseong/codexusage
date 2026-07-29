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
        Assert.Contains("$targetDpi / 96.0", script, StringComparison.Ordinal);
        Assert.Contains("desktop-context.png", script, StringComparison.Ordinal);
        Assert.Contains("SourceCopyWithLayeredWindows", script, StringComparison.Ordinal);
        Assert.Contains("CopyDesktop", script, StringComparison.Ordinal);
        Assert.Contains("runtime-report.json", script, StringComparison.Ordinal);
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
        var directRefExpressions = workflow
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("${{ github.ref_name }}", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, directRefExpressions.Length);
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

        Assert.Equal("v0.1.2", expectedTag);
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
        Assert.Equal(5, window.Descendants(avalonia + "NumericUpDown").Count());
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
        Assert.All(
            window.Descendants(avalonia + "NumericUpDown"),
            element => Assert.False(string.IsNullOrWhiteSpace(
                (string?)element.Attribute("AutomationProperties.Name"))));
    }
}
