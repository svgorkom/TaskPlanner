using TaskPlanner.Domain.Topics;

namespace TaskPlanner.Application.Abstractions;

public interface ITopicRepository
{
    Task<IReadOnlyCollection<TaskTopic>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(TaskTopic topic, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAllAsync(IReadOnlyCollection<TaskTopic> topics, CancellationToken cancellationToken);
}
