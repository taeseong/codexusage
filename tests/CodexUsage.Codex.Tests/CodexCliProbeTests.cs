using CodexUsage.Codex.Discovery;

namespace CodexUsage.Codex.Tests;

public sealed class CodexCliProbeTests
{
    [Fact]
    public async Task ProbeAsync_ReturnsTheFirstWorkingCliAndNormalizesItsVersion()
    {
        var firstDirectory = Path.Combine(Path.GetTempPath(), "codex-probe-first");
        var secondDirectory = Path.Combine(Path.GetTempPath(), "codex-probe-second");
        var executableName = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
        var first = Path.Combine(firstDirectory, executableName);
        var second = Path.Combine(secondDirectory, executableName);
        var locator = new CodexExecutableLocator(
            () => string.Join(Path.PathSeparator, firstDirectory, secondDirectory),
            [],
            path => string.Equals(path, first, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, second, StringComparison.OrdinalIgnoreCase));
        var probe = new CodexCliProbe(
            locator,
            (path, _) => Task.FromResult<string?>(
                string.Equals(path, second, StringComparison.OrdinalIgnoreCase)
                    ? "codex-cli 0.145.0\r\nignored"
                    : null));

        var result = await probe.ProbeAsync();

        Assert.Equal(Path.GetFullPath(second), result.ExecutablePath);
        Assert.Equal("codex-cli 0.145.0", result.Version);
        Assert.True(result.IsFound);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsNotFoundWhenNoCliIsAvailable()
    {
        var probe = new CodexCliProbe(
            new CodexExecutableLocator(() => string.Empty, [], _ => false),
            (_, _) => Task.FromResult<string?>("unexpected"));

        var result = await probe.ProbeAsync();

        Assert.False(result.IsFound);
        Assert.Null(result.ExecutablePath);
        Assert.Null(result.Version);
    }

    [Fact]
    public async Task ProbeAsync_DoesNotExposeUnexpectedVersionOutput()
    {
        var directory = Path.Combine(Path.GetTempPath(), "codex-probe-sensitive");
        var executableName = OperatingSystem.IsWindows() ? "codex.cmd" : "codex";
        var executable = Path.Combine(directory, executableName);
        var probe = new CodexCliProbe(
            new CodexExecutableLocator(
                () => directory,
                [],
                path => string.Equals(path, executable, StringComparison.OrdinalIgnoreCase)),
            (_, _) => Task.FromResult<string?>(
                "user@example.com token=secret codex-cli not-a-version"));

        var result = await probe.ProbeAsync();

        Assert.Equal("Version unavailable", result.Version);
        Assert.DoesNotContain("example.com", result.Version, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Version, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\Users\Example\AppData\Local\Programs\OpenAI\Codex\bin\codex.exe", "PowerShell standalone")]
    [InlineData(@"C:\Users\Example\AppData\Roaming\npm\codex.cmd", "npm")]
    [InlineData(@"C:\Users\Example\AppData\Local\Microsoft\WinGet\Links\codex.exe", "WinGet")]
    [InlineData(@"C:\Users\Example\AppData\Local\Microsoft\WindowsApps\codex.exe", "WindowsApps")]
    [InlineData(@"D:\Tools\codex.exe", "PATH or custom location")]
    public void ProbeResult_DescribesTheSelectedCliSource(string executablePath, string expectedSource)
    {
        var result = new CodexCliProbeResult(executablePath, "codex-cli 0.146.0");

        Assert.Equal(expectedSource, result.InstallationSource);
    }
}
