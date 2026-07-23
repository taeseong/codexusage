namespace CodexUsage.Core.Usage;

public sealed record UsageLimit
{
    public UsageLimit(
        string id,
        string displayName,
        UsageLimitKind kind,
        double usedPercent,
        TimeSpan? windowDuration,
        DateTimeOffset? resetsAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        DisplayName = displayName;
        Kind = kind;
        UsedPercent = Math.Clamp(usedPercent, 0d, 100d);
        WindowDuration = windowDuration;
        ResetsAt = resetsAt;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public UsageLimitKind Kind { get; }

    public double UsedPercent { get; }

    public double RemainingPercent => 100d - UsedPercent;

    public TimeSpan? WindowDuration { get; }

    public DateTimeOffset? ResetsAt { get; }

    public TimeSpan? TimeUntilReset(DateTimeOffset now)
    {
        if (ResetsAt is null)
        {
            return null;
        }

        return ResetsAt.Value <= now ? TimeSpan.Zero : ResetsAt.Value - now;
    }
}

