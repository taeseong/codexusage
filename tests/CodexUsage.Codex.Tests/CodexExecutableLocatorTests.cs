using CodexUsage.Codex.Discovery;

namespace CodexUsage.Codex.Tests;

public sealed class CodexExecutableLocatorTests
{
    [Fact]
    public void Find_OnWindows_PrefersNpmCommandShimOverPowerShellShim()
    {
        // Given
        var npmDirectory = Path.Combine(Path.GetTempPath(), "codex-npm");
        var commandShim = Path.Combine(npmDirectory, "codex.cmd");
        var locator = new CodexExecutableLocator(
            () => npmDirectory,
            [],
            path => string.Equals(path, commandShim, StringComparison.OrdinalIgnoreCase));

        // When
        var result = locator.Find();

        // Then
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Path.GetFullPath(commandShim), result);
        }
    }

    [Fact]
    public void Find_OnWindows_PrefersExecutableAcrossTheEntirePath()
    {
        // Given
        var commandDirectory = Path.Combine(Path.GetTempPath(), "codex-command-first");
        var executableDirectory = Path.Combine(Path.GetTempPath(), "codex-executable-second");
        var commandShim = Path.Combine(commandDirectory, "codex.cmd");
        var executable = Path.Combine(executableDirectory, "codex.exe");
        var locator = new CodexExecutableLocator(
            () => string.Join(Path.PathSeparator, commandDirectory, executableDirectory),
            [],
            path => string.Equals(path, commandShim, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, executable, StringComparison.OrdinalIgnoreCase));

        // When
        var result = locator.Find();

        // Then
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Path.GetFullPath(executable), result);
        }
    }

    [Fact]
    public void Find_UsesKnownApplicationPathWhenFinderPathDoesNotContainCodex()
    {
        // Given
        const string applicationCodex = "/Applications/Codex.app/Contents/Resources/codex";
        var locator = new CodexExecutableLocator(
            () => "/usr/bin:/bin",
            [applicationCodex],
            path => path == applicationCodex);

        // When
        var result = locator.Find();

        // Then
        Assert.Equal(Path.GetFullPath(applicationCodex), result);
    }
}
