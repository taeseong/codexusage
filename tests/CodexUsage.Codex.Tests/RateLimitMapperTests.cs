using CodexUsage.Codex.Protocol;
using CodexUsage.Codex.RateLimits;
using CodexUsage.Core.Usage;

namespace CodexUsage.Codex.Tests;

public sealed class RateLimitMapperTests
{
    [Theory]
    [InlineData(300L, UsageLimitKind.ShortTerm)]
    [InlineData(1440L, UsageLimitKind.ShortTerm)]
    [InlineData(10080L, UsageLimitKind.Weekly)]
    [InlineData(20160L, UsageLimitKind.Weekly)]
    [InlineData(null, UsageLimitKind.Unknown)]
    [InlineData(2880L, UsageLimitKind.Unknown)]
    public void ClassifiesWindowByDuration(long? minutes, UsageLimitKind expected) =>
        Assert.Equal(expected, RateLimitMapper.Classify(minutes));

    [Fact]
    public void MapsShortTermWeeklyAndUnknownWindows()
    {
        var response = new RateLimitsReadResponse(
            null,
            new Dictionary<string, RateLimitSnapshotDto>
            {
                ["codex"] = new("codex", null, new(20, 300, 1000), new(40, 10080, 2000), "pro"),
                ["other"] = new("other", "Other", new(60, 2880, 3000), null, "pro"),
            });

        var result = RateLimitMapper.Map(new(new("chatgpt", "pro"), true), response, DateTimeOffset.UnixEpoch);

        Assert.Collection(
            result.Limits,
            limit => Assert.Equal(UsageLimitKind.ShortTerm, limit.Kind),
            limit => Assert.Equal(UsageLimitKind.Weekly, limit.Kind),
            limit => Assert.Equal(UsageLimitKind.Unknown, limit.Kind));
    }

    [Fact]
    public void ThrowsWhenUsedPercentIsMissing()
    {
        var response = new RateLimitsReadResponse(new("codex", null, new(null, 300, 1000), null, null), null);

        Assert.Throws<AppServerResponseFormatException>(() =>
            RateLimitMapper.Map(new(new("chatgpt", "pro"), true), response, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void PrefersCanonicalRateLimitsOverModelSpecificBuckets()
    {
        var canonical = new RateLimitSnapshotDto(
            "codex",
            null,
            new(18, 10080, 1000),
            null,
            "prolite");
        var response = new RateLimitsReadResponse(
            canonical,
            new Dictionary<string, RateLimitSnapshotDto>
            {
                ["codex_bengalfox"] = new(
                    "codex_bengalfox",
                    "GPT-5.3-Codex-Spark",
                    new(0, 10080, 2000),
                    null,
                    "prolite"),
                ["codex"] = canonical,
            });

        var result = RateLimitMapper.Map(
            new(new("chatgpt", "pro"), true),
            response,
            DateTimeOffset.UnixEpoch);

        var weekly = Assert.Single(
            result.Limits,
            limit => limit.Kind is UsageLimitKind.Weekly);
        Assert.Equal("codex:primary", weekly.Id);
        Assert.Equal(18d, weekly.UsedPercent);
    }
}
