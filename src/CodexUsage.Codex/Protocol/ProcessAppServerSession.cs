using System.Diagnostics;

namespace CodexUsage.Codex.Protocol;

internal sealed class ProcessAppServerSession : IAppServerSession
{
    private readonly Process _process;
    private readonly Task _stderrDrain;
    private bool _disposed;

    public ProcessAppServerSession(string codexExecutablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (OperatingSystem.IsWindows() && IsCommandScript(codexExecutablePath))
        {
            // The npm-installed CLI on Windows is a .cmd shim. Start it through cmd.exe
            // directly, so PowerShell execution policy never affects the app server.
            startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(codexExecutablePath);
        }
        else
        {
            startInfo.FileName = codexExecutablePath;
        }

        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--stdio");

        _process = new Process { StartInfo = startInfo };
        if (!_process.Start())
        {
            _process.Dispose();
            throw new InvalidOperationException("Codex App Server process did not start.");
        }

        _stderrDrain = DrainStandardErrorAsync(_process.StandardError);
    }

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public async ValueTask WriteLineAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _process.StandardInput.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
        await _process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _process.StandardInput.Close();
            if (!_process.HasExited)
            {
                using var gracefulTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                try
                {
                    await _process.WaitForExitAsync(gracefulTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().ConfigureAwait(false);
                }
            }

            await _stderrDrain.ConfigureAwait(false);
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static async Task DrainStandardErrorAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is not null)
        {
        }
    }

    private static bool IsCommandScript(string path) =>
        path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
}

internal sealed class ProcessAppServerSessionFactory : IAppServerSessionFactory
{
    public IAppServerSession Start(string codexExecutablePath) => new ProcessAppServerSession(codexExecutablePath);
}
