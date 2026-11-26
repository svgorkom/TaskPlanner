using System.Text.Json;
using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Notes;

namespace TaskPlanner.Infrastructure.Persistence;

public sealed class FileNoteRepository : INoteRepository
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileNoteRepository(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<IReadOnlyCollection<NotePage>> ListAsync(CancellationToken cancellationToken)
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

    public async Task SaveAsync(NotePage note, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            List<NotePage> notes = [.. await LoadAsync(cancellationToken)];
            int index = notes.FindIndex(existing => existing.Id == note.Id);
            if (index >= 0)
            {
                notes[index] = note;
            }
            else
            {
                notes.Add(note);
            }

            await SaveAllCoreAsync(notes, cancellationToken);
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
            List<NotePage> notes = [.. await LoadAsync(cancellationToken)];
            if (notes.RemoveAll(note => note.Id == id) > 0)
            {
                await SaveAllCoreAsync(notes, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAllAsync(IReadOnlyCollection<NotePage> notes, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await SaveAllCoreAsync(notes, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyCollection<NotePage>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        NoteDto[]? notes = await JsonSerializer.DeserializeAsync<NoteDto[]>(stream, JsonSerializerOptions, cancellationToken);
        return notes?.Select(ToDomain).ToArray() ?? [];
    }

    private Task SaveAllCoreAsync(IReadOnlyCollection<NotePage> notes, CancellationToken cancellationToken)
    {
        NoteDto[] dtos = notes.Select(ToDto).ToArray();
        return AtomicFile.WriteAsync(
            filePath,
            (stream, token) => JsonSerializer.SerializeAsync(stream, dtos, JsonSerializerOptions, token),
            cancellationToken);
    }

    private static NotePage ToDomain(NoteDto dto)
    {
        return new NotePage(dto.Id, dto.CustomTitle, dto.Content ?? string.Empty, dto.CreatedAt, dto.UpdatedAt, dto.SortOrder ?? 0);
    }

    private static NoteDto ToDto(NotePage note)
    {
        return new NoteDto
        {
            Id = note.Id,
            CustomTitle = note.CustomTitle,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            SortOrder = note.SortOrder
        };
    }

    private sealed class NoteDto
    {
        public Guid Id { get; init; }
        public string? CustomTitle { get; init; }
        public string? Content { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public int? SortOrder { get; init; }
    }
}
