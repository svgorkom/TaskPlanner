using TaskPlanner.Domain.Notes;

namespace TaskPlanner.Application.Abstractions;

public interface INoteRepository
{
    Task<IReadOnlyCollection<NotePage>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(NotePage note, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAllAsync(IReadOnlyCollection<NotePage> notes, CancellationToken cancellationToken);
}
