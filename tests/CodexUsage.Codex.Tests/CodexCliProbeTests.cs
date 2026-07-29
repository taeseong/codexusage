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
}
