using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Abstractions;

public interface ICodexUsageProvider
{
    Task<CodexUsageResult> GetUsageAsync(CancellationToken cancellationToken = default);
}

