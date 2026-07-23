using CodexUsage.Codex.Security;

namespace CodexUsage.Codex.Tests;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactsTokensAuthorizationAndMasksEmail()
    {
        const string input = "Authorization: Bearer secret-value access_token=abc123 user@example.com";

        var output = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain("secret-value", output, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", output, StringComparison.Ordinal);
        Assert.DoesNotContain("user@example.com", output, StringComparison.Ordinal);
        Assert.Contains("u***@example.com", output, StringComparison.Ordinal);
    }
}
