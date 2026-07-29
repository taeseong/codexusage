using Avalonia;

namespace CodexUsage.Windows;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\CodexUsage.Windows.Widget";

    [STAThread]
    public static void Main(string[] args)
    {
        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            GetMutexName(),
            out var ownsInitialInstance);

        if (!ownsInitialInstance)
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static string GetMutexName() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_PATH"))
            ? SingleInstanceMutexName
            : $"{SingleInstanceMutexName}.Capture.{Environment.ProcessId}";
}
