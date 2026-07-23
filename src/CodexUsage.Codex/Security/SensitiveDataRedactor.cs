using System.Text.RegularExpressions;

namespace CodexUsage.Codex.Security;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var redacted = AuthorizationHeaderRegex().Replace(value, "$1[REDACTED]");
        redacted = JsonTokenRegex().Replace(redacted, "$1[REDACTED]$3");
        return EmailRegex().Replace(redacted, static match =>
        {
            var address = match.Value;
            var at = address.IndexOf('@');
            return at <= 0 ? "[REDACTED_EMAIL]" : $"{address[0]}***{address[at..]}";
        });
    }

    [GeneratedRegex("(?i)(authorization\\s*[:=]\\s*(?:bearer\\s+)?)[^\\s,;]+")]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex("(?i)(\\\"?(?:access_token|refresh_token|id_token)\\\"?\\s*[:=]\\s*\\\"?)([^\\\"\\s,}]+)(\\\"?)")]
    private static partial Regex JsonTokenRegex();

    [GeneratedRegex("(?i)\\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}\\b")]
    private static partial Regex EmailRegex();
}

