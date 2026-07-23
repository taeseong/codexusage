using CodexUsage.Codex;
using CodexUsage.Codex.Discovery;
using CodexUsage.Core.Usage;

var locator = new CodexExecutableLocator();
var codexPath = locator.Find();
Console.WriteLine($"Codex installation: {(codexPath is null ? "Not found" : "Found")}");
if (codexPath is not null)
{
    Console.WriteLine($"Codex location: {codexPath}");
}

var result = await new LiveCodexUsageProvider(locator).GetUsageAsync();
if (!result.IsSuccess || result.Snapshot is null)
{
    Console.WriteLine($"Authentication: {AuthenticationText(result.Status)}");
    Console.WriteLine($"Usage provider: {result.Status}");
    Console.WriteLine($"Detail: {result.Detail}");
    return;
}

var snapshot = result.Snapshot;
Console.WriteLine("Authentication: Signed in");
Console.WriteLine("Provider: Live");
PrintLimit("Short-term", snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.ShortTerm));
PrintLimit("Weekly", snapshot.Limits.FirstOrDefault(limit => limit.Kind is UsageLimitKind.Weekly));
foreach (var unknown in snapshot.Limits.Where(limit => limit.Kind is UsageLimitKind.Unknown))
{
    PrintLimit($"Unknown limit ({unknown.Id})", unknown);
}

Console.WriteLine($"Account plan: {snapshot.AccountPlan ?? "Unavailable"}");
if (snapshot.HasPlanMismatch)
{
    Console.WriteLine($"Rate-limit plan: {snapshot.RateLimitPlan} (differs from account plan)");
}

Console.WriteLine($"Retrieved at: {FormatTimestamp(snapshot.RetrievedAt)}");

static void PrintLimit(string label, UsageLimit? limit)
{
    if (limit is null)
    {
        Console.WriteLine($"{label} limit: Unavailable in response");
        Console.WriteLine($"{label} reset: Unavailable in response");
        return;
    }

    Console.WriteLine($"{label} limit: {limit.UsedPercent:0.##}% used ({limit.RemainingPercent:0.##}% remaining)");
    Console.WriteLine($"{label} reset: {(limit.ResetsAt is null ? "Unavailable in response" : FormatTimestamp(limit.ResetsAt.Value))}");
}

static string FormatTimestamp(DateTimeOffset value) => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");

static string AuthenticationText(CodexUsageStatus status) => status switch
{
    CodexUsageStatus.NotAuthenticated => "Signed out",
    CodexUsageStatus.AuthenticationExpired => "Expired",
    CodexUsageStatus.CodexNotInstalled => "Unknown",
    _ => "Signed in or unavailable",
};

