namespace CodexUsage.Core.Usage;

public sealed record CodexUsageResult
{
    public required CodexUsageStatus Status { get; init; }

    public CodexUsageSnapshot? Snapshot { get; init; }

    public string? Detail { get; init; }

    public bool IsSuccess => Status is CodexUsageStatus.Success;
}

