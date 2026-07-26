using CodexUsage.Core.Usage;

namespace CodexUsage.Core.UsageHistory;

public sealed class UsageHistoryService
{
    private static readonly TimeSpan ResetJitter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    public bool Observe(UsageHistoryState state, CodexUsageSnapshot snapshot, out UsageHistoryState updated)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(snapshot);
        var windows = (state.Windows ?? []).Where(static entry => entry is not null).ToList();
        var changed = false;
        var plan = GetPlan(snapshot);

        foreach (var limit in snapshot.Limits.Where(static limit => limit.Kind is UsageLimitKind.Weekly))
        {
            var now = snapshot.RetrievedAt;
            var active = windows.LastOrDefault(entry => entry.LimitId == limit.Id && entry.ClosureKind == UsageWindowClosureKind.InProgress);
            DateTimeOffset? calculatedStart = limit.ResetsAt is { } reset && limit.WindowDuration is { } duration
                ? reset - duration : null;

            if (active is null)
            {
                windows.Add(Create(limit, now, calculatedStart, plan));
                changed = true;
                continue;
            }

            var rollover = IsCertainRollover(active, limit, calculatedStart);
            var uncertain = false;
            if (!rollover && IsFallbackRollover(active, limit, now))
            {
                rollover = true;
                uncertain = calculatedStart is null;
            }

            if (rollover)
            {
                var observedReset = calculatedStart ?? now;
                var kind = uncertain
                    ? UsageWindowClosureKind.ResetTimingUncertain
                    : active.InitialScheduledResetAt is { } initialReset && observedReset < initialReset - ResetJitter
                        ? UsageWindowClosureKind.EarlyResetObserved
                        : UsageWindowClosureKind.NormalResetObserved;
                var closed = active with { ActualResetObservedAt = observedReset, ClosureKind = kind };
                windows[windows.IndexOf(active)] = closed;
                windows.Add(Create(limit, now, calculatedStart, plan));
                changed = true;
                continue;
            }

            var next = Update(active, limit, now, plan);
            if (next != active)
            {
                windows[windows.IndexOf(active)] = next;
                changed = true;
            }
        }

        var cutoff = snapshot.RetrievedAt - Retention;
        var retained = windows.Where(entry => entry.ClosureKind == UsageWindowClosureKind.InProgress || entry.LastObservedAt >= cutoff).ToList();
        changed |= retained.Count != windows.Count;
        updated = new UsageHistoryState { Version = UsageHistoryState.CurrentVersion, Windows = retained };
        return changed;
    }

    public UsageHistoryMetrics CalculateMetrics(UsageHistoryState state, string? currentPlan)
    {
        var plan = NormalizePlan(currentPlan);
        var completed = state.Windows.Where(entry =>
            entry.ClosureKind == UsageWindowClosureKind.NormalResetObserved &&
            (plan is null || entry.ObservedPlans.Any(value => string.Equals(value, plan, StringComparison.OrdinalIgnoreCase))))
            .ToList();
        var peaks = completed.Select(entry => entry.PeakObservedPercent).ToList();
        return new UsageHistoryMetrics(
            completed.Count,
            peaks.Count == 0 ? null : peaks.Average(),
            peaks.Count == 0 ? null : peaks.Max(),
            peaks.Count(value => value >= 80),
            peaks.Count(value => value >= 95),
            state.Windows.Count(entry => entry.ClosureKind == UsageWindowClosureKind.EarlyResetObserved));
    }

    private static WeeklyUsageWindowEntry Create(UsageLimit limit, DateTimeOffset now, DateTimeOffset? start, string? plan) => new()
    {
        LimitId = limit.Id,
        WindowInstanceId = Guid.NewGuid().ToString("N"),
        CalculatedWindowStartedAt = start,
        InitialScheduledResetAt = limit.ResetsAt,
        LastScheduledResetAt = limit.ResetsAt,
        PeakObservedPercent = limit.UsedPercent,
        LastObservedPercent = limit.UsedPercent,
        ObservedDayCount = 1,
        FirstObservedAt = now,
        LastObservedAt = now,
        ObservedPlans = plan is null ? [] : [plan],
    };

    private static WeeklyUsageWindowEntry Update(WeeklyUsageWindowEntry entry, UsageLimit limit, DateTimeOffset now, string? plan)
    {
        var plans = entry.ObservedPlans.ToList();
        if (plan is not null && !plans.Contains(plan, StringComparer.OrdinalIgnoreCase)) plans.Add(plan);
        var isNewDay = entry.LastObservedAt.Date != now.Date;
        return entry with
        {
            LastScheduledResetAt = limit.ResetsAt ?? entry.LastScheduledResetAt,
            PeakObservedPercent = Math.Max(entry.PeakObservedPercent, limit.UsedPercent),
            LastObservedPercent = limit.UsedPercent,
            ObservedDayCount = entry.ObservedDayCount + (isNewDay ? 1 : 0),
            LastObservedAt = now,
            ObservedPlans = plans,
        };
    }

    private static bool IsCertainRollover(
        WeeklyUsageWindowEntry entry,
        UsageLimit limit,
        DateTimeOffset? calculatedStart)
    {
        // A small server-side reset schedule correction is not a new window. Require a
        // meaningful gap after our last observation, unless a full next scheduling
        // period or an observed usage drop independently confirms the rollover.
        if (calculatedStart is not { } start || start <= entry.LastObservedAt)
        {
            return false;
        }

        if (start > entry.LastObservedAt + ResetJitter || limit.UsedPercent < entry.LastObservedPercent)
        {
            return true;
        }

        return entry.LastScheduledResetAt is { } oldReset &&
               limit.ResetsAt is { } newReset &&
               limit.WindowDuration is { } duration &&
               newReset - oldReset >= duration - ResetJitter;
    }

    private static bool IsFallbackRollover(WeeklyUsageWindowEntry entry, UsageLimit limit, DateTimeOffset now) =>
        entry.LastScheduledResetAt is { } oldReset && limit.ResetsAt is { } newReset &&
        oldReset <= now && newReset > now && limit.UsedPercent < entry.LastObservedPercent;

    private static string? GetPlan(CodexUsageSnapshot snapshot) =>
        NormalizePlan(snapshot.AccountPlan) ?? NormalizePlan(snapshot.RateLimitPlan);

    private static string? NormalizePlan(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
