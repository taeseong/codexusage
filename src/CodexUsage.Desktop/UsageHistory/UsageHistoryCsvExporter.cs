using System.Globalization;
using System.Text;
using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Desktop.UsageHistory;

/// <summary>Exports local weekly-limit observations only. No account or authentication fields exist in this format.</summary>
public sealed class UsageHistoryCsvExporter
{
    public async Task ExportAsync(
        IEnumerable<WeeklyUsageWindowEntry> entries,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(destination);

        await using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync("limit_id,window_instance_id,window_started_at,initial_scheduled_reset_at,last_scheduled_reset_at,actual_reset_observed_at,closure_kind,peak_observed_percent,last_observed_percent,observed_day_count,first_observed_at,last_observed_at,observed_plans");
        foreach (var entry in entries.OrderByDescending(entry => entry.FirstObservedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new[]
            {
                entry.LimitId,
                entry.WindowInstanceId,
                ToIso(entry.CalculatedWindowStartedAt),
                ToIso(entry.InitialScheduledResetAt),
                ToIso(entry.LastScheduledResetAt),
                ToIso(entry.ActualResetObservedAt),
                entry.ClosureKind.ToString(),
                entry.PeakObservedPercent.ToString("0.##", CultureInfo.InvariantCulture),
                entry.LastObservedPercent.ToString("0.##", CultureInfo.InvariantCulture),
                entry.ObservedDayCount.ToString(CultureInfo.InvariantCulture),
                ToIso(entry.FirstObservedAt),
                ToIso(entry.LastObservedAt),
                string.Join(" -> ", entry.ObservedPlans),
            };
            await writer.WriteLineAsync(string.Join(',', row.Select(Escape)));
        }
    }

    private static string ToIso(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
