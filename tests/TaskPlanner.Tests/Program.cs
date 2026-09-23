using TaskPlanner.Application.Abstractions;
using TaskPlanner.Application.Tasks;
using TaskPlanner.Domain.Tasks;

TestRunner runner = new();

await runner.RunAsync("quick parser extracts priority and due date", QuickParserExtractsPriorityAndDueDateAsync);
await runner.RunAsync("today view returns due active tasks", TodayViewReturnsDueActiveTasksAsync);
await runner.RunAsync("recurring task advances instead of completing", RecurringTaskAdvancesInsteadOfCompletingAsync);
await runner.RunAsync("tasks can be reordered", TasksCanBeReorderedAsync);
await runner.RunAsync("tasks with tied legacy sort order can be reordered", TasksWithTiedSortOrderCanBeReorderedAsync);
await runner.RunAsync("task text link parser extracts browser links", TaskTextLinkParserExtractsBrowserLinksAsync);
await runner.RunAsync("notes can be added edited and removed", NotesCanBeAddedEditedAndRemovedAsync);

runner.ThrowIfFailed();

static Task QuickParserExtractsPriorityAndDueDateAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    QuickTaskParser parser = new(clock);

    TaskDraft draft = parser.Parse("Send report tomorrow !high");

    Assert.Equal("Send report", draft.Title);
    Assert.Equal(TaskPriority.High, draft.Priority);
    Assert.Equal(new DateTimeOffset(2026, 9, 23, 9, 0, 0, TimeSpan.Zero), draft.DueAt);

    return Task.CompletedTask;
}

static async Task TodayViewReturnsDueActiveTasksAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository repository = new();
    NoopReminderScheduler reminderScheduler = new();
    TaskService taskService = new(repository, reminderScheduler, clock);
    TaskQueryService queryService = new(repository, clock);

    await taskService.CreateAsync(new TaskDraft("Inbox task", null, TaskPriority.Normal, null, null, null), CancellationToken.None);
    await taskService.CreateAsync(new TaskDraft("Today task", null, TaskPriority.High, clock.Now.Date.AddHours(12), null, null), CancellationToken.None);
    await taskService.CreateAsync(new TaskDraft("Future task", null, TaskPriority.Low, clock.Now.Date.AddDays(2), null, null), CancellationToken.None);

    IReadOnlyCollection<TaskItem> todayTasks = await queryService.ListAsync(TaskViewKind.Today, CancellationToken.None);

    Assert.Equal(1, todayTasks.Count);
    Assert.Equal("Today task", todayTasks.Single().Title);
}

static async Task RecurringTaskAdvancesInsteadOfCompletingAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository repository = new();
    NoopReminderScheduler reminderScheduler = new();
    TaskService taskService = new(repository, reminderScheduler, clock);

    TaskItem task = await taskService.CreateAsync(
        new TaskDraft("Daily task", null, TaskPriority.Normal, clock.Now.Date.AddHours(9), null, new RecurrenceRule(RecurrenceFrequency.Daily, 1)),
        CancellationToken.None);

    await taskService.CompleteAsync(task.Id, CancellationToken.None);

    TaskItem storedTask = await repository.RequireAsync(task.Id);
    Assert.Equal(PlannerTaskStatus.Active, storedTask.Status);
    Assert.Equal(clock.Now.Date.AddDays(1).AddHours(9), storedTask.DueAt);
}

static async Task TasksCanBeReorderedAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository repository = new();
    TaskService taskService = new(repository, new NoopReminderScheduler(), clock);
    TaskQueryService queryService = new(repository, clock);

    TaskItem first = await taskService.CreateAsync(new TaskDraft("First", null, TaskPriority.Normal, null, null, null), CancellationToken.None);
    TaskItem second = await taskService.CreateAsync(new TaskDraft("Second", null, TaskPriority.Normal, null, null, null), CancellationToken.None);
    TaskItem third = await taskService.CreateAsync(new TaskDraft("Third", null, TaskPriority.Normal, null, null, null), CancellationToken.None);

    await taskService.ReorderAsync([third.Id, first.Id, second.Id], CancellationToken.None);

    IReadOnlyCollection<TaskItem> tasks = await queryService.ListAsync(TaskViewKind.Inbox, CancellationToken.None);
    Assert.Equal("Third", tasks.ElementAt(0).Title);
    Assert.Equal("First", tasks.ElementAt(1).Title);
    Assert.Equal("Second", tasks.ElementAt(2).Title);
}

static async Task TasksWithTiedSortOrderCanBeReorderedAsync()
{
    // Reproduces legacy data saved before sort order existed, where every task
    // defaulted to SortOrder = 0 and only creation order distinguished them.
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository repository = new();
    TaskService taskService = new(repository, new NoopReminderScheduler(), clock);
    TaskQueryService queryService = new(repository, clock);

    TaskItem first = await taskService.CreateAsync(new TaskDraft("First", null, TaskPriority.Normal, null, null, null), CancellationToken.None);
    TaskItem second = await taskService.CreateAsync(new TaskDraft("Second", null, TaskPriority.Normal, null, null, null), CancellationToken.None);
    TaskItem third = await taskService.CreateAsync(new TaskDraft("Third", null, TaskPriority.Normal, null, null, null), CancellationToken.None);

    first.SetSortOrder(0);
    second.SetSortOrder(0);
    third.SetSortOrder(0);
    await repository.SaveAllAsync([first, second, third], CancellationToken.None);

    await taskService.ReorderAsync([third.Id, first.Id, second.Id], CancellationToken.None);

    IReadOnlyCollection<TaskItem> tasks = await queryService.ListAsync(TaskViewKind.Inbox, CancellationToken.None);
    Assert.Equal("Third", tasks.ElementAt(0).Title);
    Assert.Equal("First", tasks.ElementAt(1).Title);
    Assert.Equal("Second", tasks.ElementAt(2).Title);
}

static Task TaskTextLinkParserExtractsBrowserLinksAsync()
{
    IReadOnlyList<TaskTextSegment> segments = TaskTextLinkParser.Parse("Open https://example.com/report.");

    Assert.Equal(3, segments.Count);
    Assert.Equal("Open ", segments[0].Text);
    Assert.Equal(null, segments[0].Uri);
    Assert.Equal("https://example.com/report", segments[1].Text);
    Assert.Equal(new Uri("https://example.com/report"), segments[1].Uri);
    Assert.Equal(".", segments[2].Text);
    Assert.Equal(null, segments[2].Uri);

    return Task.CompletedTask;
}

static async Task NotesCanBeAddedEditedAndRemovedAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository repository = new();
    TaskService taskService = new(repository, new NoopReminderScheduler(), clock);

    TaskItem task = await taskService.CreateAsync(
        new TaskDraft("Task with notes", null, TaskPriority.Normal, null, null, null),
        CancellationToken.None);

    TaskNote firstNote = await taskService.AddNoteAsync(task.Id, "First note", CancellationToken.None);
    await taskService.AddNoteAsync(task.Id, "Second note", CancellationToken.None);

    TaskItem afterAdd = await repository.RequireAsync(task.Id);
    Assert.Equal(2, afterAdd.Notes.Count);

    await taskService.UpdateNoteAsync(task.Id, firstNote.Id, "First note edited", CancellationToken.None);
    TaskItem afterEdit = await repository.RequireAsync(task.Id);
    Assert.Equal("First note edited", afterEdit.Notes.Single(note => note.Id == firstNote.Id).Text);

    await taskService.RemoveNoteAsync(task.Id, firstNote.Id, CancellationToken.None);
    TaskItem afterRemove = await repository.RequireAsync(task.Id);
    Assert.Equal(1, afterRemove.Notes.Count);
    Assert.Equal("Second note", afterRemove.Notes.Single().Text);
}

internal sealed class TestRunner
{
    private int failureCount;

    public async Task RunAsync(string name, Func<Task> test)
    {
        try
        {
            await test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            failureCount++;
            Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        }
    }

    public void ThrowIfFailed()
    {
        if (failureCount > 0)
        {
            throw new InvalidOperationException($"{failureCount} test(s) failed.");
        }
    }
}

internal static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; }
}

internal sealed class MemoryTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, TaskItem> tasks = [];

    public Task<IReadOnlyCollection<TaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<TaskItem>>(tasks.Values.ToArray());
    }

    public Task<TaskItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tasks.TryGetValue(id, out TaskItem? taskItem);

        return Task.FromResult(taskItem);
    }

    public Task SaveAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tasks[taskItem.Id] = taskItem;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tasks.Remove(id);

        return Task.CompletedTask;
    }

    public Task SaveAllAsync(IReadOnlyCollection<TaskItem> taskItems, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tasks.Clear();

        foreach (TaskItem taskItem in taskItems)
        {
            tasks[taskItem.Id] = taskItem;
        }

        return Task.CompletedTask;
    }

    public async Task<TaskItem> RequireAsync(Guid id)
    {
        TaskItem? taskItem = await GetAsync(id, CancellationToken.None);

        if (taskItem is null)
        {
            throw new InvalidOperationException($"Task '{id}' was not found.");
        }

        return taskItem;
    }
}

internal sealed class NoopReminderScheduler : IReminderScheduler
{
    public Task ScheduleAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task CancelAsync(Guid taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
