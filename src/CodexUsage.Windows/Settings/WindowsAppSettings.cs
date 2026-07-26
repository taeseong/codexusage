using CodexUsage.Windows.Windowing;

namespace CodexUsage.Windows.Settings;

internal sealed record WindowsAppSettings
{
    public bool IsWidgetVisible { get; init; } = true;

    public WidgetInteractionMode WidgetMode { get; init; } = WidgetInteractionMode.Editing;

    public bool StartAtLogin { get; init; }

    public bool UsageAlertsEnabled { get; init; } = true;

    public UsageAlertHistory AlertHistory { get; init; } = new();
}

internal sealed record UsageAlertHistory
{
    public UsageLimitAlertHistory? ShortTerm { get; init; }

    public UsageLimitAlertHistory? Weekly { get; init; }
}

internal sealed record UsageLimitAlertHistory
{
    public DateTimeOffset? ResetsAt { get; init; }

    public int HighestNotifiedPercent { get; init; }
}
