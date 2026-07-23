using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Tests;

public sealed class UsageLimitTests
{
    [Theory]
    [InlineData(-1, 0, 100)]
    [InlineData(0, 0, 100)]
    [InlineData(34, 34, 66)]
    [InlineData(100, 100, 0)]
    [InlineData(101, 100, 0)]
    public void ConstructorClampsUsedAndCalculatesRemaining(double input, double used, double remaining)
    {
        var limit = Create(input, null);

        Assert.Equal(used, limit.UsedPercent);
        Assert.Equal(remaining, limit.RemainingPercent);
    }

    [Fact]
    public void TimeUntilResetReturnsRemainingDuration()
    {
        var now = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        var limit = Create(10, now.AddHours(2));

        Assert.Equal(TimeSpan.FromHours(2), limit.TimeUntilReset(now));
    }

    [Fact]
    public void TimeUntilResetDoesNotReturnNegativeDuration()
    {
        var now = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        var limit = Create(10, now.AddSeconds(-1));

        Assert.Equal(TimeSpan.Zero, limit.TimeUntilReset(now));
    }

    [Fact]
    public void ResetTimestampConvertsFromUtcToLocalWithoutChangingInstant()
    {
        var utc = DateTimeOffset.FromUnixTimeSeconds(1_785_245_175);
        var local = utc.ToLocalTime();

        Assert.Equal(utc, local);
        Assert.Equal(utc.ToUnixTimeSeconds(), local.ToUnixTimeSeconds());
    }

    private static UsageLimit Create(double used, DateTimeOffset? reset) =>
        new("codex:test", "Test", UsageLimitKind.Unknown, used, null, reset);
}

