using System.Text.Json;
using System.Text;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Settings;

internal sealed class WindowsAppSettingsStore
{
    private readonly Func<string> _pathProvider;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string> _readAllText;
    private readonly Action<string, string> _writeAllText;
    private readonly Action<string> _createDirectory;
    private readonly Action<string> _prepareForRead;
    private readonly Action<string> _preserveCorrupt;

    public WindowsSettingsRecoveryStatus LastRecoveryStatus { get; private set; }

    public WindowsAppSettingsStore()
        : this(GetDefaultPath)
    {
    }

    internal WindowsAppSettingsStore(string path)
        : this(() => path)
    {
    }

    private WindowsAppSettingsStore(Func<string> pathProvider)
        : this(
            pathProvider,
            File.Exists,
            File.ReadAllText,
            WriteAtomically,
            directory => Directory.CreateDirectory(directory),
            PrepareForRead,
            PreserveCorrupt)
    {
    }

    internal WindowsAppSettingsStore(
        Func<string> pathProvider,
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Action<string, string> writeAllText,
        Action<string> createDirectory)
        : this(
            pathProvider,
            fileExists,
            readAllText,
            writeAllText,
            createDirectory,
            _ => { },
            _ => { })
    {
    }

    internal WindowsAppSettingsStore(
        Func<string> pathProvider,
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Action<string, string> writeAllText,
        Action<string> createDirectory,
        Action<string> prepareForRead,
        Action<string> preserveCorrupt)
    {
        _pathProvider = pathProvider;
        _fileExists = fileExists;
        _readAllText = readAllText;
        _writeAllText = writeAllText;
        _createDirectory = createDirectory;
        _prepareForRead = prepareForRead;
        _preserveCorrupt = preserveCorrupt;
    }

    public WindowsAppSettings Load()
    {
        LastRecoveryStatus = WindowsSettingsRecoveryStatus.None;
        try
        {
            var path = _pathProvider();
            _prepareForRead(path);
            if (!_fileExists(path))
            {
                return new WindowsAppSettings();
            }

            var settings = JsonSerializer.Deserialize<WindowsAppSettings>(_readAllText(path));
            return settings is null ? new WindowsAppSettings() : Normalize(settings);
        }
        catch (JsonException)
        {
            try
            {
                _preserveCorrupt(_pathProvider());
                LastRecoveryStatus =
                    WindowsSettingsRecoveryStatus.CorruptFilePreserved;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                LastRecoveryStatus =
                    WindowsSettingsRecoveryStatus.CorruptFilePreservationFailed;
            }

            return new WindowsAppSettings();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            LastRecoveryStatus = WindowsSettingsRecoveryStatus.ReadFailed;
            return new WindowsAppSettings();
        }
    }

    public bool Save(WindowsAppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var path = _pathProvider();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _createDirectory(directory);
            }

            _writeAllText(path, JsonSerializer.Serialize(settings));
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static void WriteAtomically(string path, string json)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void PrepareForRead(string path)
    {
        var temporaryPath = path + ".tmp";
        if (!File.Exists(temporaryPath))
        {
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(temporaryPath);
            return;
        }

        File.Move(temporaryPath, path);
    }

    private static void PreserveCorrupt(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var suffix = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        File.Move(path, $"{path}.corrupt-{suffix}");
    }

    private static string GetDefaultPath()
    {
        var testPath = Environment.GetEnvironmentVariable("CODEX_USAGE_SETTINGS_PATH");
        return !string.IsNullOrWhiteSpace(testPath)
            ? testPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsage",
                "settings.json");
    }

    private static WindowsAppSettings Normalize(WindowsAppSettings settings)
    {
        var warningThreshold = Math.Clamp(settings.WarningThresholdPercent, 1, 99);
        var criticalThreshold = Math.Clamp(
            Math.Max(settings.CriticalThresholdPercent, warningThreshold + 1),
            2,
            100);
        return settings with
        {
            WidgetMode = settings.WidgetMode is WidgetInteractionMode.Locked
                ? WidgetInteractionMode.Locked
                : WidgetInteractionMode.Editing,
            WarningThresholdPercent = warningThreshold,
            CriticalThresholdPercent = criticalThreshold,
            QuietHoursStart = Math.Clamp(settings.QuietHoursStart, 0, 23),
            QuietHoursEnd = Math.Clamp(settings.QuietHoursEnd, 0, 23),
            ResetReminderMinutes = Math.Clamp(settings.ResetReminderMinutes, 5, 240),
            DetailsWindow = NormalizeDetailsWindow(settings.DetailsWindow),
            AlertHistory = NormalizeHistory(settings.AlertHistory, warningThreshold, criticalThreshold),
        };
    }

    private static DetailsWindowSettings NormalizeDetailsWindow(DetailsWindowSettings? settings)
    {
        settings ??= new DetailsWindowSettings();
        return settings with
        {
            Width = Math.Clamp(settings.Width, 380, 1600),
            Height = Math.Clamp(settings.Height, 620, 1200),
            SelectedTabIndex = settings.SelectedTabIndex is 1 ? 1 : 0,
        };
    }

    private static UsageAlertHistory NormalizeHistory(
        UsageAlertHistory? history,
        int warningThreshold,
        int criticalThreshold) =>
        new()
        {
            ShortTerm = NormalizeLimitHistory(history?.ShortTerm, warningThreshold, criticalThreshold),
            Weekly = NormalizeLimitHistory(history?.Weekly, warningThreshold, criticalThreshold),
        };

    private static UsageLimitAlertHistory? NormalizeLimitHistory(
        UsageLimitAlertHistory? history,
        int warningThreshold,
        int criticalThreshold)
    {
        if (history is null)
        {
            return null;
        }

        return history with
        {
            LastObservedPercent = Math.Clamp(history.LastObservedPercent, 0d, 100d),
            HighestNotifiedPercent = history.HighestNotifiedPercent >= criticalThreshold
                ? criticalThreshold
                : history.HighestNotifiedPercent >= warningThreshold
                    ? warningThreshold
                    : 0,
        };
    }
}

internal enum WindowsSettingsRecoveryStatus
{
    None,
    CorruptFilePreserved,
    CorruptFilePreservationFailed,
    ReadFailed,
}
