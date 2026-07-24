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
            SingleInstanceMutexName,
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
}
