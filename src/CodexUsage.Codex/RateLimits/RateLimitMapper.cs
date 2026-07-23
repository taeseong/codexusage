using CodexUsage.Codex.Protocol;
using CodexUsage.Core.Usage;

namespace CodexUsage.Codex.RateLimits;

internal static class RateLimitMapper
{
    private const long OneDayMinutes = 24 * 60;
    private const long OneWeekMinutes = 7 * OneDayMinutes;

    public static CodexUsageSnapshot Map(
        AccountReadResponse accountResponse,
        RateLimitsReadResponse rateLimitsResponse,
        DateTimeOffset retrievedAt)
    {
        var snapshots = rateLimitsResponse.RateLimits is not null
            ? [rateLimitsResponse.RateLimits]
            : rateLimitsResponse.RateLimitsByLimitId is { Count: > 0 }
                ? rateLimitsResponse.RateLimitsByLimitId.Values
                : throw new AppServerResponseFormatException("Rate-limit response omitted all snapshots.");

        var limits = new List<UsageLimit>();
        string? rateLimitPlan = null;
        foreach (var snapshot in snapshots)
        {
            rateLimitPlan ??= snapshot.PlanType;
            AddWindow(limits, snapshot, snapshot.Primary, "primary");
            AddWindow(limits, snapshot, snapshot.Secondary, "secondary");
        }

        if (limits.Count is 0)
        {
            throw new AppServerResponseFormatException("Rate-limit response contained no usage windows.");
        }

        return new CodexUsageSnapshot
        {
            Limits = limits,
            RetrievedAt = retrievedAt,
            AccountPlan = accountResponse.Account?.PlanType,
            RateLimitPlan = rateLimitPlan,
        };
    }

    private static void AddWindow(
        ICollection<UsageLimit> destination,
        RateLimitSnapshotDto snapshot,
        RateLimitWindowDto? window,
        string position)
    {
        if (window is null)
        {
            return;
        }

        if (window.UsedPercent is null)
        {
            throw new AppServerResponseFormatException($"The {position} rate-limit window omitted usedPercent.");
        }

        var kind = Classify(window.WindowDurationMins);
        var limitId = snapshot.LimitId ?? "unknown";
        destination.Add(new UsageLimit(
            $"{limitId}:{position}",
            DisplayName(kind, snapshot.LimitName, position),
            kind,
            window.UsedPercent.Value,
            window.WindowDurationMins is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            window.ResetsAt is { } timestamp ? DateTimeOffset.FromUnixTimeSeconds(timestamp) : null));
    }

    internal static UsageLimitKind Classify(long? windowDurationMinutes) => windowDurationMinutes switch
    {
        > 0 and <= OneDayMinutes => UsageLimitKind.ShortTerm,
        >= OneWeekMinutes => UsageLimitKind.Weekly,
        _ => UsageLimitKind.Unknown,
    };

    private static string DisplayName(UsageLimitKind kind, string? serverName, string position) => kind switch
    {
        UsageLimitKind.ShortTerm => "Short-term",
        UsageLimitKind.Weekly => "Weekly",
        _ => serverName ?? $"Unknown ({position})",
    };
}
