using System.Text.Json;

namespace CodexUsage.Codex.Protocol;

internal sealed class AppServerClient(
    string codexExecutablePath,
    IAppServerSessionFactory sessionFactory,
    TimeSpan requestTimeout) : IAsyncDisposable
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private IAppServerSession? _session;
    private long _nextRequestId;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _session = sessionFactory.Start(codexExecutablePath);
        await SendRequestAsync<JsonElement>(
            "initialize",
            new
            {
                clientInfo = new { name = "codex-usage", version = "0.1.0" },
                capabilities = new { experimentalApi = true },
            },
            cancellationToken).ConfigureAwait(false);
        await WriteAsync(new { method = "initialized" }, cancellationToken).ConfigureAwait(false);
    }

    public Task<AccountReadResponse> ReadAccountAsync(CancellationToken cancellationToken) =>
        SendRequestAsync<AccountReadResponse>(
            "account/read",
            new { refreshToken = false },
            cancellationToken);

    public Task<RateLimitsReadResponse> ReadRateLimitsAsync(CancellationToken cancellationToken) =>
        SendRequestAsync<RateLimitsReadResponse>("account/rateLimits/read", null, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<T> SendRequestAsync<T>(string method, object? parameters, CancellationToken cancellationToken)
    {
        var session = _session ?? throw new InvalidOperationException("App Server is not initialized.");
        var requestId = Interlocked.Increment(ref _nextRequestId);
        await WriteAsync(new { id = requestId, method, @params = parameters }, cancellationToken).ConfigureAwait(false);

        using var timeout = new CancellationTokenSource(requestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            while (true)
            {
                var line = await session.ReadLineAsync(linked.Token).ConfigureAwait(false);
                if (line is null)
                {
                    throw new AppServerExitedException(session.ExitCode);
                }

                using var message = ParseMessage(line);
                var root = message.RootElement;
                if (!root.TryGetProperty("id", out var idElement) || !IdMatches(idElement, requestId))
                {
                    continue;
                }

                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorCode = GetSafeErrorCode(errorElement);
                    if (string.Equals(errorCode, "-32601", StringComparison.Ordinal))
                    {
                        throw new AppServerMethodNotFoundException(method);
                    }

                    throw new AppServerProtocolException($"Codex App Server returned an error for {method}: {errorCode}");
                }

                if (!root.TryGetProperty("result", out var resultElement))
                {
                    throw new AppServerResponseFormatException($"Codex App Server response for {method} omitted result.");
                }

                try
                {
                    return resultElement.Deserialize<T>(_jsonOptions)
                        ?? throw new AppServerResponseFormatException($"Codex App Server returned an empty result for {method}.");
                }
                catch (JsonException exception)
                {
                    throw new AppServerResponseFormatException($"Codex App Server response shape changed for {method}.", exception);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Codex App Server request {method} timed out.");
        }
    }

    private async ValueTask WriteAsync(object message, CancellationToken cancellationToken)
    {
        var session = _session ?? throw new InvalidOperationException("App Server is not initialized.");
        await session.WriteLineAsync(JsonSerializer.Serialize(message, _jsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static JsonDocument ParseMessage(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            throw new AppServerProtocolException("Codex App Server emitted malformed JSON.", exception);
        }
    }

    private static bool IdMatches(JsonElement idElement, long expected) =>
        idElement.ValueKind is JsonValueKind.Number && idElement.TryGetInt64(out var actual) && actual == expected;

    private static string GetSafeErrorCode(JsonElement errorElement)
    {
        if (errorElement.ValueKind is JsonValueKind.Object &&
            errorElement.TryGetProperty("code", out var codeElement))
        {
            return codeElement.ToString();
        }

        return "unknown";
    }
}
