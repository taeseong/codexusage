using System.ComponentModel;
using CodexUsage.Codex.Discovery;
using CodexUsage.Codex.Protocol;
using CodexUsage.Codex.RateLimits;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;

namespace CodexUsage.Codex;

public sealed class LiveCodexUsageProvider : ICodexUsageProvider
{
    private readonly CodexExecutableLocator _locator;
    private readonly IAppServerSessionFactory _sessionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _requestTimeout;
    private string? _lastWorkingExecutablePath;

    public LiveCodexUsageProvider(
        CodexExecutableLocator? locator = null,
        TimeProvider? timeProvider = null,
        TimeSpan? requestTimeout = null)
        : this(
            locator ?? new CodexExecutableLocator(),
            new ProcessAppServerSessionFactory(),
            timeProvider ?? TimeProvider.System,
            requestTimeout ?? TimeSpan.FromSeconds(10))
    {
    }

    internal LiveCodexUsageProvider(
        CodexExecutableLocator locator,
        IAppServerSessionFactory sessionFactory,
        TimeProvider timeProvider,
        TimeSpan requestTimeout)
    {
        _locator = locator;
        _sessionFactory = sessionFactory;
        _timeProvider = timeProvider;
        _requestTimeout = requestTimeout;
    }

    public async Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var executablePaths = PrioritizeLastWorkingExecutable(_locator.FindAll());
        if (executablePaths.Count == 0)
        {
            return Failure(CodexUsageStatus.CodexNotInstalled, "Codex executable was not found on PATH.");
        }

        CodexUsageResult? lastRecoverableFailure = null;
        foreach (var executablePath in executablePaths)
        {
            try
            {
                await using var client = new AppServerClient(executablePath, _sessionFactory, _requestTimeout);
                await client.InitializeAsync(cancellationToken).ConfigureAwait(false);
                var account = await client.ReadAccountAsync(cancellationToken).ConfigureAwait(false);
                if (account.RequiresOpenaiAuth && account.Account is null)
                {
                    return Failure(CodexUsageStatus.NotAuthenticated, "Codex login is required.");
                }

                var rateLimits = await client.ReadRateLimitsAsync(cancellationToken).ConfigureAwait(false);
                var snapshot = RateLimitMapper.Map(account, rateLimits, _timeProvider.GetUtcNow());
                Volatile.Write(ref _lastWorkingExecutablePath, executablePath);
                return new CodexUsageResult { Status = CodexUsageStatus.Success, Snapshot = snapshot };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failure(CodexUsageStatus.Cancelled, "Usage lookup was cancelled.");
            }
            catch (Exception exception) when (IsRecoverableCandidateFailure(exception))
            {
                lastRecoverableFailure = MapCandidateFailure(exception);
            }
        }

        return lastRecoverableFailure ??
            Failure(CodexUsageStatus.CodexNotInstalled, "Codex executable could not be started.");
    }

    private IReadOnlyList<string> PrioritizeLastWorkingExecutable(IReadOnlyList<string> executablePaths)
    {
        var lastWorking = Volatile.Read(ref _lastWorkingExecutablePath);
        if (string.IsNullOrWhiteSpace(lastWorking))
        {
            return executablePaths;
        }

        return executablePaths
            .OrderByDescending(path => string.Equals(path, lastWorking, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool IsRecoverableCandidateFailure(Exception exception) =>
        exception is Win32Exception or IOException or InvalidOperationException or TimeoutException or
        AppServerResponseFormatException or AppServerMethodNotFoundException or
        AppServerProtocolException or AppServerExitedException;

    private static CodexUsageResult MapCandidateFailure(Exception exception) => exception switch
    {
        TimeoutException => Failure(CodexUsageStatus.TimedOut, "Codex App Server did not respond before the timeout."),
        AppServerResponseFormatException => Failure(CodexUsageStatus.ResponseFormatChanged, "Codex App Server returned an unrecognized response shape."),
        AppServerMethodNotFoundException => Failure(CodexUsageStatus.UsageUnsupported, "The installed Codex version does not support usage lookup."),
        AppServerProtocolException or AppServerExitedException => Failure(CodexUsageStatus.ProtocolError, "Codex App Server protocol failed."),
        Win32Exception or IOException or InvalidOperationException => Failure(CodexUsageStatus.CodexNotInstalled, "Codex executable could not be started."),
        _ => Failure(CodexUsageStatus.UnknownError, "An unexpected local error occurred."),
    };

    private static CodexUsageResult Failure(CodexUsageStatus status, string detail) =>
        new() { Status = status, Detail = detail };
}
