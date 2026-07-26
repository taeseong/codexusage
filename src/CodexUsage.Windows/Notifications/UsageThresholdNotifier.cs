using CodexUsage.Core.Usage;
using CodexUsage.Windows.Settings;

namespace CodexUsage.Windows.Notifications;

internal sealed class UsageThresholdNotifier
{
    internal const int WarningThreshold = 80;
    internal const int CriticalThreshold = 95;

    public IReadOnlyList<UsageThresholdAlert> Evaluate(
        CodexUsageSnapshot snapshot,
        WindowsAppSettings settings,
        out UsageAlertHistory updatedHistory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settings);

        var alerts = new List<UsageThresholdAlert>();
        var shortTerm = EvaluateLimit(
            snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.ShortTerm),
            settings.AlertHistory.ShortTerm,
            settings.UsageAlertsEnabled,
            alerts);
        var weekly = EvaluateLimit(
            snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.Weekly),
            settings.AlertHistory.Weekly,
            settings.UsageAlertsEnabled,
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
        ICollection<UsageThresholdAlert> alerts)
    {
        if (limit is null)
        {
            return previous;
        }

        var sameWindow = previous?.ResetsAt == limit.ResetsAt;
        var alreadyNotified = sameWindow ? previous?.HighestNotifiedPercent ?? 0 : 0;
        var threshold = limit.UsedPercent >= CriticalThreshold
            ? CriticalThreshold
            : limit.UsedPercent >= WarningThreshold
                ? WarningThreshold
                : 0;

        if (alertsEnabled && threshold > alreadyNotified)
        {
            alerts.Add(new UsageThresholdAlert(limit, threshold));
        }

        return new UsageLimitAlertHistory
        {
            ResetsAt = limit.ResetsAt,
            HighestNotifiedPercent = Math.Max(alreadyNotified, alertsEnabled ? threshold : 0),
        };
    }
}

internal sealed record UsageThresholdAlert(UsageLimit Limit, int ThresholdPercent)
{
    public string Title => $"CodexUsage: {ThresholdPercent}% used";

    public string Message => $"{Limit.DisplayName} usage is {Math.Round(Limit.UsedPercent):0}%.";
}
