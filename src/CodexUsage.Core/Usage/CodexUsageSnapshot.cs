namespace CodexUsage.Core.Usage;

public sealed record CodexUsageSnapshot
{
    public required IReadOnlyList<UsageLimit> Limits { get; init; }

    public required DateTimeOffset RetrievedAt { get; init; }

    public string? AccountPlan { get; init; }

    public string? RateLimitPlan { get; init; }

    public bool HasPlanMismatch =>
        AccountPlan is not null &&
        RateLimitPlan is not null &&
        !string.Equals(AccountPlan, RateLimitPlan, StringComparison.OrdinalIgnoreCase);
}

