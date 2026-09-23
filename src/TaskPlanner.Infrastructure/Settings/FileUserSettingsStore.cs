using System.Text.Json;
using System.Text.Json.Serialization;
using TaskPlanner.Application.Settings;

namespace TaskPlanner.Infrastructure.Settings;

public sealed class FileUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string filePath;

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
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonSerializerOptions, cancellationToken);
    }
}
