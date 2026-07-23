namespace CodexUsage.Codex.Discovery;

public sealed class CodexExecutableLocator
{
    private readonly Func<string?> _pathProvider;
    private readonly IReadOnlyList<string> _knownPaths;
    private readonly Func<string, bool> _fileExists;

    public CodexExecutableLocator()
        : this(
            () => Environment.GetEnvironmentVariable("PATH"),
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

    public string? Find()
    {
        var executableName = OperatingSystem.IsWindows() ? "codex.exe" : "codex";
        var path = _pathProvider();
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var resolved = Resolve(Path.Combine(directory, executableName));
                if (resolved is not null)
                {
                    return resolved;
                }
            }
        }

        foreach (var knownPath in _knownPaths)
        {
            var resolved = Resolve(knownPath);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

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
        if (!OperatingSystem.IsMacOS())
        {
            return [];
        }

        var paths = new List<string>
        {
            "/Applications/Codex.app/Contents/Resources/codex",
        };
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            paths.Add(Path.Combine(userProfile, "Applications/Codex.app/Contents/Resources/codex"));
            paths.Add(Path.Combine(userProfile, ".local/bin/codex"));
        }

        return paths;
    }
}
