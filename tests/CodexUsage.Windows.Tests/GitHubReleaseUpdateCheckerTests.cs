using System.Net;
using System.Net.Http;
using System.Text;
using CodexUsage.Windows.Updates;

namespace CodexUsage.Windows.Tests;

public sealed class GitHubReleaseUpdateCheckerTests
{
    [Fact]
    public async Task CheckAsync_ReturnsUpdateAvailableForNewerPublishedTag()
    {
        var checker = CreateChecker(HttpStatusCode.OK, """
            {"tag_name":"v0.2.0","html_url":"https://github.com/taeseong/codexusage/releases/tag/v0.2.0"}
            """);

        var result = await checker.CheckAsync("0.1.4+local");

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("Version 0.2.0 is available.", result.Message);
        Assert.Equal(
            "https://github.com/taeseong/codexusage/releases/tag/v0.2.0",
            result.ReleaseUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUpToDateForEqualPublishedTag()
    {
        var checker = CreateChecker(HttpStatusCode.OK, """
            {"tag_name":"v0.1.4","html_url":"https://github.com/taeseong/codexusage/releases/tag/v0.1.4"}
            """);

        var result = await checker.CheckAsync("0.1.4");

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckAsync_MapsNoReleaseAndNetworkFailureToSafeStates()
    {
        var noRelease = CreateChecker(HttpStatusCode.NotFound, "{}");
        var noReleaseResult = await noRelease.CheckAsync("0.1.4");

        Assert.Equal(UpdateCheckStatus.NoPublishedRelease, noReleaseResult.Status);

        var unavailable = new GitHubReleaseUpdateChecker(new HttpClient(
            new ThrowingHandler())
        { BaseAddress = new Uri("https://api.github.com/") });
        var unavailableResult = await unavailable.CheckAsync("0.1.4");

        Assert.Equal(UpdateCheckStatus.Unavailable, unavailableResult.Status);
        Assert.Null(unavailableResult.ReleaseUri);
    }

    private static GitHubReleaseUpdateChecker CreateChecker(HttpStatusCode statusCode, string body) =>
        new(new HttpClient(new StaticResponseHandler(statusCode, body)));

    private sealed class StaticResponseHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Network unavailable");
    }
}
