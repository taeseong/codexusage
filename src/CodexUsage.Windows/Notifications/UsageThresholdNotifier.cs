using CodexUsage.Core.Usage;
using CodexUsage.Windows.Settings;

namespace CodexUsage.Windows.Notifications;

internal sealed class UsageThresholdNotifier
{
    internal const int WarningThreshold = 80;
    internal const int CriticalThreshold = 95;
    private static readonly TimeSpan ResetJitter = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _timeProvider;

    public UsageThresholdNotifier(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<UsageThresholdAlert> Evaluate(
        CodexUsageSnapshot snapshot,
        WindowsAppSettings settings,
        out UsageAlertHistory updatedHistory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settings);

        var alerts = new List<UsageThresholdAlert>();
        var quietHoursActive = IsQuietHoursActive(settings, _timeProvider.GetLocalNow());
        var alertsPaused = settings.AlertsPausedUntil is { } pausedUntil &&
            pausedUntil > _timeProvider.GetUtcNow();
        var shortTerm = EvaluateLimit(
            snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.ShortTerm),
            settings.AlertHistory.ShortTerm,
            settings.UsageAlertsEnabled && settings.ShortTermAlertsEnabled && !quietHoursActive && !alertsPaused,
            settings.WarningThresholdPercent,
            settings.CriticalThresholdPercent,
            settings.ResetReminderEnabled,
            settings.ResetReminderMinutes,
            snapshot.RetrievedAt,
            alerts);
        var weekly = EvaluateLimit(
            snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.Weekly),
            settings.AlertHistory.Weekly,
            settings.UsageAlertsEnabled && settings.WeeklyAlertsEnabled && !quietHoursActive && !alertsPaused,
            settings.WarningThresholdPercent,
            settings.CriticalThresholdPercent,
            settings.ResetReminderEnabled,
            settings.ResetReminderMinutes,
            snapshot.RetrievedAt,
            alerts);
        updatedHistory = new UsageAlertHistory
        {
            ShortTerm = shortTerm,
            Weekly = weekly,
        };
        return alerts;
    }

    private static UsageLimitAlertHistory? EvaluateLimit(
        UsageLimit? limit,
        UsageLimitAlertHistory? previous,
        bool alertsEnabled,
        int warningThreshold,
        int criticalThreshold,
        bool resetReminderEnabled,
        int resetReminderMinutes,
        DateTimeOffset now,
        ICollection<UsageThresholdAlert> alerts)
    {
        if (limit is null)
        {
            return previous;
        }

        var sameWindow = IsSameWindow(previous, limit, now);
        var alreadyNotified = sameWindow ? previous?.HighestNotifiedPercent ?? 0 : 0;
        var resetReminderNotified = sameWindow && previous?.ResetReminderNotified is true;
        var threshold = limit.UsedPercent >= criticalThreshold
            ? criticalThreshold
            : limit.UsedPercent >= warningThreshold
                ? warningThreshold
                : 0;

        if (alertsEnabled && threshold > alreadyNotified)
        {
            alerts.Add(UsageThresholdAlert.ForThreshold(limit, threshold));
        }

        var resetRemaining = limit.ResetsAt - now;
        if (alertsEnabled &&
            resetReminderEnabled &&
            !resetReminderNotified &&
            resetRemaining is { } remaining &&
            remaining > TimeSpan.Zero &&
            remaining <= TimeSpan.FromMinutes(resetReminderMinutes))
        {
            alerts.Add(UsageThresholdAlert.ForReset(limit, remaining));
            resetReminderNotified = true;
        }

        return new UsageLimitAlertHistory
        {
            ResetsAt = limit.ResetsAt,
            WindowDuration = limit.WindowDuration,
            LastObservedAt = now,
            LastObservedPercent = limit.UsedPercent,
            HighestNotifiedPercent = Math.Max(alreadyNotified, alertsEnabled ? threshold : 0),
            ResetReminderNotified = resetReminderNotified,
        };
    }

    private static bool IsSameWindow(
        UsageLimitAlertHistory? previous,
        UsageLimit limit,
        DateTimeOffset now)
    {
        if (previous is null)
        {
            return false;
        }

        if (previous.ResetsAt == limit.ResetsAt)
        {
            return true;
        }

        if (previous.ResetsAt is not { } oldReset || limit.ResetsAt is not { } newReset)
        {
            return true;
        }

        var resetDelta = newReset - oldReset;
        if (resetDelta.Duration() <= ResetJitter)
        {
            return true;
        }

        var duration = limit.WindowDuration ?? previous.WindowDuration;
        if (duration is { } windowDuration &&
            resetDelta >= windowDuration - ResetJitter)
        {
            return false;
        }

        if (duration is { } calculatedDuration &&
            previous.LastObservedAt is { } lastObservedAt)
        {
            var calculatedStart = newReset - calculatedDuration;
            if (calculatedStart > lastObservedAt &&
                (calculatedStart > lastObservedAt + ResetJitter ||
                 limit.UsedPercent < previous.LastObservedPercent))
            {
                return false;
            }
        }

        return !(oldReset <= now &&
                 newReset > now &&
                 limit.UsedPercent < previous.LastObservedPercent);
    }

    private static bool IsQuietHoursActive(WindowsAppSettings settings, DateTimeOffset localNow)
    {
        if (!settings.QuietHoursEnabled)
        {
            return false;
        }

        if (settings.QuietHoursStart == settings.QuietHoursEnd)
        {
            return false;
        }

        var hour = localNow.Hour;
        return settings.QuietHoursStart < settings.QuietHoursEnd
            ? hour >= settings.QuietHoursStart && hour < settings.QuietHoursEnd
            : hour >= settings.QuietHoursStart || hour < settings.QuietHoursEnd;
    }
}

internal sealed record UsageThresholdAlert(
    UsageLimit Limit,
    int? ThresholdPercent,
    TimeSpan? ResetRemaining)
{
    public string Title => ThresholdPercent is { } threshold
        ? $"CodexUsage: {threshold}% used"
        : "CodexUsage: limit resets soon";

    public string Message => ThresholdPercent is not null
        ? $"{Limit.DisplayName} usage is {Math.Round(Limit.UsedPercent):0}%."
        : $"{Limit.DisplayName} resets in {Math.Max(1, Math.Ceiling(ResetRemaining?.TotalMinutes ?? 1)):0} minutes.";

    public static UsageThresholdAlert ForThreshold(UsageLimit limit, int threshold) =>
        new(limit, threshold, null);

    public static UsageThresholdAlert ForReset(UsageLimit limit, TimeSpan remaining) =>
        new(limit, null, remaining);
}
