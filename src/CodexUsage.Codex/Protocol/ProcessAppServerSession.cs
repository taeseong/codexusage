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
            FileName = codexExecutablePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
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
}

internal sealed class ProcessAppServerSessionFactory : IAppServerSessionFactory
{
    public IAppServerSession Start(string codexExecutablePath) => new ProcessAppServerSession(codexExecutablePath);
}
