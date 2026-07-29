using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Settings;

internal sealed record WindowsAppSettings
{
    public bool IsWidgetVisible { get; init; } = true;

    public WidgetInteractionMode WidgetMode { get; init; } = WidgetInteractionMode.Editing;

    public bool StartAtLogin { get; init; }

    public bool UsageAlertsEnabled { get; init; } = true;

    public bool ShortTermAlertsEnabled { get; init; } = true;

    public bool WeeklyAlertsEnabled { get; init; } = true;

    public int WarningThresholdPercent { get; init; } = 80;

    public int CriticalThresholdPercent { get; init; } = 95;

    public bool QuietHoursEnabled { get; init; }

    public int QuietHoursStart { get; init; } = 22;

    public int QuietHoursEnd { get; init; } = 8;

    public bool ResetReminderEnabled { get; init; }

    public int ResetReminderMinutes { get; init; } = 30;

    public DetailsWindowSettings DetailsWindow { get; init; } = new();

    public UsageAlertHistory AlertHistory { get; init; } = new();
}

internal sealed record DetailsWindowSettings
{
    public int? X { get; init; }

    public int? Y { get; init; }

    public double Width { get; init; } = 420;

    public double Height { get; init; } = 660;

    public int SelectedTabIndex { get; init; }
}

internal sealed record UsageAlertHistory
{
    public UsageLimitAlertHistory? ShortTerm { get; init; }

    public UsageLimitAlertHistory? Weekly { get; init; }
}

internal sealed record UsageLimitAlertHistory
{
    public DateTimeOffset? ResetsAt { get; init; }

    public TimeSpan? WindowDuration { get; init; }

    public DateTimeOffset? LastObservedAt { get; init; }

    public double LastObservedPercent { get; init; }

    public int HighestNotifiedPercent { get; init; }

    public bool ResetReminderNotified { get; init; }
}
