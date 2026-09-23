using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Abstractions;

public interface ITaskRepository
{
    Task<IReadOnlyCollection<TaskItem>> ListAsync(CancellationToken cancellationToken);

    Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(TaskItem taskItem, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAllAsync(IReadOnlyCollection<TaskItem> taskItems, CancellationToken cancellationToken);
}
