using Microsoft.Win32;

namespace CodexUsage.Windows.Startup;

internal sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexUsage";

    private readonly Func<IWindowsStartupRegistry?> _openRunKeyForRead;
    private readonly Func<IWindowsStartupRegistry?> _openRunKeyForWrite;
    private readonly Func<string?> _executablePathProvider;

    public WindowsStartupService()
        : this(
            () => OpenCurrentUserRunKey(writable: false),
            () => OpenCurrentUserRunKey(writable: true),
            () => Environment.ProcessPath)
    {
    }

    internal WindowsStartupService(
        Func<IWindowsStartupRegistry?> openRunKey,
        Func<string?> executablePathProvider)
        : this(openRunKey, openRunKey, executablePathProvider)
    {
    }

    private WindowsStartupService(
        Func<IWindowsStartupRegistry?> openRunKeyForRead,
        Func<IWindowsStartupRegistry?> openRunKeyForWrite,
        Func<string?> executablePathProvider)
    {
        _openRunKeyForRead = openRunKeyForRead;
        _openRunKeyForWrite = openRunKeyForWrite;
        _executablePathProvider = executablePathProvider;
    }

    public void SetEnabled(bool enabled)
    {
        using var runKey = _openRunKeyForWrite()
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

    public StartupRegistrationStatus GetStatus()
    {
        using var runKey = _openRunKeyForRead()
            ?? throw new InvalidOperationException("The current-user startup registry key is unavailable.");
        var executablePath = _executablePathProvider();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("The process executable path is unavailable.");
        }

        var registeredCommand = runKey.GetValue(ValueName);
        var matches = !string.IsNullOrWhiteSpace(registeredCommand) &&
            string.Equals(
                NormalizeExecutableCommand(registeredCommand),
                executablePath,
                StringComparison.OrdinalIgnoreCase);
        return new StartupRegistrationStatus(
            !string.IsNullOrWhiteSpace(registeredCommand),
            matches,
            registeredCommand,
            executablePath);
    }

    private static string NormalizeExecutableCommand(string command) =>
        command.Trim().Trim('"');

    private static IWindowsStartupRegistry? OpenCurrentUserRunKey(bool writable)
    {
        var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable);
        return key is null ? null : new RegistryStartupRegistry(key);
    }

    internal interface IWindowsStartupRegistry : IDisposable
    {
        void DeleteValue(string name, bool throwOnMissingValue);

        string? GetValue(string name);

        void SetValue(string name, string value);
    }

    private sealed class RegistryStartupRegistry(RegistryKey key) : IWindowsStartupRegistry
    {
        public void DeleteValue(string name, bool throwOnMissingValue) =>
            key.DeleteValue(name, throwOnMissingValue);

        public string? GetValue(string name) => key.GetValue(name) as string;

        public void SetValue(string name, string value) =>
            key.SetValue(name, value, RegistryValueKind.String);

        public void Dispose() => key.Dispose();
    }
}

internal sealed record StartupRegistrationStatus(
    bool IsRegistered,
    bool MatchesCurrentExecutable,
    string? RegisteredCommand,
    string ExpectedExecutablePath);
