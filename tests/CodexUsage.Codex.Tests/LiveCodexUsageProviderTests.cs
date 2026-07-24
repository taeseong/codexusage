using System.Collections.Concurrent;
using System.ComponentModel;
using CodexUsage.Codex.Discovery;
using CodexUsage.Codex.Protocol;
using CodexUsage.Core.Usage;

namespace CodexUsage.Codex.Tests;

public sealed class LiveCodexUsageProviderTests
{
    [Fact]
    public async Task GetUsageAsync_FallsBackWhenPreferredExecutableCannotStart()
    {
        // Given
        var firstDirectory = Path.Combine(Path.GetTempPath(), "codex-unavailable");
        var secondDirectory = Path.Combine(Path.GetTempPath(), "codex-npm");
        var unavailableExecutable = Path.Combine(firstDirectory, "codex.exe");
        var workingCommand = Path.Combine(secondDirectory, "codex.cmd");
        var locator = new CodexExecutableLocator(
            () => string.Join(Path.PathSeparator, firstDirectory, secondDirectory),
            [],
            path => string.Equals(path, unavailableExecutable, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, workingCommand, StringComparison.OrdinalIgnoreCase));
        var sessionFactory = new FallbackSessionFactory(unavailableExecutable, workingCommand);
        var provider = new LiveCodexUsageProvider(locator, sessionFactory, TimeProvider.System, TimeSpan.FromSeconds(1));

        // When
        var result = await provider.GetUsageAsync();

        // Then
        Assert.Equal(CodexUsageStatus.NotAuthenticated, result.Status);
        Assert.Equal([unavailableExecutable, workingCommand], sessionFactory.StartedPaths);
    }

    private sealed class FallbackSessionFactory(string unavailableExecutable, string workingCommand) : IAppServerSessionFactory
    {
        public List<string> StartedPaths { get; } = [];

        public IAppServerSession Start(string codexExecutablePath)
        {
            StartedPaths.Add(codexExecutablePath);
            if (string.Equals(codexExecutablePath, unavailableExecutable, StringComparison.OrdinalIgnoreCase))
            {
                throw new Win32Exception();
            }

            Assert.Equal(workingCommand, codexExecutablePath);
            return new FakeSession(
            [
                "{\"id\":1,\"result\":{}}",
                "{\"id\":2,\"result\":{\"requiresOpenaiAuth\":true}}",
            ]);
        }
    }

    private sealed class FakeSession(IEnumerable<string> lines) : IAppServerSession
    {
        private readonly ConcurrentQueue<string> _lines = new(lines);

        public int? ExitCode => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_lines.TryDequeue(out var line) ? line : null);
        }
    }
}
