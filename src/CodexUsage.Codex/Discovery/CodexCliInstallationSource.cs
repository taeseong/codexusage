namespace CodexUsage.Codex.Discovery;

internal static class CodexCliInstallationSource
{
    public static string Describe(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return "Not found";
        }

        var normalized = executablePath.Replace('/', '\\');
        if (normalized.Contains("\\Programs\\OpenAI\\Codex\\bin\\codex.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "PowerShell standalone";
        }

        if (normalized.Contains("\\npm\\codex.", StringComparison.OrdinalIgnoreCase))
        {
            return "npm";
        }

        if (normalized.Contains("\\Microsoft\\WinGet\\Links\\", StringComparison.OrdinalIgnoreCase))
        {
            return "WinGet";
        }

        if (normalized.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase))
        {
            return "WindowsApps";
        }

        return "PATH or custom location";
    }
}
