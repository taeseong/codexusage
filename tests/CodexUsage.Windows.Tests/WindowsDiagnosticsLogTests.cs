using CodexUsage.Core.Usage;
using CodexUsage.Windows.Diagnostics;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsDiagnosticsLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void Record_KeepsOnlyTheLatestBoundedSafeEvents()
    {
        var timeProvider = new FakeTimeProvider();
        var log = new WindowsDiagnosticsLog(Path.Combine(_directory, "diagnostics.log"), timeProvider);

        for (var index = 0; index < 45; index++)
        {
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            log.Record(WindowsDiagnosticEventKind.UsageLookupFailed, CodexUsageStatus.ProtocolError);
        }

        var entries = log.ReadRecent();

        Assert.Equal(40, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.Equal(WindowsDiagnosticEventKind.UsageLookupFailed, entry.Kind);
            Assert.Equal(CodexUsageStatus.ProtocolError, entry.UsageStatus);
        });
        var content = File.ReadAllText(Path.Combine(_directory, "diagnostics.log"));
        Assert.DoesNotContain("token", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadRecent_IgnoresMalformedOrUnsafeLines()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "diagnostics.log");
        File.WriteAllLines(path,
        [
            "not a valid event",
            "2026-07-30T00:00:00.0000000+00:00|UsageLookupFailed|ProtocolError",
            "2026-07-30T00:00:00.0000000+00:00|UsageLookupFailed|token-value",
        ]);
        var log = new WindowsDiagnosticsLog(path, TimeProvider.System);

        var entries = log.ReadRecent();

        var entry = Assert.Single(entries);
        Assert.Equal(CodexUsageStatus.ProtocolError, entry.UsageStatus);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }
}
