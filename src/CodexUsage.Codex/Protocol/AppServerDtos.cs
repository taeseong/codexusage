using System.Text.Json.Serialization;

namespace CodexUsage.Codex.Protocol;

internal sealed record AccountReadResponse(
    [property: JsonPropertyName("account")] AccountDto? Account,
    [property: JsonPropertyName("requiresOpenaiAuth")] bool RequiresOpenaiAuth);

internal sealed record AccountDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("planType")] string? PlanType);

internal sealed record RateLimitsReadResponse(
    [property: JsonPropertyName("rateLimits")] RateLimitSnapshotDto? RateLimits,
    [property: JsonPropertyName("rateLimitsByLimitId")] IReadOnlyDictionary<string, RateLimitSnapshotDto>? RateLimitsByLimitId);

internal sealed record RateLimitSnapshotDto(
    [property: JsonPropertyName("limitId")] string? LimitId,
    [property: JsonPropertyName("limitName")] string? LimitName,
    [property: JsonPropertyName("primary")] RateLimitWindowDto? Primary,
    [property: JsonPropertyName("secondary")] RateLimitWindowDto? Secondary,
    [property: JsonPropertyName("planType")] string? PlanType);

internal sealed record RateLimitWindowDto(
    [property: JsonPropertyName("usedPercent")] int? UsedPercent,
    [property: JsonPropertyName("windowDurationMins")] long? WindowDurationMins,
    [property: JsonPropertyName("resetsAt")] long? ResetsAt);

