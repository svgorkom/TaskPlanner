using System.Text.Json;
using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Topics;

namespace TaskPlanner.Infrastructure.Persistence;

public sealed class FileTopicRepository : ITopicRepository
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileTopicRepository(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<IReadOnlyCollection<TaskTopic>> ListAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(TaskTopic topic, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<TaskTopic> topics = [.. await LoadAsync(cancellationToken)];
            int index = topics.FindIndex(existingTopic => existingTopic.Id == topic.Id);
            if (index >= 0)
            {
                topics[index] = topic;
            }
            else
            {
                topics.Add(topic);
            }

            await SaveAllCoreAsync(topics, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<TaskTopic> topics = [.. await LoadAsync(cancellationToken)];
            topics.RemoveAll(topic => topic.Id == id);
            await SaveAllCoreAsync(topics, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAllAsync(IReadOnlyCollection<TaskTopic> topics, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await SaveAllCoreAsync(topics, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyCollection<TaskTopic>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        TopicDto[]? topics = await JsonSerializer.DeserializeAsync<TopicDto[]>(stream, JsonSerializerOptions, cancellationToken);
        return topics?.Select(ToDomain).ToArray() ?? [];
    }

    private async Task SaveAllCoreAsync(IReadOnlyCollection<TaskTopic> topics, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, topics.Select(ToDto).ToArray(), JsonSerializerOptions, cancellationToken);
    }

    private static TaskTopic ToDomain(TopicDto dto)
    {
        return new TaskTopic(dto.Id, dto.Name, dto.CreatedAt, dto.UpdatedAt, dto.SortOrder ?? 0);
    }

    private static TopicDto ToDto(TaskTopic topic)
    {
        return new TopicDto
        {
            Id = topic.Id,
            Name = topic.Name,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt,
            SortOrder = topic.SortOrder
        };
    }

    private sealed class TopicDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public int? SortOrder { get; init; }
    }
}
