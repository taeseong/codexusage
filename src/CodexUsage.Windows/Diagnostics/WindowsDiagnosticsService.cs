using System.Runtime.InteropServices;
using System.Text;
using CodexUsage.Codex.Discovery;
using CodexUsage.Core.Usage;
using CodexUsage.Windows.Startup;

namespace CodexUsage.Windows.Diagnostics;

internal sealed class WindowsDiagnosticsService
{
    private readonly Func<CancellationToken, Task<CodexCliProbeResult>> _probe;
    private readonly Func<string> _osDescriptionProvider;
    private readonly Func<Architecture> _osArchitectureProvider;
    private readonly Func<Architecture> _processArchitectureProvider;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly IReadOnlyList<(string Root, string Token)> _pathRoots;

    public WindowsDiagnosticsService()
        : this(
            new CodexCliProbe().ProbeAsync,
            () => RuntimeInformation.OSDescription,
            () => RuntimeInformation.OSArchitecture,
            () => RuntimeInformation.ProcessArchitecture,
            () => DateTimeOffset.UtcNow,
            GetPathRoots())
    {
    }

    internal WindowsDiagnosticsService(
        Func<CancellationToken, Task<CodexCliProbeResult>> probe,
        Func<string> osDescriptionProvider,
        Func<Architecture> osArchitectureProvider,
        Func<Architecture> processArchitectureProvider,
        Func<DateTimeOffset> utcNowProvider,
        IReadOnlyList<(string Root, string Token)> pathRoots)
    {
        _probe = probe;
        _osDescriptionProvider = osDescriptionProvider;
        _osArchitectureProvider = osArchitectureProvider;
        _processArchitectureProvider = processArchitectureProvider;
        _utcNowProvider = utcNowProvider;
        _pathRoots = pathRoots
            .Where(static root => !string.IsNullOrWhiteSpace(root.Root))
            .OrderByDescending(static root => root.Root.Length)
            .ToArray();
    }

    public async Task<string> BuildAsync(
        string appVersion,
        string buildRevision,
        CodexUsageStatus? usageStatus,
        StartupRegistrationStatus? startupStatus,
        IReadOnlyList<WindowsDiagnosticEvent>? recentEvents = null,
        CancellationToken cancellationToken = default)
    {
        CodexCliProbeResult cli;
        try
        {
            cli = await _probe(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            cli = new CodexCliProbeResult(null, null);
        }

        var builder = new StringBuilder()
            .AppendLine("CodexUsage diagnostics")
            .Append("App version: ").AppendLine(appVersion)
            .Append("Build revision: ").AppendLine(buildRevision)
            .Append("OS: ").AppendLine(_osDescriptionProvider())
            .Append("OS architecture: ").AppendLine(_osArchitectureProvider().ToString())
            .Append("Process architecture: ").AppendLine(_processArchitectureProvider().ToString())
            .Append("Usage status: ").AppendLine(usageStatus?.ToString() ?? "Not checked")
            .Append("Codex CLI: ").AppendLine(cli.IsFound ? cli.Version ?? "Version unavailable" : "Not found")
            .Append("Codex source: ").AppendLine(cli.InstallationSource)
            .Append("Codex path: ").AppendLine(SanitizePath(cli.ExecutablePath))
            .Append("Startup: ").AppendLine(FormatStartupStatus(startupStatus))
            .Append("Generated at UTC: ")
            .AppendLine(_utcNowProvider().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var recentEvent in recentEvents?.TakeLast(12) ?? [])
        {
            builder.Append("Recent event: ")
                .Append(recentEvent.OccurredAt.ToUniversalTime().ToString(
                    "yyyy-MM-dd HH:mm:ss 'UTC'",
                    System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(recentEvent.Kind);
            if (recentEvent.UsageStatus is not null)
            {
                builder.Append(" | ").Append(recentEvent.UsageStatus);
            }

            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    private string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Not found";
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "Unavailable";
        }

        foreach (var (root, token) in _pathRoots)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return token;
            }

            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return token + Path.DirectorySeparatorChar + fullPath[prefix.Length..];
            }
        }

        return $"{Path.GetFileName(fullPath)} (custom path)";
    }

    private static string FormatStartupStatus(StartupRegistrationStatus? status) =>
        status switch
        {
            null => "Unavailable",
            { IsRegistered: false } => "Not registered",
            { MatchesCurrentExecutable: true } => "Registered correctly",
            _ => "Registered path differs",
        };

    private static IReadOnlyList<(string Root, string Token)> GetPathRoots() =>
    [
        (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
        (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
        (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "%PROGRAMFILES%"),
        (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "%PROGRAMFILES(X86)%"),
        (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
    ];
}
