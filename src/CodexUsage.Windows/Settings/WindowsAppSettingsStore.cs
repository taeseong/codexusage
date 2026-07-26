using System.Text.Json;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Settings;

internal sealed class WindowsAppSettingsStore
{
    private readonly Func<string> _pathProvider;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string> _readAllText;
    private readonly Action<string, string> _writeAllText;
    private readonly Action<string> _createDirectory;

    public WindowsAppSettingsStore()
        : this(
            GetDefaultPath,
            File.Exists,
            File.ReadAllText,
            File.WriteAllText,
            directory => Directory.CreateDirectory(directory))
    {
    }

    internal WindowsAppSettingsStore(
        Func<string> pathProvider,
        Func<string, bool> fileExists,
        Func<string, string> readAllText,
        Action<string, string> writeAllText,
        Action<string> createDirectory)
    {
        _pathProvider = pathProvider;
        _fileExists = fileExists;
        _readAllText = readAllText;
        _writeAllText = writeAllText;
        _createDirectory = createDirectory;
    }

    public WindowsAppSettings Load()
    {
        try
        {
            var path = _pathProvider();
            if (!_fileExists(path))
            {
                return new WindowsAppSettings();
            }

            var settings = JsonSerializer.Deserialize<WindowsAppSettings>(_readAllText(path));
            return settings is null ? new WindowsAppSettings() : Normalize(settings);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new WindowsAppSettings();
        }
    }

    public void Save(WindowsAppSettings settings)
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
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
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

    private static WindowsAppSettings Normalize(WindowsAppSettings settings) =>
        settings with
        {
            WidgetMode = settings.WidgetMode is WidgetInteractionMode.Locked
                ? WidgetInteractionMode.Locked
                : WidgetInteractionMode.Editing,
            AlertHistory = NormalizeHistory(settings.AlertHistory),
        };

    private static UsageAlertHistory NormalizeHistory(UsageAlertHistory? history) =>
        new()
        {
            ShortTerm = NormalizeLimitHistory(history?.ShortTerm),
            Weekly = NormalizeLimitHistory(history?.Weekly),
        };

    private static UsageLimitAlertHistory? NormalizeLimitHistory(UsageLimitAlertHistory? history)
    {
        if (history is null)
        {
            return null;
        }

        return history with
        {
            HighestNotifiedPercent = history.HighestNotifiedPercent >= 95
                ? 95
                : history.HighestNotifiedPercent >= 80
                    ? 80
                    : 0,
        };
    }
}
