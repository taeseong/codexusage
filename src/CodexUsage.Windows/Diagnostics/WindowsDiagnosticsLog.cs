using CodexUsage.Core.Usage;

namespace CodexUsage.Windows.Diagnostics;

internal enum WindowsDiagnosticEventKind
{
    AppStarted,
    AppStopped,
    InitializationFailed,
    UsageLookupSucceeded,
    UsageLookupFailed,
}

internal sealed record WindowsDiagnosticEvent(
    DateTimeOffset OccurredAt,
    WindowsDiagnosticEventKind Kind,
    CodexUsageStatus? UsageStatus);

internal sealed class WindowsDiagnosticsLog
{
    private const int MaximumEvents = 40;
    private readonly object _sync = new();
    private readonly string _path;
    private readonly TimeProvider _timeProvider;

    public WindowsDiagnosticsLog()
        : this(GetDefaultPath(), TimeProvider.System)
    {
    }

    internal WindowsDiagnosticsLog(string path, TimeProvider timeProvider)
    {
        _path = path;
        _timeProvider = timeProvider;
    }

    public void Record(WindowsDiagnosticEventKind kind, CodexUsageStatus? usageStatus = null)
    {
        var entry = new WindowsDiagnosticEvent(_timeProvider.GetUtcNow(), kind, usageStatus);
        lock (_sync)
        {
            try
            {
                var events = ReadCore().Append(entry).TakeLast(MaximumEvents).ToArray();
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = _path + ".tmp";
                File.WriteAllLines(temporaryPath, events.Select(Serialize));
                File.Move(temporaryPath, _path, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Diagnostics must never prevent usage refresh or app shutdown.
            }
        }
    }

    public IReadOnlyList<WindowsDiagnosticEvent> ReadRecent()
    {
        lock (_sync)
        {
            try
            {
                return ReadCore().TakeLast(MaximumEvents).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }

    private IEnumerable<WindowsDiagnosticEvent> ReadCore()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        return File.ReadLines(_path)
            .Select(TryParse)
            .OfType<WindowsDiagnosticEvent>()
            .TakeLast(MaximumEvents)
            .ToArray();
    }

    private static string Serialize(WindowsDiagnosticEvent entry) =>
        string.Join(
            "|",
            entry.OccurredAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            entry.Kind,
            entry.UsageStatus?.ToString() ?? string.Empty);

    private static WindowsDiagnosticEvent? TryParse(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 3 ||
            !DateTimeOffset.TryParse(
                parts[0],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var occurredAt) ||
            !Enum.TryParse<WindowsDiagnosticEventKind>(parts[1], ignoreCase: false, out var kind))
        {
            return null;
        }

        CodexUsageStatus? status = null;
        if (!string.IsNullOrWhiteSpace(parts[2]))
        {
            if (!Enum.TryParse<CodexUsageStatus>(parts[2], ignoreCase: false, out var parsedStatus))
            {
                return null;
            }

            status = parsedStatus;
        }

        return new WindowsDiagnosticEvent(occurredAt, kind, status);
    }

    private static string GetDefaultPath() =>
        Environment.GetEnvironmentVariable("CODEX_USAGE_DIAGNOSTICS_LOG_PATH")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsage",
            "diagnostics.log");
}
