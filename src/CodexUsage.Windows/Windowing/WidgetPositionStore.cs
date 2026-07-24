using System.Text.Json;
using Avalonia;

namespace CodexUsage.Windows.Windowing;

internal sealed class WidgetPositionStore
{
    private readonly Func<string> _pathProvider;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string> _readAllText;
    private readonly Action<string, string> _writeAllText;
    private readonly Action<string> _createDirectory;

    public WidgetPositionStore()
        : this(
            GetDefaultPath,
            File.Exists,
            File.ReadAllText,
            File.WriteAllText,
            directory => Directory.CreateDirectory(directory))
    {
    }

    internal WidgetPositionStore(
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

    public PixelPoint? Load()
    {
        try
        {
            var path = _pathProvider();
            if (!_fileExists(path))
            {
                return null;
            }

            var stored = JsonSerializer.Deserialize<StoredWidgetPosition>(_readAllText(path));
            return stored is null ? null : new PixelPoint(stored.X, stored.Y);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    public void Save(PixelPoint position)
    {
        try
        {
            var path = _pathProvider();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _createDirectory(directory);
            }

            _writeAllText(path, JsonSerializer.Serialize(new StoredWidgetPosition(position.X, position.Y)));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private sealed record StoredWidgetPosition(int X, int Y);

    private static string GetDefaultPath()
    {
        var testPath = Environment.GetEnvironmentVariable("CODEX_USAGE_WIDGET_POSITION_PATH");
        return !string.IsNullOrWhiteSpace(testPath)
            ? testPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsage",
                "widget-position.json");
    }
}
