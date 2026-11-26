using System.Text.Json;
using System.Text.Json.Serialization;
using TaskPlanner.Application.Settings;
using TaskPlanner.Infrastructure.Persistence;

namespace TaskPlanner.Infrastructure.Settings;

public sealed class FileUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string filePath;

    // Theme, startup and pomodoro changes can each trigger a save; never let two writes overlap.
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileUserSettingsStore(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new UserSettings();
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonSerializerOptions, cancellationToken)
            ?? new UserSettings();
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await AtomicFile.WriteAsync(
                filePath,
                (stream, token) => JsonSerializer.SerializeAsync(stream, settings, JsonSerializerOptions, token),
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
