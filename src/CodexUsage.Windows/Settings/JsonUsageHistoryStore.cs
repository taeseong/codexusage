using System.Text.Json;
using CodexUsage.Core.Abstractions;
using CodexUsage.Core.UsageHistory;

namespace CodexUsage.Windows.Settings;

internal sealed class JsonUsageHistoryStore : IUsageHistoryStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonUsageHistoryStore()
        : this(GetDefaultPath())
    {
    }

    internal JsonUsageHistoryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    private static string GetDefaultPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("CODEX_USAGE_HISTORY_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsage", "usage-history.json")
            : overridePath;
    }

    public async Task<UsageHistoryState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            var temporaryPath = GetTemporaryPath();
            if (!File.Exists(temporaryPath))
            {
                return new UsageHistoryState();
            }

            try
            {
                var recovered = await ReadAsync(temporaryPath, cancellationToken);
                File.Move(temporaryPath, _path, true);
                return recovered;
            }
            catch (JsonException)
            {
                PreserveCorruptFile(temporaryPath);
                return new UsageHistoryState();
            }
        }

        try
        {
            return await ReadAsync(_path, cancellationToken);
        }
        catch (JsonException)
        {
            PreserveCorruptFile(_path);
            return new UsageHistoryState();
        }
    }

    public async Task SaveAsync(UsageHistoryState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new IOException("History path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = GetTemporaryPath();
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_path)) File.Delete(_path);
        var temporaryPath = GetTemporaryPath();
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        return Task.CompletedTask;
    }

    private async Task<UsageHistoryState> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync<UsageHistoryState>(stream, Options, cancellationToken);
        return state is { Version: UsageHistoryState.CurrentVersion }
            ? state with { Windows = (state.Windows ?? []).Where(static entry => entry is not null).ToArray() }
            : new UsageHistoryState();
    }

    private string GetTemporaryPath() => _path + ".tmp";

    private static void PreserveCorruptFile(string path)
    {
        var corruptPath = path + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try { File.Move(path, corruptPath, true); } catch (IOException) { }
    }
}
