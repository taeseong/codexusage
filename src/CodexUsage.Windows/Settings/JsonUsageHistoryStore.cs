using System.Text.Json;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Windows.Settings;

internal sealed class JsonUsageHistoryStore : IUsageHistoryStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonUsageHistoryStore()
    {
        var overridePath = Environment.GetEnvironmentVariable("CODEX_USAGE_HISTORY_PATH");
        _path = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsage", "usage-history.json")
            : overridePath;
    }

    public async Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new UsageHistoryState();
        try
        {
            await using var stream = File.OpenRead(_path);
            var state = await JsonSerializer.DeserializeAsync<UsageHistoryState>(stream, Options, cancellationToken);
            return state is { Version: UsageHistoryState.CurrentVersion }
                ? state with { Windows = (state.Windows ?? []).Where(static entry => entry is not null).ToArray() }
                : new UsageHistoryState();
        }
        catch (JsonException)
        {
            var corruptPath = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try { File.Move(_path, corruptPath, true); } catch (IOException) { }
            return new UsageHistoryState();
        }
    }

    public async Task SaveAsync(UsageHistoryState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new IOException("History path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, Options, cancellationToken);
        }
        File.Move(temporaryPath, _path, true);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
