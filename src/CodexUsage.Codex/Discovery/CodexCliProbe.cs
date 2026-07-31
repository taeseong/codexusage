using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodexUsage.Codex.Discovery;

public sealed class CodexCliProbe
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private readonly CodexExecutableLocator _locator;
    private readonly Func<string, CancellationToken, Task<string?>> _versionReader;

    public CodexCliProbe()
        : this(new CodexExecutableLocator(), ReadVersionAsync)
    {
    }

    internal CodexCliProbe(
        CodexExecutableLocator locator,
        Func<string, CancellationToken, Task<string?>> versionReader)
    {
        _locator = locator;
        _versionReader = versionReader;
    }

    public async Task<CodexCliProbeResult> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        var paths = _locator.FindAll();
        if (paths.Count == 0)
        {
            return new CodexCliProbeResult(null, null);
        }

        foreach (var path in paths)
        {
            try
            {
                var version = await _versionReader(path, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return new CodexCliProbeResult(path, NormalizeVersion(version));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is Win32Exception or IOException or InvalidOperationException or TimeoutException)
            {
            }
        }

        return new CodexCliProbeResult(paths[0], null);
    }

    private static async Task<string?> ReadVersionAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(executablePath),
        };
        if (!process.Start())
        {
            return null;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Codex CLI version probe timed out.");
        }

        var output = await outputTask.ConfigureAwait(false);
        _ = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    private static ProcessStartInfo CreateStartInfo(string executablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows() && IsCommandScript(executablePath))
        {
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(executablePath);
        }
        else
        {
            startInfo.FileName = executablePath;
        }

        startInfo.ArgumentList.Add("--version");
        return startInfo;
    }

    private static string NormalizeVersion(string value)
    {
        var firstLine = value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim() ?? string.Empty;
        var match = Regex.Match(
            firstLine,
            @"\bcodex(?:-cli)?\s+([0-9]+(?:\.[0-9A-Za-z-]+)+)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return match.Success
            ? $"codex-cli {match.Groups[1].Value}"
            : "Version unavailable";
    }

    private static bool IsCommandScript(string path) =>
        path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
}

public sealed record CodexCliProbeResult(
    string? ExecutablePath,
    string? Version)
{
    public bool IsFound => !string.IsNullOrWhiteSpace(ExecutablePath);

    public string InstallationSource => CodexCliInstallationSource.Describe(ExecutablePath);
}
