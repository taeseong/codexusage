using System.Collections.Concurrent;
using CodexUsage.Codex.Protocol;

namespace CodexUsage.Codex.Tests;

public sealed class AppServerClientTests
{
    [Fact]
    public async Task RejectsMalformedJson()
    {
        var session = new FakeSession(["not-json"]);
        await using var client = CreateClient(session);

        await Assert.ThrowsAsync<AppServerProtocolException>(() => client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TimesOutWhenServerDoesNotRespond()
    {
        var session = new FakeSession([], blockWhenEmpty: true);
        await using var client = CreateClient(session, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<TimeoutException>(() => client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        var session = new FakeSession([], blockWhenEmpty: true);
        await using var client = CreateClient(session, TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.InitializeAsync(cancellation.Token));
    }

    [Fact]
    public async Task ReportsUnexpectedProcessExit()
    {
        var session = new FakeSession([], blockWhenEmpty: false, exitCode: 23);
        await using var client = CreateClient(session);

        var exception = await Assert.ThrowsAsync<AppServerExitedException>(() =>
            client.InitializeAsync(CancellationToken.None));
        Assert.Contains("23", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsUnsupportedInitializeMethod()
    {
        var session = new FakeSession(["{\"id\":1,\"error\":{\"code\":-32601,\"message\":\"not found\"}}"]);
        await using var client = CreateClient(session);

        await Assert.ThrowsAsync<AppServerMethodNotFoundException>(() => client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CorrelatesResponsesAndIgnoresNotifications()
    {
        var session = new FakeSession(
        [
            "{\"method\":\"account/updated\",\"params\":{}}",
            "{\"id\":1,\"result\":{\"userAgent\":\"codex\",\"platformOs\":\"macos\",\"platformFamily\":\"unix\",\"codexHome\":\"/redacted\"}}",
            "{\"id\":2,\"result\":{\"account\":{\"type\":\"chatgpt\",\"email\":\"ignored@example.com\",\"planType\":\"pro\"},\"requiresOpenaiAuth\":true}}",
        ]);
        await using var client = CreateClient(session);

        await client.InitializeAsync(CancellationToken.None);
        var account = await client.ReadAccountAsync(CancellationToken.None);

        Assert.Equal("pro", account.Account?.PlanType);
        Assert.Contains(session.Writes, line => line.Contains("account/read", StringComparison.Ordinal));
    }

    private static AppServerClient CreateClient(FakeSession session, TimeSpan? timeout = null) =>
        new("codex", new FakeSessionFactory(session), timeout ?? TimeSpan.FromSeconds(1));

    private sealed class FakeSessionFactory(FakeSession session) : IAppServerSessionFactory
    {
        public IAppServerSession Start(string codexExecutablePath) => session;
    }

    private sealed class FakeSession(
        IEnumerable<string> lines,
        bool blockWhenEmpty = false,
        int? exitCode = null) : IAppServerSession
    {
        private readonly ConcurrentQueue<string> _lines = new(lines);

        public List<string> Writes { get; } = [];

        public int? ExitCode { get; } = exitCode;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add(message);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            if (_lines.TryDequeue(out var line))
            {
                return line;
            }

            if (!blockWhenEmpty)
            {
                return null;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }
}
