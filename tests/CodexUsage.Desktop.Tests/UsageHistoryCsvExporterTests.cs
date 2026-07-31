using System.Text;
using CodexUsage.Core.UsageHistory;
using CodexUsage.Desktop.UsageHistory;

namespace CodexUsage.Desktop.Tests;

public sealed class UsageHistoryCsvExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesObservedUsageOnlyAndEscapesPlans()
    {
        var entry = new WeeklyUsageWindowEntry
        {
            LimitId = "weekly",
            WindowInstanceId = "window-1",
            CalculatedWindowStartedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            PeakObservedPercent = 74,
            LastObservedPercent = 40,
            ObservedDayCount = 5,
            FirstObservedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            LastObservedAt = new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
            ObservedPlans = ["PRO", "PLUS, family"],
        };
        await using var stream = new MemoryStream();

        await new UsageHistoryCsvExporter().ExportAsync([entry], stream);

        var csv = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("peak_observed_percent", csv, StringComparison.Ordinal);
        Assert.Contains("74", csv, StringComparison.Ordinal);
        Assert.Contains("\"PRO -> PLUS, family\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("token", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", csv, StringComparison.OrdinalIgnoreCase);
    }
}
