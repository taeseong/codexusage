using System.Runtime.InteropServices;
using CodexUsage.Codex.Discovery;
using CodexUsage.Core.Usage;
using CodexUsage.Windows.Diagnostics;
using CodexUsage.Windows.Startup;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsDiagnosticsServiceTests
{
    [Fact]
    public async Task BuildAsync_IncludesOperationalStateAndRedactsTheUserProfile()
    {
        const string userProfile = @"C:\Users\PrivateUser";
        const string appData = userProfile + @"\AppData\Roaming";
        var service = new WindowsDiagnosticsService(
            _ => Task.FromResult(new CodexCliProbeResult(
                appData + @"\npm\codex.cmd",
                "codex-cli 0.145.0")),
            () => "Microsoft Windows 11",
            () => Architecture.X64,
            () => Architecture.X64,
            () => new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            [
                (appData, "%APPDATA%"),
                (userProfile, "%USERPROFILE%"),
            ]);

        var diagnostics = await service.BuildAsync(
            "0.1.1",
            "123456789abc",
            CodexUsageStatus.Success,
            new StartupRegistrationStatus(
                IsRegistered: true,
                MatchesCurrentExecutable: true,
                RegisteredCommand: $"\"{userProfile}\\CodexUsage.exe\"",
                ExpectedExecutablePath: userProfile + @"\CodexUsage.exe"),
            [
                new WindowsDiagnosticEvent(
                    new DateTimeOffset(2026, 7, 29, 11, 0, 0, TimeSpan.Zero),
                    WindowsDiagnosticEventKind.UsageLookupFailed,
                    CodexUsageStatus.ProtocolError),
            ]);

        Assert.Contains("App version: 0.1.1", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Build revision: 123456789abc", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Usage status: Success", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Codex CLI: codex-cli 0.145.0", diagnostics, StringComparison.Ordinal);
        Assert.Contains(@"Codex path: %APPDATA%\npm\codex.cmd", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Startup: Registered correctly", diagnostics, StringComparison.Ordinal);
        Assert.Contains("Recent event: 2026-07-29 11:00:00 UTC | UsageLookupFailed | ProtocolError", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateUser", diagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RegisteredCommand", diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_DoesNotExposeAnUnknownCustomDirectory()
    {
        var service = new WindowsDiagnosticsService(
            _ => Task.FromResult(new CodexCliProbeResult(
                @"E:\Confidential Project\tools\codex.exe",
                null)),
            () => "Windows",
            () => Architecture.X64,
            () => Architecture.X64,
            () => DateTimeOffset.UnixEpoch,
            []);

        var diagnostics = await service.BuildAsync(
            "0.1.1",
            "local build",
            CodexUsageStatus.CodexNotInstalled,
            startupStatus: null);

        Assert.Contains("Codex path: codex.exe (custom path)", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain("Confidential Project", diagnostics, StringComparison.Ordinal);
    }
}
