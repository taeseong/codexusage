using System.Text;
using System.Text.Json;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.Usage;

namespace CodexUsage.Windows.Settings;

internal sealed class JsonUsageSnapshotCache : IUsageSnapshotCache
{
    private const int CurrentVersion = 1;
    private const int MaximumLimitCount = 2;
    private const int MaximumPlanLength = 32;
    private readonly string _path;

    public JsonUsageSnapshotCache()
        : this(GetDefaultPath())
    {
    }

    internal JsonUsageSnapshotCache(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<CodexUsageSnapshot?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            PrepareForRead(_path);
            if (!File.Exists(_path))
            {
                return null;
            }

            CacheDocument? document;
            await using (var stream = new FileStream(
                             _path,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             4096,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                document = await JsonSerializer.DeserializeAsync<CacheDocument>(
                    stream,
                    cancellationToken: cancellationToken);
            }

            if (document is not { Version: CurrentVersion } ||
                MapSnapshot(document.Snapshot) is not { } snapshot)
            {
                PreserveCorrupt(_path);
                return null;
            }

            return snapshot;
        }
        catch (JsonException)
        {
            PreserveCorrupt(_path);
            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        CodexUsageSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new CacheDocument
        {
            Version = CurrentVersion,
            Snapshot = MapSnapshot(snapshot),
        };
        var temporaryPath = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(
                             stream,
                             new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                var json = JsonSerializer.Serialize(document);
                await writer.WriteAsync(json.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static CacheSnapshot MapSnapshot(CodexUsageSnapshot snapshot) =>
        new()
        {
            RetrievedAt = snapshot.RetrievedAt,
            AccountPlan = NormalizePlan(snapshot.AccountPlan),
            RateLimitPlan = NormalizePlan(snapshot.RateLimitPlan),
            Limits = snapshot.Limits
                .Where(static limit =>
                    limit.Kind is UsageLimitKind.ShortTerm or UsageLimitKind.Weekly)
                .GroupBy(static limit => limit.Kind)
                .Select(static group => group.First())
                .OrderBy(static limit => limit.Kind)
                .Take(MaximumLimitCount)
                .Select(static limit => new CacheLimit
                {
                    Kind = limit.Kind,
                    UsedPercent = limit.UsedPercent,
                    WindowDurationSeconds = limit.WindowDuration?.TotalSeconds,
                    ResetsAt = limit.ResetsAt,
                })
                .ToArray(),
        };

    private static CodexUsageSnapshot? MapSnapshot(CacheSnapshot? snapshot)
    {
        if (snapshot is null ||
            snapshot.RetrievedAt == default ||
            snapshot.Limits is null ||
            snapshot.Limits.Count > MaximumLimitCount)
        {
            return null;
        }

        var limits = new List<UsageLimit>(snapshot.Limits.Count);
        var seenKinds = new HashSet<UsageLimitKind>();
        foreach (var limit in snapshot.Limits)
        {
            if (limit.Kind is not (UsageLimitKind.ShortTerm or UsageLimitKind.Weekly) ||
                !seenKinds.Add(limit.Kind) ||
                !double.IsFinite(limit.UsedPercent) ||
                limit.UsedPercent is < 0d or > 100d ||
                limit.WindowDurationSeconds is < 0d ||
                limit.WindowDurationSeconds is { } duration && !double.IsFinite(duration))
            {
                return null;
            }

            limits.Add(new UsageLimit(
                limit.Kind is UsageLimitKind.ShortTerm ? "short-term" : "weekly",
                limit.Kind is UsageLimitKind.ShortTerm ? "5-hour" : "Weekly",
                limit.Kind,
                limit.UsedPercent,
                limit.WindowDurationSeconds is { } seconds
                    ? TimeSpan.FromSeconds(seconds)
                    : null,
                limit.ResetsAt));
        }

        return new CodexUsageSnapshot
        {
            RetrievedAt = snapshot.RetrievedAt,
            AccountPlan = NormalizePlan(snapshot.AccountPlan),
            RateLimitPlan = NormalizePlan(snapshot.RateLimitPlan),
            Limits = limits,
        };
    }

    private static string? NormalizePlan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumPlanLength ||
            value.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not ('-' or '_')))
        {
            return null;
        }

        return value;
    }

    private static void PrepareForRead(string path)
    {
        var temporaryPath = path + ".tmp";
        if (!File.Exists(temporaryPath))
        {
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(temporaryPath);
            return;
        }

        File.Move(temporaryPath, path);
    }

    private static void PreserveCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var suffix = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
            File.Move(path, $"{path}.corrupt-{suffix}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private static string GetDefaultPath()
    {
        var testPath = Environment.GetEnvironmentVariable("CODEX_USAGE_CACHE_PATH");
        return !string.IsNullOrWhiteSpace(testPath)
            ? testPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexUsage",
                "usage-cache.json");
    }

    private sealed record CacheDocument
    {
        public int Version { get; init; }

        public CacheSnapshot? Snapshot { get; init; }
    }

    private sealed record CacheSnapshot
    {
        public DateTimeOffset RetrievedAt { get; init; }

        public string? AccountPlan { get; init; }

        public string? RateLimitPlan { get; init; }

        public IReadOnlyList<CacheLimit> Limits { get; init; } = [];
    }

    private sealed record CacheLimit
    {
        public UsageLimitKind Kind { get; init; }

        public double UsedPercent { get; init; }

        public double? WindowDurationSeconds { get; init; }

        public DateTimeOffset? ResetsAt { get; init; }
    }
}
