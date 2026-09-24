using System.Text.Json;
using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Infrastructure.Persistence;

public sealed class FileTaskRepository : ITaskRepository
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileTaskRepository(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<IReadOnlyCollection<TaskItem>> ListAsync(CancellationToken cancellationToken)
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

    public async Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskItem> tasks = await ListAsync(cancellationToken);
        return tasks.SingleOrDefault(task => task.Id == id);
    }

    public async Task SaveAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            List<TaskItem> tasks = [.. await LoadAsync(cancellationToken)];
            int index = tasks.FindIndex(existingTask => existingTask.Id == taskItem.Id);

            if (index >= 0)
            {
                tasks[index] = taskItem;
            }
            else
            {
                tasks.Add(taskItem);
            }

            await SaveAllCoreAsync(tasks, cancellationToken);
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
            List<TaskItem> tasks = [.. await LoadAsync(cancellationToken)];
            tasks.RemoveAll(task => task.Id == id);

            await SaveAllCoreAsync(tasks, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAllAsync(IReadOnlyCollection<TaskItem> taskItems, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);

        try
        {
            await SaveAllCoreAsync(taskItems, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyCollection<TaskItem>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        TaskItemDto[]? taskDtos = await JsonSerializer.DeserializeAsync<TaskItemDto[]>(stream, JsonSerializerOptions, cancellationToken);

        return taskDtos?.Select(ToDomain).ToArray() ?? [];
    }

    private async Task SaveAllCoreAsync(IReadOnlyCollection<TaskItem> tasks, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(filePath);
        TaskItemDto[] taskDtos = tasks.Select(ToDto).ToArray();
        await JsonSerializer.SerializeAsync(stream, taskDtos, JsonSerializerOptions, cancellationToken);
    }

    private static TaskItem ToDomain(TaskItemDto dto)
    {
        RecurrenceRule? recurrenceRule = dto.RecurrenceFrequency.HasValue && dto.RecurrenceInterval.HasValue
            ? new RecurrenceRule(dto.RecurrenceFrequency.Value, dto.RecurrenceInterval.Value)
            : null;

        return new TaskItem(
            dto.Id,
            dto.Title,
            dto.Description,
            dto.Status,
            dto.Priority,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.DueAt,
            dto.ReminderAt,
            recurrenceRule,
            dto.CompletedAt,
            dto.SortOrder ?? 0,
            dto.Notes?.Select(ToDomain),
            dto.TopicId);
    }

    private static TaskNote ToDomain(NoteDto dto)
    {
        return new TaskNote(dto.Id, dto.Text, dto.CreatedAt, dto.UpdatedAt);
    }

    private static TaskItemDto ToDto(TaskItem taskItem)
    {
        return new TaskItemDto
        {
            Id = taskItem.Id,
            Title = taskItem.Title,
            Description = taskItem.Description,
            Status = taskItem.Status,
            Priority = taskItem.Priority,
            CreatedAt = taskItem.CreatedAt,
            UpdatedAt = taskItem.UpdatedAt,
            DueAt = taskItem.DueAt,
            ReminderAt = taskItem.ReminderAt,
            RecurrenceFrequency = taskItem.RecurrenceRule?.Frequency,
            RecurrenceInterval = taskItem.RecurrenceRule?.Interval,
            CompletedAt = taskItem.CompletedAt,
            SortOrder = taskItem.SortOrder,
            TopicId = taskItem.TopicId,
            Notes = taskItem.Notes.Select(ToDto).ToArray()
        };
    }

    private static NoteDto ToDto(TaskNote note)
    {
        return new NoteDto
        {
            Id = note.Id,
            Text = note.Text,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }

    private sealed class TaskItemDto
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string? Description { get; init; }

        public PlannerTaskStatus Status { get; init; }

        public TaskPriority Priority { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public DateTimeOffset? DueAt { get; init; }

        public DateTimeOffset? ReminderAt { get; init; }

        public RecurrenceFrequency? RecurrenceFrequency { get; init; }

        public int? RecurrenceInterval { get; init; }

        public DateTimeOffset? CompletedAt { get; init; }

        public int? SortOrder { get; init; }

        public Guid? TopicId { get; init; }

        public NoteDto[]? Notes { get; init; }
    }

    private sealed class NoteDto
    {
        public Guid Id { get; init; }

        public string Text { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
