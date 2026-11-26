using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Notes;

namespace TaskPlanner.Application.Notes;

public sealed class NoteService
{
    private readonly IClock clock;
    private readonly INoteRepository noteRepository;

    public NoteService(INoteRepository noteRepository, IClock clock)
    {
        this.noteRepository = noteRepository;
        this.clock = clock;
    }

    /// <summary>Lists all notes in tab order.</summary>
    public async Task<IReadOnlyList<NotePage>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<NotePage> notes = await noteRepository.ListAsync(cancellationToken);
        return notes
            .OrderBy(note => note.SortOrder)
            .ThenBy(note => note.CreatedAt)
            .ToArray();
    }

    /// <summary>Creates an empty note as the last tab.</summary>
    public async Task<NotePage> CreateAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<NotePage> notes = await noteRepository.ListAsync(cancellationToken);
        NotePage note = NotePage.Create(clock.Now);
        note.SetSortOrder(notes.Count == 0 ? 0 : notes.Max(existing => existing.SortOrder) + 1);
        await noteRepository.SaveAsync(note, cancellationToken);
        return note;
    }

    public async Task UpdateContentAsync(Guid id, string content, CancellationToken cancellationToken)
    {
        NotePage note = await RequireAsync(id, cancellationToken);
        if (note.Content == content)
        {
            return;
        }

        note.UpdateContent(content, clock.Now);
        await noteRepository.SaveAsync(note, cancellationToken);
    }

    /// <summary>Sets a custom tab title; <c>null</c> or whitespace makes the title follow the first line again.</summary>
    public async Task SetTitleAsync(Guid id, string? title, CancellationToken cancellationToken)
    {
        NotePage note = await RequireAsync(id, cancellationToken);
        note.SetCustomTitle(title, clock.Now);
        await noteRepository.SaveAsync(note, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return noteRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedNoteIds, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<NotePage> notes = await noteRepository.ListAsync(cancellationToken);
        if (orderedNoteIds.Count != notes.Count || orderedNoteIds.Distinct().Count() != notes.Count ||
            orderedNoteIds.Any(id => notes.All(note => note.Id != id)))
        {
            throw new ArgumentException("The note order is invalid.", nameof(orderedNoteIds));
        }

        foreach ((Guid id, int index) in orderedNoteIds.Select((id, index) => (id, index)))
        {
            notes.Single(note => note.Id == id).SetSortOrder(index);
        }

        await noteRepository.SaveAllAsync(notes, cancellationToken);
    }

    private async Task<NotePage> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<NotePage> notes = await noteRepository.ListAsync(cancellationToken);
        return notes.SingleOrDefault(note => note.Id == id)
            ?? throw new InvalidOperationException($"Note '{id}' does not exist.");
    }
}
