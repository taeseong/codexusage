using System.Text.Json;
using CodexUsage.Core.Usage;
using CodexUsage.Windows.Recovery;
using CodexUsage.Windows.Settings;
using Microsoft.Win32;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsUsageSnapshotCacheTests
{
    private static readonly DateTimeOffset RetrievedAt =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cache_RoundTripsOnlyDisplaySafeUsageFields()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            var store = new JsonUsageSnapshotCache(path);
            await store.SaveAsync(Snapshot());

            var restored = await store.LoadAsync();
            var json = await File.ReadAllTextAsync(path);

            Assert.NotNull(restored);
            Assert.Equal(RetrievedAt, restored.RetrievedAt);
            Assert.Equal("pro", restored.AccountPlan);
            Assert.Equal(42d, restored.Limits.Single().UsedPercent);
            Assert.Equal("weekly", restored.Limits.Single().Id);
            Assert.DoesNotContain("user@example.com", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-token", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("server-supplied-id", json, StringComparison.Ordinal);
            Assert.DoesNotContain("server-supplied-name", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cache_RecoversACompleteInterruptedTemporaryFile()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            var store = new JsonUsageSnapshotCache(path);
            await store.SaveAsync(Snapshot());
            File.Move(path, path + ".tmp");

            var restored = await store.LoadAsync();

            Assert.NotNull(restored);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cache_PreservesOneLimitPerKindRegardlessOfServerOrder()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            var store = new JsonUsageSnapshotCache(path);
            var snapshot = Snapshot() with
            {
                Limits =
                [
                    new UsageLimit(
                        "weekly-first",
                        "Weekly first",
                        UsageLimitKind.Weekly,
                        42d,
                        TimeSpan.FromDays(7),
                        RetrievedAt.AddDays(6)),
                    new UsageLimit(
                        "weekly-duplicate",
                        "Weekly duplicate",
                        UsageLimitKind.Weekly,
                        43d,
                        TimeSpan.FromDays(7),
                        RetrievedAt.AddDays(6)),
                    new UsageLimit(
                        "short",
                        "5-hour",
                        UsageLimitKind.ShortTerm,
                        21d,
                        TimeSpan.FromHours(5),
                        RetrievedAt.AddHours(4)),
                ],
            };

            await store.SaveAsync(snapshot);
            var restored = await store.LoadAsync();

            Assert.NotNull(restored);
            Assert.Equal(2, restored.Limits.Count);
            Assert.Equal(21d, restored.Limits.Single(limit =>
                limit.Kind is UsageLimitKind.ShortTerm).UsedPercent);
            Assert.Equal(42d, restored.Limits.Single(limit =>
                limit.Kind is UsageLimitKind.Weekly).UsedPercent);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cache_PreservesMalformedFileAndReturnsNoSnapshot()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            await File.WriteAllTextAsync(path, "{not-json");
            var store = new JsonUsageSnapshotCache(path);

            var restored = await store.LoadAsync();

            Assert.Null(restored);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.GetFiles(directory, "usage-cache.json.corrupt-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cache_RejectsUnsupportedOrStructurallyInvalidDocuments()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(new
                {
                    Version = 99,
                    Snapshot = new { RetrievedAt },
                }));
            var store = new JsonUsageSnapshotCache(path);

            var restored = await store.LoadAsync();

            Assert.Null(restored);
            Assert.Single(Directory.GetFiles(directory, "usage-cache.json.corrupt-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Cache_RejectsDuplicateLimitKinds()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "usage-cache.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(new
                {
                    Version = 1,
                    Snapshot = new
                    {
                        RetrievedAt,
                        Limits = new[]
                        {
                            new
                            {
                                Kind = UsageLimitKind.Weekly,
                                UsedPercent = 42d,
                                WindowDurationSeconds = 604800d,
                                ResetsAt = RetrievedAt.AddDays(6),
                            },
                            new
                            {
                                Kind = UsageLimitKind.Weekly,
                                UsedPercent = 43d,
                                WindowDurationSeconds = 604800d,
                                ResetsAt = RetrievedAt.AddDays(6),
                            },
                        },
                    },
                }));
            var store = new JsonUsageSnapshotCache(path);

            var restored = await store.LoadAsync();

            Assert.Null(restored);
            Assert.Single(Directory.GetFiles(directory, "usage-cache.json.corrupt-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RecoveryService_RefreshesOnlyForResumeAndAvailableNetwork()
    {
        var refreshes = 0;
        using var service = new WindowsRefreshRecoveryService(() => refreshes++);

        service.NotifyNetworkAvailabilityChanged(isAvailable: false);
        service.NotifyPowerModeChanged(PowerModes.Suspend);
        Assert.Equal(0, refreshes);

        service.NotifyNetworkAvailabilityChanged(isAvailable: true);
        service.NotifyPowerModeChanged(PowerModes.Resume);
        Assert.Equal(2, refreshes);
    }

    [Fact]
    public void RecoveryService_RollsBackNetworkSubscriptionWhenPowerSubscriptionFails()
    {
        var calls = new List<string>();
        using var service = new WindowsRefreshRecoveryService(
            () => { },
            subscribeNetwork: () => calls.Add("network+"),
            unsubscribeNetwork: () => calls.Add("network-"),
            subscribePower: () =>
            {
                calls.Add("power+");
                throw new InvalidOperationException("power events unavailable");
            },
            unsubscribePower: () => calls.Add("power-"));

        Assert.Throws<InvalidOperationException>(() => service.Start());
        Assert.Equal(["network+", "power+", "network-"], calls);
    }

    [Fact]
    public void RecoveryService_DisposeAttemptsBothUnsubscriptionsWithoutThrowing()
    {
        var networkUnsubscribeAttempted = false;
        var powerUnsubscribeAttempted = false;
        var service = new WindowsRefreshRecoveryService(
            () => { },
            subscribeNetwork: () => { },
            unsubscribeNetwork: () =>
            {
                networkUnsubscribeAttempted = true;
                throw new InvalidOperationException("network event source unavailable");
            },
            subscribePower: () => { },
            unsubscribePower: () =>
            {
                powerUnsubscribeAttempted = true;
                throw new InvalidOperationException("power event source unavailable");
            });
        service.Start();

        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
        Assert.True(networkUnsubscribeAttempted);
        Assert.True(powerUnsubscribeAttempted);
    }

    private static CodexUsageSnapshot Snapshot() =>
        new()
        {
            RetrievedAt = RetrievedAt,
            AccountPlan = "pro",
            RateLimitPlan = "server supplied plan with spaces user@example.com secret-token",
            Limits =
            [
                new UsageLimit(
                    "server-supplied-id",
                    "server-supplied-name",
                    UsageLimitKind.Weekly,
                    42d,
                    TimeSpan.FromDays(7),
                    RetrievedAt.AddDays(6)),
            ],
        };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "CodexUsageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
