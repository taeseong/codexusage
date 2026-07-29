using CodexUsage.Core.Usage;

namespace CodexUsage.Desktop.ViewModels;

public sealed class UsageRefreshSchedule
{
    private static readonly TimeSpan[] DefaultRetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
    ];

    private readonly IReadOnlyList<TimeSpan> _retryDelays;

    public UsageRefreshSchedule(
        TimeSpan? normalInterval = null,
        IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        NormalInterval = normalInterval ?? TimeSpan.FromMinutes(1);
        _retryDelays = retryDelays ?? DefaultRetryDelays;

        if (NormalInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(normalInterval));
        }

        if (_retryDelays.Count == 0 || _retryDelays.Any(static delay => delay <= TimeSpan.Zero))
        {
            throw new ArgumentException(
                "At least one positive retry delay is required.",
                nameof(retryDelays));
        }
    }

    public TimeSpan NormalInterval { get; }

    public TimeSpan GetNextDelay(
        CodexUsageStatus? status,
        int consecutiveTransientFailures)
    {
        if (!IsTransient(status) || consecutiveTransientFailures <= 0)
        {
            return NormalInterval;
        }

        var index = Math.Min(consecutiveTransientFailures - 1, _retryDelays.Count - 1);
        return _retryDelays[index];
    }

    public static bool IsTransient(CodexUsageStatus? status) =>
        status is
            CodexUsageStatus.NetworkError or
            CodexUsageStatus.TimedOut or
            CodexUsageStatus.ProtocolError or
            CodexUsageStatus.ResponseFormatChanged or
            CodexUsageStatus.UnknownError;
}
