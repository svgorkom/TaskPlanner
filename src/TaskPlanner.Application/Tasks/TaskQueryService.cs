using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Tasks;

public sealed class TaskQueryService
{
    private readonly IClock clock;
    private readonly ITaskRepository taskRepository;

    public TaskQueryService(ITaskRepository taskRepository, IClock clock)
    {
        this.taskRepository = taskRepository;
        this.clock = clock;
    }

    public async Task<IReadOnlyCollection<TaskItem>> ListAsync(
        TaskViewKind viewKind,
        Guid? topicId,
        bool noTopic,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskItem> tasks = await taskRepository.ListAsync(cancellationToken);
        DateTimeOffset now = clock.Now;
        DateTimeOffset today = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        DateTimeOffset tomorrow = today.AddDays(1);

        IEnumerable<TaskItem> query = viewKind switch
        {
            TaskViewKind.Inbox => tasks.Where(task => task.Status == PlannerTaskStatus.Active && !task.DueAt.HasValue),
            TaskViewKind.Today => tasks.Where(task => task.Status == PlannerTaskStatus.Active && task.DueAt.HasValue && task.DueAt.Value < tomorrow),
            TaskViewKind.Upcoming => tasks.Where(task => task.Status == PlannerTaskStatus.Active && task.DueAt.HasValue && task.DueAt.Value >= tomorrow),
            TaskViewKind.Completed => tasks.Where(task => task.Status == PlannerTaskStatus.Completed),
            TaskViewKind.Active => tasks.Where(task => task.Status == PlannerTaskStatus.Active),
            _ => throw new InvalidOperationException($"Unsupported task view: {viewKind}.")
        };

        query = noTopic ? query.Where(task => !task.TopicId.HasValue) :
            topicId.HasValue ? query.Where(task => task.TopicId == topicId) : query;

        return query
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToArray();
    }

    public Task<IReadOnlyCollection<TaskItem>> ListAsync(TaskViewKind viewKind, CancellationToken cancellationToken)
    {
        return ListAsync(viewKind, null, false, cancellationToken);
    }
}
