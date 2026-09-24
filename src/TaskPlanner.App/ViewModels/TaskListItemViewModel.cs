using System.Collections.ObjectModel;
using TaskPlanner.Domain.Tasks;
using TaskPlanner.Application.Tasks;

namespace TaskPlanner.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    private readonly IReadOnlyList<TaskNote> notes;
    private bool isExpanded;
    private string editTitle;
    private TopicOption? editTopic;
    private string newNoteText = string.Empty;

    public TaskListItemViewModel(TaskItem taskItem, int number, IEnumerable<TopicOption> topics, bool isExpanded)
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
        notes = taskItem.Notes;
        this.isExpanded = isExpanded;
        editTitle = taskItem.Title;
        editTopic = topics.FirstOrDefault(topic => topic.Id == taskItem.TopicId) ?? topics.FirstOrDefault();

        Notes = new ObservableCollection<NoteViewModel>(
            notes.Select(note => new NoteViewModel(taskItem.Id, note)));
    }

    public int Number { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string? Description { get; }

    // Priority, due date, and recurrence are intentionally not editable from the UI anymore,
    // but the values are preserved here so a save round-trips them unchanged.
    public TaskPriority Priority { get; }

    public DateTimeOffset? DueAt { get; }

    public DateTimeOffset? ReminderAt { get; }

    public PlannerTaskStatus Status { get; }

    public bool IsCompleted => Status == PlannerTaskStatus.Completed;

    public RecurrenceRule? RecurrenceRule { get; }

    public Guid? TopicId { get; }

    public ObservableCollection<NoteViewModel> Notes { get; }

    public string DisplayTitle => $"{Number}. {Title}";

    public IReadOnlyList<TaskTextSegment> DisplayTitleSegments => TaskTextLinkParser.Parse(DisplayTitle);

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (SetProperty(ref isExpanded, value))
            {
                RaisePropertyChanged(nameof(ToggleGlyph));
            }
        }
    }

    public string ToggleGlyph => IsExpanded ? "⌃" : "✎";

    public string EditTitle
    {
        get => editTitle;
        set => SetProperty(ref editTitle, value);
    }

    public TopicOption? EditTopic
    {
        get => editTopic;
        set => SetProperty(ref editTopic, value);
    }

    public string NewNoteText
    {
        get => newNoteText;
        set => SetProperty(ref newNoteText, value);
    }

    public IReadOnlyList<string> NotePreviewLines
    {
        get
        {
            const int MaxNotesShown = 2;
            const int MaxNoteLength = 40;

            List<string> lines = notes
                .Take(MaxNotesShown)
                .Select(note => note.Text.Length > MaxNoteLength
                    ? $"📝 {note.Text.AsSpan(0, MaxNoteLength)}…"
                    : $"📝 {note.Text}")
                .ToList();

            int remaining = notes.Count - MaxNotesShown;
            if (remaining > 0)
            {
                lines.Add($"+{remaining} more");
            }

            return lines;
        }
    }

    public void ResetEdits(IEnumerable<TopicOption> topics)
    {
        EditTitle = Title;
        EditTopic = topics.FirstOrDefault(topic => topic.Id == TopicId) ?? topics.FirstOrDefault();
        NewNoteText = string.Empty;
    }
}
