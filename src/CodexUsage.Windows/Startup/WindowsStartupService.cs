using Microsoft.Win32;

namespace CodexUsage.Windows.Startup;

internal sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsage";

    private readonly Func<IWindowsStartupRegistry?> _openRunKey;
    private readonly Func<string?> _executablePathProvider;

    public WindowsStartupService()
        : this(
            OpenCurrentUserRunKey,
            () => Environment.ProcessPath)
    {
    }

    internal WindowsStartupService(
        Func<IWindowsStartupRegistry?> openRunKey,
        Func<string?> executablePathProvider)
    {
        _openRunKey = openRunKey;
        _executablePathProvider = executablePathProvider;
    }

    public void SetEnabled(bool enabled)
    {
        using var runKey = _openRunKey()
            ?? throw new InvalidOperationException("The current-user startup registry key is unavailable.");

        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = _executablePathProvider();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The process executable path is unavailable.");
        }

        runKey.SetValue(ValueName, $"\"{executablePath}\"");
    }

    private static IWindowsStartupRegistry? OpenCurrentUserRunKey()
    {
        var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        return key is null ? null : new RegistryStartupRegistry(key);
    }

    internal interface IWindowsStartupRegistry : IDisposable
    {
        void DeleteValue(string name, bool throwOnMissingValue);

        void SetValue(string name, string value);
    }

    private sealed class RegistryStartupRegistry(RegistryKey key) : IWindowsStartupRegistry
    {
        public void DeleteValue(string name, bool throwOnMissingValue) =>
            key.DeleteValue(name, throwOnMissingValue);

        public void SetValue(string name, string value) =>
            key.SetValue(name, value, RegistryValueKind.String);

        public void Dispose() => key.Dispose();
    }
}
