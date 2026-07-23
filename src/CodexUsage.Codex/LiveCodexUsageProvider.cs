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
        var executablePath = _locator.Find();
        if (executablePath is null)
        {
            return Failure(CodexUsageStatus.CodexNotInstalled, "Codex executable was not found on PATH.");
        }

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
            return new CodexUsageResult { Status = CodexUsageStatus.Success, Snapshot = snapshot };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(CodexUsageStatus.Cancelled, "Usage lookup was cancelled.");
        }
        catch (TimeoutException)
        {
            return Failure(CodexUsageStatus.TimedOut, "Codex App Server did not respond before the timeout.");
        }
        catch (AppServerResponseFormatException)
        {
            return Failure(CodexUsageStatus.ResponseFormatChanged, "Codex App Server returned an unrecognized response shape.");
        }
        catch (AppServerMethodNotFoundException)
        {
            return Failure(CodexUsageStatus.UsageUnsupported, "The installed Codex version does not support usage lookup.");
        }
        catch (AppServerProtocolException)
        {
            return Failure(CodexUsageStatus.ProtocolError, "Codex App Server protocol failed.");
        }
        catch (AppServerExitedException)
        {
            return Failure(CodexUsageStatus.ProtocolError, "Codex App Server exited unexpectedly.");
        }
        catch (Win32Exception)
        {
            return Failure(CodexUsageStatus.CodexNotInstalled, "Codex executable could not be started.");
        }
        catch (Exception)
        {
            return Failure(CodexUsageStatus.UnknownError, "An unexpected local error occurred.");
        }
    }

    private static CodexUsageResult Failure(CodexUsageStatus status, string detail) =>
        new() { Status = status, Detail = detail };
}
