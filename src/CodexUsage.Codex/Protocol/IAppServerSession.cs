namespace CodexUsage.Codex.Protocol;

internal interface IAppServerSession : IAsyncDisposable
{
    int? ExitCode { get; }

    ValueTask WriteLineAsync(string message, CancellationToken cancellationToken);

    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);
}

internal interface IAppServerSessionFactory
{
    IAppServerSession Start(string codexExecutablePath);
}

