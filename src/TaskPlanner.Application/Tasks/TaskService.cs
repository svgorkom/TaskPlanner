using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Tasks;

public sealed class TaskService
{
    private readonly IClock clock;
    private readonly IReminderScheduler reminderScheduler;
    private readonly ITaskRepository taskRepository;

    public TaskService(ITaskRepository taskRepository, IReminderScheduler reminderScheduler, IClock clock)
    {
        this.taskRepository = taskRepository;
        this.reminderScheduler = reminderScheduler;
        this.clock = clock;
    }

    public async Task<TaskItem> CreateAsync(TaskDraft draft, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;
        TaskItem taskItem = TaskItem.Create(draft.Title, now);
        IReadOnlyCollection<TaskItem> existingTasks = await taskRepository.ListAsync(cancellationToken);
        taskItem.SetSortOrder(existingTasks.Count == 0 ? 0 : existingTasks.Max(task => task.SortOrder) + 1);
        Apply(taskItem, draft, now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
        await ScheduleReminderAsync(taskItem, cancellationToken);

        return taskItem;
    }

    public async Task UpdateAsync(Guid id, TaskUpdate update, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(id, cancellationToken);
        DateTimeOffset now = clock.Now;

        taskItem.Rename(update.Title, now);
        taskItem.UpdateDescription(update.Description, now);
        taskItem.SetPriority(update.Priority, now);
        taskItem.Reschedule(update.DueAt, now);
        taskItem.SetReminder(update.ReminderAt, now);
        taskItem.SetRecurrence(update.RecurrenceRule, now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
        await ScheduleReminderAsync(taskItem, cancellationToken);
    }

    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(id, cancellationToken);
        taskItem.Complete(clock.Now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
        await ScheduleReminderAsync(taskItem, cancellationToken);
    }

    public async Task ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(id, cancellationToken);
        taskItem.Reopen(clock.Now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
        await ScheduleReminderAsync(taskItem, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await taskRepository.DeleteAsync(id, cancellationToken);
        await reminderScheduler.CancelAsync(id, cancellationToken);
    }

    public async Task<TaskNote> AddNoteAsync(Guid taskId, string text, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(taskId, cancellationToken);
        TaskNote note = taskItem.AddNote(text, clock.Now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);

        return note;
    }

    public async Task UpdateNoteAsync(Guid taskId, Guid noteId, string text, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(taskId, cancellationToken);
        taskItem.UpdateNote(noteId, text, clock.Now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
    }

    public async Task RemoveNoteAsync(Guid taskId, Guid noteId, CancellationToken cancellationToken)
    {
        TaskItem taskItem = await RequireTaskAsync(taskId, cancellationToken);
        taskItem.RemoveNote(noteId, clock.Now);

        await taskRepository.SaveAsync(taskItem, cancellationToken);
    }

    public async Task ReorderAsync(
        IReadOnlyList<Guid> orderedVisibleTaskIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskItem> storedTasks = await taskRepository.ListAsync(cancellationToken);
        List<TaskItem> allTasks = storedTasks
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToList();
        HashSet<Guid> visibleIds = orderedVisibleTaskIds.ToHashSet();

        if (visibleIds.Count != orderedVisibleTaskIds.Count ||
            visibleIds.Count == 0 ||
            visibleIds.Any(id => allTasks.All(task => task.Id != id)))
        {
            throw new ArgumentException("The visible task order is invalid.", nameof(orderedVisibleTaskIds));
        }

        List<TaskItem> visibleTasksInNewOrder = orderedVisibleTaskIds
            .Select(id => allTasks.Single(task => task.Id == id))
            .ToList();
        Queue<TaskItem> visibleQueue = new(visibleTasksInNewOrder);

        List<TaskItem> reorderedTasks = allTasks
            .Select(task => visibleIds.Contains(task.Id) ? visibleQueue.Dequeue() : task)
            .ToList();

        for (int index = 0; index < reorderedTasks.Count; index++)
        {
            reorderedTasks[index].SetSortOrder(index);
        }

        await taskRepository.SaveAllAsync(reorderedTasks, cancellationToken);
    }

    private static void Apply(TaskItem taskItem, TaskDraft draft, DateTimeOffset now)
    {
        taskItem.UpdateDescription(draft.Description, now);
        taskItem.SetPriority(draft.Priority, now);
        taskItem.Reschedule(draft.DueAt, now);
        taskItem.SetReminder(draft.ReminderAt, now);
        taskItem.SetRecurrence(draft.RecurrenceRule, now);
    }

    private async Task<TaskItem> RequireTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        TaskItem? taskItem = await taskRepository.GetAsync(id, cancellationToken);

        if (taskItem is null)
        {
            throw new InvalidOperationException($"Task '{id}' does not exist.");
        }

        return taskItem;
    }

    private async Task ScheduleReminderAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        if (taskItem.Status == PlannerTaskStatus.Completed || !taskItem.ReminderAt.HasValue)
        {
            await reminderScheduler.CancelAsync(taskItem.Id, cancellationToken);
            return;
        }

        await reminderScheduler.ScheduleAsync(taskItem, cancellationToken);
    }
}
