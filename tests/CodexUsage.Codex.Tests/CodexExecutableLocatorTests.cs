using CodexUsage.Codex.Discovery;

namespace CodexUsage.Codex.Tests;

public sealed class CodexExecutableLocatorTests
{
    [Fact]
    public void Find_UsesKnownApplicationPathWhenFinderPathDoesNotContainCodex()
    {
        // Given
        const string applicationCodex = "/Applications/Codex.app/Contents/Resources/codex";
        var locator = new CodexExecutableLocator(
            () => "/usr/bin:/bin",
            [applicationCodex],
            path => path == applicationCodex);

        // When
        var result = locator.Find();

        // Then
        Assert.Equal(applicationCodex, result);
    }
}
