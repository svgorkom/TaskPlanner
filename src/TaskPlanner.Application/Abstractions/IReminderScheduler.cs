using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Abstractions;

public interface IReminderScheduler
{
    Task ScheduleAsync(TaskItem taskItem, CancellationToken cancellationToken);

    Task CancelAsync(Guid taskId, CancellationToken cancellationToken);
}
