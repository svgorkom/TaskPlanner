using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Infrastructure.Scheduling;

public sealed class InMemoryReminderScheduler : IReminderScheduler
{
    private readonly Dictionary<Guid, DateTimeOffset> reminders = [];

    public IReadOnlyDictionary<Guid, DateTimeOffset> Reminders => reminders;

    public Task ScheduleAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (taskItem.ReminderAt.HasValue)
        {
            reminders[taskItem.Id] = taskItem.ReminderAt.Value;
        }

        return Task.CompletedTask;
    }

    public Task CancelAsync(Guid taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        reminders.Remove(taskId);

        return Task.CompletedTask;
    }
}
