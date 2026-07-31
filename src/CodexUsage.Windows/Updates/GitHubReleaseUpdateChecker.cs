using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexUsage.Windows.Updates;

internal enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    NoPublishedRelease,
    Unavailable,
}

internal sealed record AppUpdateCheckResult(
    UpdateCheckStatus Status,
    string Message,
    Uri? ReleaseUri = null);

/// <summary>
/// Checks the public GitHub latest-release endpoint only after an explicit user action.
/// No account, usage, or Codex CLI information is sent with this request.
/// </summary>
internal sealed class GitHubReleaseUpdateChecker
{
    internal const string LatestReleaseEndpoint =
        "https://api.github.com/repos/taeseong/codexusage/releases/latest";

    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateChecker(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CodexUsage", "1.0"));
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-GitHub-Api-Version",
            "2022-11-28");
    }

    public async Task<AppUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                LatestReleaseEndpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new AppUpdateCheckResult(
                    UpdateCheckStatus.NoPublishedRelease,
                    "No published GitHub release was found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new AppUpdateCheckResult(
                    UpdateCheckStatus.Unavailable,
                    "Update check is temporarily unavailable.");
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagElement) ||
                string.IsNullOrWhiteSpace(tagElement.GetString()))
            {
                return new AppUpdateCheckResult(
                    UpdateCheckStatus.Unavailable,
                    "The latest release did not include a version tag.");
            }

            var latestVersionText = tagElement.GetString()!;
            var releaseUri = root.TryGetProperty("html_url", out var urlElement) &&
                             Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var parsedUri)
                ? parsedUri
                : null;
            if (!TryParseVersion(latestVersionText, out var latestVersion) ||
                !TryParseVersion(currentVersion, out var installedVersion))
            {
                return new AppUpdateCheckResult(
                    UpdateCheckStatus.Unavailable,
                    "The release version could not be compared.",
                    releaseUri);
            }

            return latestVersion > installedVersion
                ? new AppUpdateCheckResult(
                    UpdateCheckStatus.UpdateAvailable,
                    $"Version {latestVersion} is available.",
                    releaseUri)
                : new AppUpdateCheckResult(
                    UpdateCheckStatus.UpToDate,
                    "You are using the latest published version.",
                    releaseUri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new AppUpdateCheckResult(
                UpdateCheckStatus.Unavailable,
                "Update check is temporarily unavailable.");
        }
        catch (JsonException)
        {
            return new AppUpdateCheckResult(
                UpdateCheckStatus.Unavailable,
                "The latest release response was not recognized.");
        }
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }
}
