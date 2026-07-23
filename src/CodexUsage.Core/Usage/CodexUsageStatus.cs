namespace CodexUsage.Core.Usage;

public enum CodexUsageStatus
{
    Success,
    CodexNotInstalled,
    NotAuthenticated,
    AuthenticationExpired,
    UsageUnsupported,
    NetworkError,
    ProtocolError,
    ResponseFormatChanged,
    TimedOut,
    Cancelled,
    UnknownError,
}

