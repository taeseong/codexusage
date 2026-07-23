namespace CodexUsage.Codex.Protocol;

internal sealed class AppServerProtocolException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class AppServerMethodNotFoundException(string method)
    : Exception($"Codex App Server does not support {method}.");

internal sealed class AppServerResponseFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class AppServerExitedException(int? exitCode)
    : Exception($"Codex App Server exited before completing the request (exit code: {exitCode?.ToString() ?? "unknown"}).");
