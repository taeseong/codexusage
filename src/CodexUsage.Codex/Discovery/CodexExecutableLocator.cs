namespace CodexUsage.Codex.Discovery;

public sealed class CodexExecutableLocator
{
    private const string CodexInstallDirectoryVariable = "CODEX_INSTALL_DIR";
    private readonly Func<string?> _pathProvider;
    private readonly IReadOnlyList<string> _knownPaths;
    private readonly Func<string, bool> _fileExists;

    public CodexExecutableLocator()
        : this(
            GetCurrentSearchPath,
            GetKnownPaths(),
            File.Exists)
    {
    }

    internal CodexExecutableLocator(
        Func<string?> pathProvider,
        IReadOnlyList<string> knownPaths,
        Func<string, bool> fileExists)
    {
        _pathProvider = pathProvider;
        _knownPaths = knownPaths;
        _fileExists = fileExists;
    }

    public string? Find() => FindAll().FirstOrDefault();

    internal IReadOnlyList<string> FindAll()
    {
        var executableNames = GetExecutableNames();
        var candidates = new List<string>();
        var path = _pathProvider();
        if (!string.IsNullOrWhiteSpace(path))
        {
            var directories = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var executableName in executableNames)
            {
                foreach (var directory in directories)
                {
                    var resolved = Resolve(Path.Combine(directory, executableName));
                    if (resolved is not null)
                    {
                        candidates.Add(resolved);
                    }
                }
            }
        }

        foreach (var knownPath in _knownPaths)
        {
            var resolved = Resolve(knownPath);
            if (resolved is not null)
            {
                candidates.Add(resolved);
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> GetExecutableNames() =>
        OperatingSystem.IsWindows()
            // npm's Windows shim is codex.cmd. Never select codex.ps1: it is subject to
            // the user's PowerShell execution policy and is not needed by the app server.
            ? ["codex.exe", "codex.cmd", "codex.bat"]
            : ["codex"];

    private string? Resolve(string candidate)
    {
        try
        {
            return _fileExists(candidate) ? Path.GetFullPath(candidate) : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> GetKnownPaths()
    {
        var paths = new List<string>();
        if (IsForcedNotFoundCapture())
        {
            return paths;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            paths.AddRange(
                GetWindowsStandalonePaths(
                    localAppData,
                    Environment.GetEnvironmentVariable(CodexInstallDirectoryVariable)));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                paths.Add(Path.Combine(appData, "npm", "codex.cmd"));
            }

            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                paths.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "codex.exe"));
                paths.Add(Path.Combine(localAppData, "Microsoft", "WindowsApps", "codex.exe"));
            }

            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                paths.Add(Path.Combine(userProfile, ".local", "bin", "codex.exe"));
            }

            return paths;
        }

        if (!OperatingSystem.IsMacOS())
        {
            return paths;
        }

        paths.AddRange(
        [
            "/Applications/Codex.app/Contents/Resources/codex",
        ]);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            paths.Add(Path.Combine(userProfile, "Applications/Codex.app/Contents/Resources/codex"));
            paths.Add(Path.Combine(userProfile, ".local/bin/codex"));
        }

        return paths;
    }

    internal static IReadOnlyList<string> GetWindowsStandalonePaths(
        string? localAppData,
        string? configuredInstallDirectory)
    {
        var paths = new List<string>();

        // The official PowerShell installer writes codex.exe here by default. A caller can
        // change that visible command directory with CODEX_INSTALL_DIR, so probe it first.
        AddCodexExecutablePath(paths, configuredInstallDirectory);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            AddCodexExecutablePath(
                paths,
                Path.Combine(localAppData, "Programs", "OpenAI", "Codex", "bin"));
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddCodexExecutablePath(ICollection<string> paths, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            var candidate = Path.Combine(directory.Trim(), "codex.exe");
            _ = Path.GetFullPath(candidate);
            paths.Add(candidate);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A malformed user environment variable must not prevent the remaining
            // safe discovery locations from being checked.
        }
    }

    private static string? GetCurrentSearchPath()
    {
        if (IsForcedNotFoundCapture())
        {
            return null;
        }

        var paths = new List<string?>
        {
            Environment.GetEnvironmentVariable("PATH"),
        };
        if (OperatingSystem.IsWindows())
        {
            paths.Add(TryGetEnvironmentPath(EnvironmentVariableTarget.User));
            paths.Add(TryGetEnvironmentPath(EnvironmentVariableTarget.Machine));
        }

        var distinct = paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(Path.PathSeparator, distinct);
    }

    private static string? TryGetEnvironmentPath(EnvironmentVariableTarget target)
    {
        try
        {
            return Environment.GetEnvironmentVariable("PATH", target);
        }
        catch (Exception exception) when (
            exception is System.Security.SecurityException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static bool IsForcedNotFoundCapture() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_PATH")) &&
        string.Equals(
            Environment.GetEnvironmentVariable("CODEX_USAGE_CAPTURE_FORCE_CODEX_NOT_FOUND"),
            "1",
            StringComparison.Ordinal);
}
