using Avalonia;
using CodexUsage.Core.Usage;
using CodexUsage.Windows.Notifications;
using CodexUsage.Windows.Settings;
using CodexUsage.Windows.Startup;
using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Tests;

public sealed class WindowsSettingsAndNotificationTests
{
    private static readonly DateTimeOffset ResetAt =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SettingsStore_RoundTripsWidgetStartupAndAlertState()
    {
        string? json = null;
        var store = new WindowsAppSettingsStore(
            () => @"C:\test\CodexUsage\settings.json",
            _ => json is not null,
            _ => json!,
            (_, value) => json = value,
            _ => { });
        var expected = new WindowsAppSettings
        {
            IsWidgetVisible = false,
            WidgetMode = WidgetInteractionMode.Locked,
            StartAtLogin = true,
            UsageAlertsEnabled = false,
            AlertHistory = new UsageAlertHistory
            {
                Weekly = new UsageLimitAlertHistory
                {
                    ResetsAt = ResetAt,
                    HighestNotifiedPercent = 95,
                },
            },
        };

        store.Save(expected);

        Assert.Equal(expected, store.Load());
    }

    [Fact]
    public void SettingsStore_ReturnsSafeDefaultsForCorruptJson()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "not-json",
            (_, _) => { },
            _ => { });

        Assert.Equal(new WindowsAppSettings(), store.Load());
    }

    [Fact]
    public void SettingsStore_NormalizesMissingNestedSettingsAndInvalidValues()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "{\"WidgetMode\":999,\"AlertHistory\":null}",
            (_, _) => { },
            _ => { });

        var settings = store.Load();

        Assert.Equal(WidgetInteractionMode.Editing, settings.WidgetMode);
        Assert.NotNull(settings.AlertHistory);
        Assert.Null(settings.AlertHistory.ShortTerm);
        Assert.Null(settings.AlertHistory.Weekly);
    }

    [Fact]
    public void SettingsStore_NormalizesUnsupportedNotificationHistoryLevels()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "{\"AlertHistory\":{\"Weekly\":{\"HighestNotifiedPercent\":500}}}",
            (_, _) => { },
            _ => { });

        var settings = store.Load();

        Assert.Equal(UsageThresholdNotifier.CriticalThreshold, settings.AlertHistory.Weekly?.HighestNotifiedPercent);
    }

    [Fact]
    public void StartupService_WritesAQuotedExecutablePathAndCanRemoveIt()
    {
        var registry = new FakeStartupRegistry();
        var service = new WindowsStartupService(
            () => registry,
            () => @"C:\Program Files\CodexUsage\CodexUsage.exe");

        service.SetEnabled(true);

        Assert.Equal("\"C:\\Program Files\\CodexUsage\\CodexUsage.exe\"", registry.Value);
        service.SetEnabled(false);
        Assert.True(registry.Deleted);
    }

    [Fact]
    public void UsageThresholdNotifier_NotifiesOncePerThresholdAndResetWindow()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings();

        var warningAlerts = notifier.Evaluate(Snapshot(82d, ResetAt), settings, out var warningHistory);
        var repeatedAlerts = notifier.Evaluate(Snapshot(82d, ResetAt), settings with { AlertHistory = warningHistory }, out var repeatedHistory);
        var criticalAlerts = notifier.Evaluate(Snapshot(97d, ResetAt), settings with { AlertHistory = repeatedHistory }, out var criticalHistory);
        var nextWindowAlerts = notifier.Evaluate(
            Snapshot(82d, ResetAt.AddDays(7)),
            settings with { AlertHistory = criticalHistory },
            out _);

        Assert.Single(warningAlerts);
        Assert.Equal(UsageThresholdNotifier.WarningThreshold, warningAlerts[0].ThresholdPercent);
        Assert.Empty(repeatedAlerts);
        Assert.Single(criticalAlerts);
        Assert.Equal(UsageThresholdNotifier.CriticalThreshold, criticalAlerts[0].ThresholdPercent);
        Assert.Single(nextWindowAlerts);
    }

    [Fact]
    public void UsageThresholdNotifier_DoesNotNotifyWhileDisabled()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings { UsageAlertsEnabled = false };

        var alerts = notifier.Evaluate(Snapshot(97d, ResetAt), settings, out var history);

        Assert.Empty(alerts);
        Assert.Equal(0, history.Weekly?.HighestNotifiedPercent);
    }

    private static CodexUsageSnapshot Snapshot(double weeklyUsedPercent, DateTimeOffset resetAt) =>
        new()
        {
            RetrievedAt = ResetAt,
            Limits = new[]
            {
                new UsageLimit(
                    "weekly",
                    "Weekly",
                    UsageLimitKind.Weekly,
                    weeklyUsedPercent,
                    TimeSpan.FromDays(7),
                    resetAt),
            },
        };

    private sealed class FakeStartupRegistry : WindowsStartupService.IWindowsStartupRegistry
    {
        public string? Value { get; private set; }

        public bool Deleted { get; private set; }

        public void DeleteValue(string name, bool throwOnMissingValue) => Deleted = true;

        public void SetValue(string name, string value) => Value = value;

        public void Dispose()
        {
        }
    }
}
