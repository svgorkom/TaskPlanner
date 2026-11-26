using TaskPlanner.Application.Abstractions;
using TaskPlanner.Application.Pomodoro;
using TaskPlanner.Application.Settings;
using TaskPlanner.Application.Tasks;
using TaskPlanner.Application.Topics;
using TaskPlanner.Domain.Tasks;
using TaskPlanner.Domain.Topics;

TestRunner runner = new();

await runner.RunAsync("quick parser extracts priority and due date", QuickParserExtractsPriorityAndDueDateAsync);
await runner.RunAsync("today view returns due active tasks", TodayViewReturnsDueActiveTasksAsync);
await runner.RunAsync("recurring task advances instead of completing", RecurringTaskAdvancesInsteadOfCompletingAsync);
await runner.RunAsync("tasks can be reordered", TasksCanBeReorderedAsync);
await runner.RunAsync("tasks with tied legacy sort order can be reordered", TasksWithTiedSortOrderCanBeReorderedAsync);
await runner.RunAsync("task text link parser extracts browser links", TaskTextLinkParserExtractsBrowserLinksAsync);
await runner.RunAsync("notes can be added edited and removed", NotesCanBeAddedEditedAndRemovedAsync);
await runner.RunAsync("tasks can be assigned and filtered by one topic", TasksCanBeAssignedAndFilteredByOneTopicAsync);
await runner.RunAsync("topics can be renamed", TopicsCanBeRenamedAsync);
await runner.RunAsync("renaming a topic rejects duplicate names", RenamingATopicRejectsDuplicateNamesAsync);
await runner.RunAsync("deleting a topic clears assignments", DeletingATopicClearsAssignmentsAsync);
await runner.RunAsync("pomodoro focus completes into short break", PomodoroFocusCompletesIntoShortBreakAsync);
await runner.RunAsync("pomodoro long break follows configured session count", PomodoroLongBreakFollowsSessionCountAsync);
await runner.RunAsync("pomodoro pause freezes remaining time", PomodoroPauseFreezesRemainingTimeAsync);
await runner.RunAsync("pomodoro skip does not count focus session", PomodoroSkipDoesNotCountFocusAsync);
await runner.RunAsync("pomodoro settings apply to ready phase only", PomodoroSettingsApplyToReadyPhaseOnlyAsync);
await runner.RunAsync("pomodoro settings are clamped", PomodoroSettingsAreClampedAsync);

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

static async Task TasksCanBeAssignedAndFilteredByOneTopicAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository taskRepository = new();
    MemoryTopicRepository topicRepository = new();
    TopicService topicService = new(topicRepository, taskRepository, clock);
    TaskService taskService = new(taskRepository, new NoopReminderScheduler(), clock);
    TaskQueryService queryService = new(taskRepository, clock);
    TaskTopic topic = await topicService.CreateAsync("Work", CancellationToken.None);

    TaskItem assigned = await taskService.CreateAsync(
        new TaskDraft("Assigned", null, TaskPriority.Normal, null, null, null, topic.Id),
        CancellationToken.None);
    await taskService.CreateAsync(
        new TaskDraft("Unassigned", null, TaskPriority.Normal, null, null, null),
        CancellationToken.None);

    IReadOnlyCollection<TaskItem> topicTasks = await queryService.ListAsync(TaskViewKind.Inbox, topic.Id, false, CancellationToken.None);
    IReadOnlyCollection<TaskItem> unassignedTasks = await queryService.ListAsync(TaskViewKind.Inbox, null, true, CancellationToken.None);

    Assert.Equal(1, topicTasks.Count);
    Assert.Equal(assigned.Id, topicTasks.Single().Id);
    Assert.Equal(1, unassignedTasks.Count);
    Assert.Equal("Unassigned", unassignedTasks.Single().Title);
}

static async Task DeletingATopicClearsAssignmentsAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTaskRepository taskRepository = new();
    MemoryTopicRepository topicRepository = new();
    TopicService topicService = new(topicRepository, taskRepository, clock);
    TaskService taskService = new(taskRepository, new NoopReminderScheduler(), clock);
    TaskTopic topic = await topicService.CreateAsync("Work", CancellationToken.None);
    TaskItem task = await taskService.CreateAsync(
        new TaskDraft("Assigned", null, TaskPriority.Normal, null, null, null, topic.Id),
        CancellationToken.None);

    await topicService.DeleteAsync(topic.Id, CancellationToken.None);

    TaskItem storedTask = await taskRepository.RequireAsync(task.Id);
    Assert.Equal(null, storedTask.TopicId);
    Assert.Equal(0, (await topicRepository.ListAsync(CancellationToken.None)).Count);
}

static async Task TopicsCanBeRenamedAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTopicRepository topicRepository = new();
    TopicService topicService = new(topicRepository, new MemoryTaskRepository(), clock);
    TaskTopic topic = await topicService.CreateAsync("Work", CancellationToken.None);

    await topicService.RenameAsync(topic.Id, "Personal", CancellationToken.None);

    TaskTopic renamedTopic = (await topicRepository.ListAsync(CancellationToken.None)).Single();
    Assert.Equal("Personal", renamedTopic.Name);
}

static async Task RenamingATopicRejectsDuplicateNamesAsync()
{
    FixedClock clock = new(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero));
    MemoryTopicRepository topicRepository = new();
    TopicService topicService = new(topicRepository, new MemoryTaskRepository(), clock);
    TaskTopic firstTopic = await topicService.CreateAsync("Work", CancellationToken.None);
    TaskTopic secondTopic = await topicService.CreateAsync("Personal", CancellationToken.None);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => topicService.RenameAsync(secondTopic.Id, firstTopic.Name, CancellationToken.None));
}

static Task PomodoroFocusCompletesIntoShortBreakAsync()
{
    MutableClock clock = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
    PomodoroTimer timer = new(clock, new PomodoroSettings());

    timer.Start();
    clock.Advance(TimeSpan.FromMinutes(24));
    Assert.Equal<PomodoroPhaseCompleted?>(null, timer.Update());
    Assert.Equal(TimeSpan.FromMinutes(1), timer.Remaining);

    clock.Advance(TimeSpan.FromMinutes(1));
    PomodoroPhaseCompleted? completed = timer.Update();

    Assert.Equal(new PomodoroPhaseCompleted(PomodoroPhase.Focus, PomodoroPhase.ShortBreak, 1), completed);
    Assert.Equal(PomodoroStatus.Ready, timer.Status);
    Assert.Equal(TimeSpan.FromMinutes(5), timer.Remaining);
    Assert.Equal<PomodoroPhaseCompleted?>(null, timer.Update());

    return Task.CompletedTask;
}

static Task PomodoroLongBreakFollowsSessionCountAsync()
{
    MutableClock clock = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
    PomodoroTimer timer = new(clock, new PomodoroSettings { FocusMinutes = 10, ShortBreakMinutes = 2, LongBreakMinutes = 20, SessionsBeforeLongBreak = 2 });

    PomodoroPhase RunPhase()
    {
        timer.Start();
        clock.Advance(timer.Remaining);
        return timer.Update()!.NextPhase;
    }

    Assert.Equal(PomodoroPhase.ShortBreak, RunPhase());
    Assert.Equal(PomodoroPhase.Focus, RunPhase());
    Assert.Equal(PomodoroPhase.LongBreak, RunPhase());
    Assert.Equal(TimeSpan.FromMinutes(20), timer.Remaining);
    Assert.Equal(PomodoroPhase.Focus, RunPhase());
    Assert.Equal(2, timer.CompletedFocusSessions);

    return Task.CompletedTask;
}

static Task PomodoroPauseFreezesRemainingTimeAsync()
{
    MutableClock clock = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
    PomodoroTimer timer = new(clock, new PomodoroSettings());

    timer.Start();
    clock.Advance(TimeSpan.FromMinutes(10));
    timer.Pause();
    clock.Advance(TimeSpan.FromHours(2));

    Assert.Equal(PomodoroStatus.Paused, timer.Status);
    Assert.Equal(TimeSpan.FromMinutes(15), timer.Remaining);
    Assert.Equal<PomodoroPhaseCompleted?>(null, timer.Update());

    timer.Start();
    clock.Advance(TimeSpan.FromMinutes(5));
    Assert.Equal(TimeSpan.FromMinutes(10), timer.Remaining);

    timer.Reset();
    Assert.Equal(PomodoroStatus.Ready, timer.Status);
    Assert.Equal(TimeSpan.FromMinutes(25), timer.Remaining);

    return Task.CompletedTask;
}

static Task PomodoroSkipDoesNotCountFocusAsync()
{
    MutableClock clock = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
    PomodoroTimer timer = new(clock, new PomodoroSettings { SessionsBeforeLongBreak = 1 });

    timer.Start();
    timer.Skip();

    Assert.Equal(PomodoroPhase.ShortBreak, timer.Phase);
    Assert.Equal(0, timer.CompletedFocusSessions);

    timer.Skip();
    Assert.Equal(PomodoroPhase.Focus, timer.Phase);

    return Task.CompletedTask;
}

static Task PomodoroSettingsApplyToReadyPhaseOnlyAsync()
{
    MutableClock clock = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero));
    PomodoroTimer timer = new(clock, new PomodoroSettings());

    timer.UpdateSettings(new PomodoroSettings { FocusMinutes = 50 });
    Assert.Equal(TimeSpan.FromMinutes(50), timer.Remaining);

    timer.Start();
    clock.Advance(TimeSpan.FromMinutes(10));
    timer.UpdateSettings(new PomodoroSettings { FocusMinutes = 30 });
    Assert.Equal(TimeSpan.FromMinutes(40), timer.Remaining);

    return Task.CompletedTask;
}

static Task PomodoroSettingsAreClampedAsync()
{
    PomodoroSettings normalized = new PomodoroSettings
    {
        FocusMinutes = 0,
        ShortBreakMinutes = -5,
        LongBreakMinutes = 999,
        SessionsBeforeLongBreak = 0
    }.Normalize();

    Assert.Equal(PomodoroSettings.MinMinutes, normalized.FocusMinutes);
    Assert.Equal(PomodoroSettings.MinMinutes, normalized.ShortBreakMinutes);
    Assert.Equal(PomodoroSettings.MaxMinutes, normalized.LongBreakMinutes);
    Assert.Equal(PomodoroSettings.MinSessionsBeforeLongBreak, normalized.SessionsBeforeLongBreak);

    return Task.CompletedTask;
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

    public static async Task ThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
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

internal sealed class MutableClock : IClock
{
    public MutableClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; private set; }

    public void Advance(TimeSpan duration)
    {
        Now += duration;
    }
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

internal sealed class MemoryTopicRepository : ITopicRepository
{
    private readonly Dictionary<Guid, TaskTopic> topics = [];

    public Task<IReadOnlyCollection<TaskTopic>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<TaskTopic>>(topics.Values.ToArray());
    }

    public Task SaveAsync(TaskTopic topic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        topics[topic.Id] = topic;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        topics.Remove(id);
        return Task.CompletedTask;
    }

    public Task SaveAllAsync(IReadOnlyCollection<TaskTopic> topicItems, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        topics.Clear();
        foreach (TaskTopic topic in topicItems)
        {
            topics[topic.Id] = topic;
        }

        return Task.CompletedTask;
    }
}
