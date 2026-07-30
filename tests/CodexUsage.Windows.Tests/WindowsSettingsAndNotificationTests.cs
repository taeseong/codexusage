using Avalonia;
using CodexUsage.Core.Usage;
using CodexUsage.Windows.Notifications;
using CodexUsage.Windows.Settings;
using CodexUsage.Windows.Startup;
using CodexUsage.Windows.ViewModels;
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
            WarningThresholdPercent = 70,
            CriticalThresholdPercent = 90,
            DetailsWindow = new DetailsWindowSettings
            {
                X = 100,
                Y = 200,
                Width = 640,
                Height = 720,
                SelectedTabIndex = 1,
            },
            AlertHistory = new UsageAlertHistory
            {
                Weekly = new UsageLimitAlertHistory
                {
                    ResetsAt = ResetAt,
                    WindowDuration = TimeSpan.FromDays(7),
                    LastObservedAt = ResetAt.AddHours(-1),
                    LastObservedPercent = 90,
                    HighestNotifiedPercent = 90,
                },
            },
        };

        Assert.True(store.Save(expected));

        Assert.Equal(expected, store.Load());
    }

    [Fact]
    public void SettingsStore_WritesAtomicallyAndRecoversACompleteTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new WindowsAppSettingsStore(path);
            Assert.True(store.Save(new WindowsAppSettings { WarningThresholdPercent = 72 }));
            Assert.False(File.Exists(path + ".tmp"));
            File.Move(path, path + ".tmp");

            var restored = store.Load();

            Assert.Equal(72, restored.WarningThresholdPercent);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SettingsStore_RoundTripsWidgetAppearanceThemeAndAlertPause()
    {
        string? json = null;
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => json is not null,
            _ => json!,
            (_, value) => json = value,
            _ => { });
        var pausedUntil = new DateTimeOffset(2026, 8, 1, 15, 0, 0, TimeSpan.Zero);

        Assert.True(store.Save(new WindowsAppSettings
        {
            WidgetScalePercent = 125,
            WidgetOpacityPercent = 80,
            ShowWidgetShortTermUsage = false,
            ShowWidgetWeeklyUsage = true,
            ShowWidgetWeeklyProgress = false,
            ThemePreference = WindowsThemePreference.Dark,
            AlertsPausedUntil = pausedUntil,
        }));

        var restored = store.Load();
        Assert.Equal(125, restored.WidgetScalePercent);
        Assert.Equal(80, restored.WidgetOpacityPercent);
        Assert.False(restored.ShowWidgetShortTermUsage);
        Assert.True(restored.ShowWidgetWeeklyUsage);
        Assert.False(restored.ShowWidgetWeeklyProgress);
        Assert.Equal(WindowsThemePreference.Dark, restored.ThemePreference);
        Assert.Equal(pausedUntil, restored.AlertsPausedUntil);
    }

    [Fact]
    public async Task SettingsStore_PreservesCorruptFileAndReturnsSafeDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(path, "not-json");
            var store = new WindowsAppSettingsStore(path);

            var restored = store.Load();

            Assert.Equal(new WindowsAppSettings(), restored);
            Assert.Equal(
                WindowsSettingsRecoveryStatus.CorruptFilePreserved,
                store.LastRecoveryStatus);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.GetFiles(directory, "settings.json.corrupt-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SettingsStore_ReturnsFalseWhenWriteFails()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => false,
            _ => string.Empty,
            (_, _) => throw new IOException("simulated"),
            _ => { });

        Assert.False(store.Save(new WindowsAppSettings()));
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
        Assert.Equal(
            WindowsSettingsRecoveryStatus.CorruptFilePreserved,
            store.LastRecoveryStatus);
    }

    [Fact]
    public void SettingsStore_ReportsReadFailureWithoutTreatingAMissingFileAsDamage()
    {
        var failingStore = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => throw new IOException("simulated"),
            (_, _) => { },
            _ => { });
        var missingStore = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => false,
            _ => string.Empty,
            (_, _) => { },
            _ => { });

        Assert.Equal(new WindowsAppSettings(), failingStore.Load());
        Assert.Equal(
            WindowsSettingsRecoveryStatus.ReadFailed,
            failingStore.LastRecoveryStatus);
        Assert.Equal(new WindowsAppSettings(), missingStore.Load());
        Assert.Equal(
            WindowsSettingsRecoveryStatus.None,
            missingStore.LastRecoveryStatus);
    }

    [Fact]
    public void SettingsStore_PausesWritesWhenACorruptFileCannotBePreserved()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "not-json",
            (_, _) => { },
            _ => { },
            _ => { },
            _ => throw new UnauthorizedAccessException("simulated"));

        Assert.Equal(new WindowsAppSettings(), store.Load());
        Assert.Equal(
            WindowsSettingsRecoveryStatus.CorruptFilePreservationFailed,
            store.LastRecoveryStatus);
        var gate = new WindowsSettingsWriteGate(store.LastRecoveryStatus);
        Assert.True(gate.IsAutomaticWritePaused);
        Assert.False(gate.CanWrite(allowRecoveryOverwrite: false));
    }

    [Fact]
    public void SettingsWriteGate_RequiresExplicitSaveAfterAnUnreadableFile()
    {
        var unreadable = new WindowsSettingsWriteGate(
            WindowsSettingsRecoveryStatus.ReadFailed);
        var corrupt = new WindowsSettingsWriteGate(
            WindowsSettingsRecoveryStatus.CorruptFilePreserved);
        var unpreservedCorrupt = new WindowsSettingsWriteGate(
            WindowsSettingsRecoveryStatus.CorruptFilePreservationFailed);

        Assert.False(unreadable.CanWrite(allowRecoveryOverwrite: false));
        Assert.True(unreadable.CanWrite(allowRecoveryOverwrite: true));
        Assert.False(unreadable.CanApplyAutomaticChange);
        Assert.True(corrupt.CanWrite(allowRecoveryOverwrite: false));
        Assert.True(corrupt.CanApplyAutomaticChange);
        Assert.False(unpreservedCorrupt.CanWrite(allowRecoveryOverwrite: false));
        Assert.False(unpreservedCorrupt.CanApplyAutomaticChange);

        unreadable.OnWriteSucceeded(allowRecoveryOverwrite: true);

        Assert.False(unreadable.IsAutomaticWritePaused);
        Assert.True(unreadable.CanWrite(allowRecoveryOverwrite: false));
        Assert.True(unreadable.CanApplyAutomaticChange);
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
    public void SettingsStore_NormalizesThresholdsAndDetailWindowState()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "{\"WarningThresholdPercent\":150,\"CriticalThresholdPercent\":2,\"DetailsWindow\":{\"Width\":1,\"Height\":9999,\"SelectedTabIndex\":9}}",
            (_, _) => { },
            _ => { });

        var settings = store.Load();

        Assert.Equal(99, settings.WarningThresholdPercent);
        Assert.Equal(100, settings.CriticalThresholdPercent);
        Assert.Equal(380, settings.DetailsWindow.Width);
        Assert.Equal(1200, settings.DetailsWindow.Height);
        Assert.Equal(0, settings.DetailsWindow.SelectedTabIndex);
    }

    [Fact]
    public void SettingsStore_NormalizesWidgetAppearanceAndTheme()
    {
        var store = new WindowsAppSettingsStore(
            () => "settings.json",
            _ => true,
            _ => "{\"WidgetScalePercent\":300,\"WidgetOpacityPercent\":2,\"ShowWidgetShortTermUsage\":false,\"ShowWidgetWeeklyUsage\":false,\"ThemePreference\":999}",
            (_, _) => { },
            _ => { });

        var settings = store.Load();

        Assert.Equal(150, settings.WidgetScalePercent);
        Assert.False(settings.ShowWidgetShortTermUsage);
        Assert.True(settings.ShowWidgetWeeklyUsage);
        Assert.Equal(65, settings.WidgetOpacityPercent);
        Assert.Equal(WindowsThemePreference.System, settings.ThemePreference);
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
        var enabledStatus = service.GetStatus();
        Assert.True(enabledStatus.IsRegistered);
        Assert.True(enabledStatus.MatchesCurrentExecutable);
        service.SetEnabled(false);
        Assert.True(registry.Deleted);
        Assert.False(service.GetStatus().IsRegistered);
    }

    [Fact]
    public void StartupService_DetectsAStaleExecutablePath()
    {
        var registry = new FakeStartupRegistry
        {
            Value = "\"C:\\Old\\CodexUsage.exe\"",
        };
        var service = new WindowsStartupService(
            () => registry,
            () => @"C:\Program Files\CodexUsage\CodexUsage.exe");

        var status = service.GetStatus();

        Assert.True(status.IsRegistered);
        Assert.False(status.MatchesCurrentExecutable);
        Assert.Equal("\"C:\\Old\\CodexUsage.exe\"", status.RegisteredCommand);
    }

    [Fact]
    public void SettingsViewModel_ExposesStartupRepairAndTestNotificationCommands()
    {
        var viewModel = new WindowsSettingsViewModel(
            new WindowsAppSettings { StartAtLogin = true },
            new StartupRegistrationStatus(
                IsRegistered: false,
                MatchesCurrentExecutable: false,
                RegisteredCommand: null,
                ExpectedExecutablePath: @"C:\CodexUsage.exe"));
        var repairRequested = false;
        var notificationRequested = false;
        viewModel.RepairStartupRequested += enabled => repairRequested = enabled;
        viewModel.TestNotificationRequested += () => notificationRequested = true;

        viewModel.RepairStartupCommand.Execute(null);
        viewModel.TestNotificationCommand.Execute(null);

        Assert.True(repairRequested);
        Assert.True(notificationRequested);
        Assert.True(viewModel.CanRepairStartup);
        Assert.Equal("Startup entry is missing", viewModel.StartupStatusText);
    }

    [Fact]
    public void SettingsViewModel_DoesNotRepairFromAnUnsavedDisabledStartupSetting()
    {
        var viewModel = new WindowsSettingsViewModel(
            new WindowsAppSettings { StartAtLogin = true },
            new StartupRegistrationStatus(
                IsRegistered: true,
                MatchesCurrentExecutable: true,
                RegisteredCommand: "\"C:\\CodexUsage.exe\"",
                ExpectedExecutablePath: @"C:\CodexUsage.exe"));
        bool? requestedState = null;
        viewModel.RepairStartupRequested += enabled => requestedState = enabled;

        viewModel.StartAtLogin = false;
        viewModel.RepairStartupCommand.Execute(null);

        Assert.False(viewModel.CanRepairStartup);
        Assert.Equal("Save changes to update startup registration", viewModel.StartupStatusText);
        Assert.Null(requestedState);
    }

    [Fact]
    public void SettingsViewModel_DoesNotRepairFromAnUnsavedEnabledStartupSetting()
    {
        var viewModel = new WindowsSettingsViewModel(
            new WindowsAppSettings { StartAtLogin = false },
            new StartupRegistrationStatus(
                IsRegistered: false,
                MatchesCurrentExecutable: false,
                RegisteredCommand: null,
                ExpectedExecutablePath: @"C:\CodexUsage.exe"));
        bool? requestedState = null;
        viewModel.RepairStartupRequested += enabled => requestedState = enabled;

        viewModel.StartAtLogin = true;
        viewModel.RepairStartupCommand.Execute(null);

        Assert.False(viewModel.CanRepairStartup);
        Assert.Equal("Save changes to update startup registration", viewModel.StartupStatusText);
        Assert.Null(requestedState);
    }

    [Fact]
    public void SettingsViewModel_RestoresSafeDefaultsOnlyWhenTheUserSaves()
    {
        var settings = new WindowsAppSettings
        {
            StartAtLogin = true,
            UsageAlertsEnabled = false,
            ShortTermAlertsEnabled = false,
            WeeklyAlertsEnabled = false,
            WarningThresholdPercent = 35,
            CriticalThresholdPercent = 45,
            QuietHoursEnabled = true,
            QuietHoursStart = 1,
            QuietHoursEnd = 2,
            ResetReminderEnabled = true,
            ResetReminderMinutes = 120,
        };
        var viewModel = new WindowsSettingsViewModel(
            settings,
            recoveryNotice: "Damaged settings were preserved.");
        WindowsSettingsPreferences? saved = null;
        viewModel.SaveRequested += preferences => saved = preferences;

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Null(saved);
        Assert.False(viewModel.StartAtLogin);
        Assert.True(viewModel.UsageAlertsEnabled);
        Assert.True(viewModel.ShortTermAlertsEnabled);
        Assert.True(viewModel.WeeklyAlertsEnabled);
        Assert.Equal(80, viewModel.WarningThresholdPercent);
        Assert.Equal(95, viewModel.CriticalThresholdPercent);
        Assert.False(viewModel.QuietHoursEnabled);
        Assert.Equal(22, viewModel.QuietHoursStart);
        Assert.Equal(8, viewModel.QuietHoursEnd);
        Assert.False(viewModel.ResetReminderEnabled);
        Assert.Equal(30, viewModel.ResetReminderMinutes);
        Assert.Equal("Defaults loaded. Select Save to apply.", viewModel.FeedbackMessage);
        Assert.True(viewModel.HasRecoveryNotice);

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.True(saved.ResetAlertHistory);
    }

    [Fact]
    public void SettingsViewModel_SavesWidgetAppearanceAndThemeWithoutChangingRestoreDefaultsScope()
    {
        var viewModel = new WindowsSettingsViewModel(new WindowsAppSettings
        {
            WidgetScalePercent = 125,
            WidgetOpacityPercent = 80,
            ShowWidgetShortTermUsage = false,
            ShowWidgetWeeklyUsage = true,
            ShowWidgetWeeklyProgress = false,
            ThemePreference = WindowsThemePreference.Dark,
        });
        WindowsSettingsPreferences? saved = null;
        viewModel.SaveRequested += preferences => saved = preferences;

        viewModel.RestoreDefaultsCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Equal(125, saved.WidgetScalePercent);
        Assert.Equal(80, saved.WidgetOpacityPercent);
        Assert.False(saved.ShowWidgetShortTermUsage);
        Assert.True(saved.ShowWidgetWeeklyUsage);
        Assert.False(saved.ShowWidgetWeeklyProgress);
        Assert.Equal(WindowsThemePreference.Dark, saved.ThemePreference);
    }

    [Fact]
    public void SettingsViewModel_RequiresAtLeastOneWidgetUsageLimit()
    {
        var viewModel = new WindowsSettingsViewModel(new WindowsAppSettings());
        WindowsSettingsPreferences? saved = null;
        viewModel.SaveRequested += preferences => saved = preferences;
        viewModel.ShowWidgetShortTermUsage = false;
        viewModel.ShowWidgetWeeklyUsage = false;

        viewModel.SaveCommand.Execute(null);

        Assert.Null(saved);
        Assert.Equal("Choose at least one widget usage limit.", viewModel.ValidationMessage);
        Assert.True(viewModel.HasWidgetContentValidationError);
    }

    [Fact]
    public void SettingsViewModel_RaisesPauseAndResumeAlertCommands()
    {
        var until = DateTimeOffset.UtcNow.AddHours(1);
        var viewModel = new WindowsSettingsViewModel(new WindowsAppSettings
        {
            AlertsPausedUntil = until,
        });
        var pauseHours = 0;
        var resumeRequested = false;
        viewModel.PauseAlertsRequested += hours => pauseHours = hours;
        viewModel.ResumeAlertsRequested += () => resumeRequested = true;

        viewModel.PauseAlertsHours = 4;
        viewModel.PauseAlertsCommand.Execute(null);
        viewModel.ResumeAlertsCommand.Execute(null);

        Assert.Equal(4, pauseHours);
        Assert.True(resumeRequested);
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

    [Fact]
    public void UsageThresholdNotifier_UsesConfiguredThresholds()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings
        {
            WarningThresholdPercent = 60,
            CriticalThresholdPercent = 90,
        };

        var warning = notifier.Evaluate(Snapshot(61, ResetAt), settings, out var history);
        var critical = notifier.Evaluate(
            Snapshot(91, ResetAt),
            settings with { AlertHistory = history },
            out _);

        Assert.Equal(60, Assert.Single(warning).ThresholdPercent);
        Assert.Equal(90, Assert.Single(critical).ThresholdPercent);
    }

    [Fact]
    public void UsageThresholdNotifier_PreservesThresholdHistoryAcrossResetScheduleCorrection()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings();
        var initial = notifier.Evaluate(
            Snapshot(82, ResetAt, ResetAt.AddHours(-2)),
            settings,
            out var initialHistory);

        var corrected = notifier.Evaluate(
            Snapshot(82, ResetAt.AddMinutes(2), ResetAt.AddHours(-2).AddMinutes(1)),
            settings with { AlertHistory = initialHistory },
            out var correctedHistory);

        Assert.Single(initial);
        Assert.Empty(corrected);
        Assert.Equal(80, correctedHistory.Weekly?.HighestNotifiedPercent);
        Assert.Equal(ResetAt.AddMinutes(2), correctedHistory.Weekly?.ResetsAt);
    }

    [Fact]
    public void UsageThresholdNotifier_ResetsHistoryAfterRolloverFollowingScheduleCorrection()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings();
        _ = notifier.Evaluate(
            Snapshot(82, ResetAt, ResetAt.AddHours(-2)),
            settings,
            out var initialHistory);
        _ = notifier.Evaluate(
            Snapshot(82, ResetAt.AddMinutes(2), ResetAt.AddHours(-2).AddMinutes(1)),
            settings with { AlertHistory = initialHistory },
            out var correctedHistory);

        var nextWindow = notifier.Evaluate(
            Snapshot(82, ResetAt.AddDays(7).AddMinutes(2), ResetAt.AddMinutes(3)),
            settings with { AlertHistory = correctedHistory },
            out _);

        Assert.Single(nextWindow);
        Assert.Equal(80, nextWindow[0].ThresholdPercent);
    }

    [Fact]
    public void UsageThresholdNotifier_RespectsPerLimitSwitches()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings
        {
            ShortTermAlertsEnabled = true,
            WeeklyAlertsEnabled = false,
        };
        var snapshot = new CodexUsageSnapshot
        {
            RetrievedAt = ResetAt.AddHours(-1),
            Limits =
            [
                new UsageLimit("short", "5-hour", UsageLimitKind.ShortTerm, 90, TimeSpan.FromHours(5), ResetAt),
                new UsageLimit("weekly", "Weekly", UsageLimitKind.Weekly, 90, TimeSpan.FromDays(7), ResetAt),
            ],
        };

        var alerts = notifier.Evaluate(snapshot, settings, out _);

        Assert.Single(alerts);
        Assert.Equal(UsageLimitKind.ShortTerm, alerts[0].Limit.Kind);
    }

    [Fact]
    public void UsageThresholdNotifier_SuppressesAlertsDuringQuietHours()
    {
        var notifier = new UsageThresholdNotifier(new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));
        var settings = new WindowsAppSettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = 10,
            QuietHoursEnd = 14,
        };

        var alerts = notifier.Evaluate(Snapshot(97, ResetAt), settings, out _);

        Assert.Empty(alerts);
    }

    [Fact]
    public void UsageThresholdNotifier_SuppressesAlertsWhilePaused()
    {
        var now = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var notifier = new UsageThresholdNotifier(new FixedTimeProvider(now));
        var settings = new WindowsAppSettings { AlertsPausedUntil = now.AddHours(1) };

        var alerts = notifier.Evaluate(Snapshot(97d, ResetAt), settings, out var history);

        Assert.Empty(alerts);
        Assert.Equal(0, history.Weekly?.HighestNotifiedPercent);
    }

    [Fact]
    public void UsageThresholdNotifier_SuppressesAlertsDuringOvernightQuietHours()
    {
        var notifier = new UsageThresholdNotifier(new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 29, 23, 0, 0, TimeSpan.Zero)));
        var settings = new WindowsAppSettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = 22,
            QuietHoursEnd = 8,
        };

        var alerts = notifier.Evaluate(Snapshot(97, ResetAt), settings, out _);

        Assert.Empty(alerts);
    }

    [Fact]
    public void UsageThresholdNotifier_AllowsAlertsOutsideOvernightQuietHours()
    {
        var notifier = new UsageThresholdNotifier(new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));
        var settings = new WindowsAppSettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = 22,
            QuietHoursEnd = 8,
        };

        var alerts = notifier.Evaluate(Snapshot(97, ResetAt), settings, out _);

        Assert.Single(alerts);
    }

    [Fact]
    public void UsageThresholdNotifier_IgnoresInvalidEqualQuietHours()
    {
        var snapshot = Snapshot(85, ResetAt);
        var settings = new WindowsAppSettings
        {
            UsageAlertsEnabled = true,
            QuietHoursEnabled = true,
            QuietHoursStart = 10,
            QuietHoursEnd = 10,
        };

        var alerts = new UsageThresholdNotifier().Evaluate(snapshot, settings, out _);

        Assert.Contains(alerts, alert => alert.ThresholdPercent == 80);
    }

    [Fact]
    public void UsageThresholdNotifier_NotifiesOnceBeforeReset()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings
        {
            ResetReminderEnabled = true,
            ResetReminderMinutes = 30,
        };
        var snapshot = Snapshot(20, ResetAt, ResetAt.AddMinutes(-20));

        var first = notifier.Evaluate(snapshot, settings, out var history);
        var repeated = notifier.Evaluate(
            snapshot,
            settings with { AlertHistory = history },
            out _);

        Assert.Contains(first, alert => alert.ResetRemaining is not null);
        Assert.DoesNotContain(repeated, alert => alert.ResetRemaining is not null);
    }

    [Fact]
    public void UsageThresholdNotifier_PreservesResetReminderAcrossScheduleCorrection()
    {
        var notifier = new UsageThresholdNotifier();
        var settings = new WindowsAppSettings
        {
            ResetReminderEnabled = true,
            ResetReminderMinutes = 30,
        };
        _ = notifier.Evaluate(
            Snapshot(20, ResetAt, ResetAt.AddMinutes(-20)),
            settings,
            out var history);

        var corrected = notifier.Evaluate(
            Snapshot(20, ResetAt.AddMinutes(2), ResetAt.AddMinutes(-19)),
            settings with { AlertHistory = history },
            out var correctedHistory);

        Assert.DoesNotContain(corrected, alert => alert.ResetRemaining is not null);
        Assert.True(correctedHistory.Weekly?.ResetReminderNotified is true);
    }

    [Fact]
    public async Task UsageHistoryStore_RecoversACompleteTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "usage-history.json");
        try
        {
            var store = new JsonUsageHistoryStore(path);
            await store.SaveAsync(new CodexUsage.Core.UsageHistory.UsageHistoryState());
            File.Move(path, path + ".tmp");

            var state = await store.LoadAsync();

            Assert.Equal(CodexUsage.Core.UsageHistory.UsageHistoryState.CurrentVersion, state.Version);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UsageHistoryStore_PreservesACorruptTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "usage-history.json");
        try
        {
            await File.WriteAllTextAsync(path + ".tmp", "not-json");
            var store = new JsonUsageHistoryStore(path);

            var state = await store.LoadAsync();

            Assert.Empty(state.Windows);
            Assert.Single(Directory.GetFiles(directory, "*.corrupt-*"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task UsageHistoryStore_DoesNotPersistDerivedAccessibilityOrObservationText()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "CodexUsageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "usage-history.json");
        try
        {
            var at = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
            var state = new CodexUsage.Core.UsageHistory.UsageHistoryState
            {
                Windows =
                [
                    new CodexUsage.Core.UsageHistory.WeeklyUsageWindowEntry
                    {
                        LimitId = "weekly",
                        WindowInstanceId = "window",
                        CalculatedWindowStartedAt = at,
                        InitialScheduledResetAt = at.AddDays(7),
                        FirstObservedAt = at,
                        LastObservedAt = at,
                        ObservedDayCount = 1,
                    },
                ],
            };
            var store = new JsonUsageHistoryStore(path);

            await store.SaveAsync(state);
            var json = await File.ReadAllTextAsync(path);

            Assert.DoesNotContain("IsPartialObservation", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ObservationSummaryText", json, StringComparison.Ordinal);
            Assert.DoesNotContain("AccessibilitySummaryText", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static CodexUsageSnapshot Snapshot(
        double weeklyUsedPercent,
        DateTimeOffset resetAt,
        DateTimeOffset? retrievedAt = null) =>
        new()
        {
            RetrievedAt = retrievedAt ?? ResetAt,
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeStartupRegistry : WindowsStartupService.IWindowsStartupRegistry
    {
        public string? Value { get; set; }

        public bool Deleted { get; private set; }

        public void DeleteValue(string name, bool throwOnMissingValue)
        {
            Deleted = true;
            Value = null;
        }

        public string? GetValue(string name) => Value;

        public void SetValue(string name, string value) => Value = value;

        public void Dispose()
        {
        }
    }
}
