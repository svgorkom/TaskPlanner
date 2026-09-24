using TaskPlanner.Domain.Tasks;
using TaskPlanner.Application.Tasks;

namespace TaskPlanner.App.ViewModels;

public sealed class TaskListItemViewModel
{
    public TaskListItemViewModel(TaskItem taskItem, int number)
    {
        Number = number;
        Id = taskItem.Id;
        Title = taskItem.Title;
        Description = taskItem.Description;
        Priority = taskItem.Priority;
        DueAt = taskItem.DueAt;
        ReminderAt = taskItem.ReminderAt;
        Status = taskItem.Status;
        RecurrenceRule = taskItem.RecurrenceRule;
        TopicId = taskItem.TopicId;
        Notes = taskItem.Notes;
    }

    public int Number { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public TaskPriority Priority { get; }

    public DateTimeOffset? DueAt { get; }

    public DateTimeOffset? ReminderAt { get; }

    public PlannerTaskStatus Status { get; }

    public RecurrenceRule? RecurrenceRule { get; }

    public IReadOnlyList<TaskNote> Notes { get; }

    public Guid? TopicId { get; }

    public string DisplayTitle => $"{Number}. {Title}";

    public IReadOnlyList<TaskTextSegment> DisplayTitleSegments => TaskTextLinkParser.Parse(DisplayTitle);

    public string Summary
    {
        get
        {
            string due = DueAt.HasValue ? DueAt.Value.ToString("g") : "No due date";
            string recurrence = RecurrenceRule is null ? string.Empty : $" · Repeats {RecurrenceRule.Frequency}";

            return $"{Priority} · {due}{recurrence}";
        }
    }

    public IReadOnlyList<string> NotePreviewLines
    {
        get
        {
            const int MaxNotesShown = 2;
            const int MaxNoteLength = 40;

            List<string> lines = Notes
                .Take(MaxNotesShown)
                .Select(note => note.Text.Length > MaxNoteLength
                    ? $"📝 {note.Text.AsSpan(0, MaxNoteLength)}…"
                    : $"📝 {note.Text}")
                .ToList();

            int remaining = Notes.Count - MaxNotesShown;
            if (remaining > 0)
            {
                lines.Add($"+{remaining} more");
            }

            return lines;
        }
    }
}
