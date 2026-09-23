namespace TaskPlanner.Domain.Tasks;

public sealed class TaskItem
{
    public TaskItem(
        Guid id,
        string title,
        string? description,
        PlannerTaskStatus status,
        TaskPriority priority,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? dueAt,
        DateTimeOffset? reminderAt,
        RecurrenceRule? recurrenceRule,
        DateTimeOffset? completedAt,
        int sortOrder = 0,
        IEnumerable<TaskNote>? notes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The task id cannot be empty.", nameof(id));
        }

        Id = id;
        Title = RequireTitle(title);
        Description = description;
        Status = status;
        Priority = priority;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DueAt = dueAt;
        ReminderAt = reminderAt;
        RecurrenceRule = recurrenceRule;
        CompletedAt = completedAt;
        SortOrder = sortOrder;
        noteList = notes is null ? [] : [.. notes];
    }

    private readonly List<TaskNote> noteList;

    public Guid Id { get; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public PlannerTaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? ReminderAt { get; private set; }

    public RecurrenceRule? RecurrenceRule { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<TaskNote> Notes => noteList;

    public static TaskItem Create(string title, DateTimeOffset createdAt)
    {
        return new TaskItem(
            Guid.NewGuid(),
            title,
            null,
            PlannerTaskStatus.Active,
            TaskPriority.Normal,
            createdAt,
            createdAt,
            null,
            null,
            null,
            null,
            0);
    }

    public void Rename(string title, DateTimeOffset updatedAt)
    {
        Title = RequireTitle(title);
        UpdatedAt = updatedAt;
    }

    public void UpdateDescription(string? description, DateTimeOffset updatedAt)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = updatedAt;
    }

    public void SetPriority(TaskPriority priority, DateTimeOffset updatedAt)
    {
        Priority = priority;
        UpdatedAt = updatedAt;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    public void Reschedule(DateTimeOffset? dueAt, DateTimeOffset updatedAt)
    {
        DueAt = dueAt;

        if (ReminderAt.HasValue && dueAt.HasValue && ReminderAt.Value > dueAt.Value)
        {
            ReminderAt = dueAt;
        }

        UpdatedAt = updatedAt;
    }

    public void SetReminder(DateTimeOffset? reminderAt, DateTimeOffset updatedAt)
    {
        if (reminderAt.HasValue && DueAt.HasValue && reminderAt.Value > DueAt.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(reminderAt), "The reminder cannot be later than the due date.");
        }

        ReminderAt = reminderAt;
        UpdatedAt = updatedAt;
    }

    public void SetRecurrence(RecurrenceRule? recurrenceRule, DateTimeOffset updatedAt)
    {
        RecurrenceRule = recurrenceRule;
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (RecurrenceRule is not null && DueAt.HasValue)
        {
            DueAt = RecurrenceRule.GetNextOccurrence(DueAt.Value);
            ReminderAt = ReminderAt.HasValue ? RecurrenceRule.GetNextOccurrence(ReminderAt.Value) : null;
            UpdatedAt = completedAt;
            return;
        }

        Status = PlannerTaskStatus.Completed;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void Reopen(DateTimeOffset updatedAt)
    {
        Status = PlannerTaskStatus.Active;
        CompletedAt = null;
        UpdatedAt = updatedAt;
    }

    public TaskNote AddNote(string text, DateTimeOffset now)
    {
        TaskNote note = TaskNote.Create(text, now);
        noteList.Add(note);
        UpdatedAt = now;

        return note;
    }

    public void UpdateNote(Guid noteId, string text, DateTimeOffset now)
    {
        RequireNote(noteId).UpdateText(text, now);
        UpdatedAt = now;
    }

    public void RemoveNote(Guid noteId, DateTimeOffset now)
    {
        int removedCount = noteList.RemoveAll(note => note.Id == noteId);

        if (removedCount == 0)
        {
            throw new InvalidOperationException($"Note '{noteId}' does not exist.");
        }

        UpdatedAt = now;
    }

    private TaskNote RequireNote(Guid noteId)
    {
        TaskNote? note = noteList.SingleOrDefault(candidate => candidate.Id == noteId);

        if (note is null)
        {
            throw new InvalidOperationException($"Note '{noteId}' does not exist.");
        }

        return note;
    }

    private static string RequireTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A task title is required.", nameof(title));
        }

        return title.Trim();
    }
}
