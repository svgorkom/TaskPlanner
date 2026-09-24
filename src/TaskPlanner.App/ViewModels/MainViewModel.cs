using System.Collections.ObjectModel;
using System.Windows.Input;
using TaskPlanner.Application.Settings;
using TaskPlanner.Application.Tasks;
using TaskPlanner.Application.Topics;
using TaskPlanner.Domain.Tasks;
using TaskPlanner.Domain.Topics;

namespace TaskPlanner.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly QuickTaskParser quickTaskParser;
    private readonly TaskQueryService taskQueryService;
    private readonly TaskService taskService;
    private readonly TopicService topicService;
    private string newTaskText = string.Empty;
    private TaskViewKind selectedView;
    private TaskListItemViewModel? selectedTask;
    private string editTitle = string.Empty;
    private TaskPriority editPriority = TaskPriority.Normal;
    private DateTime? editDueDate;
    private bool editIsRecurring;
    private RecurrenceFrequency editRecurrenceFrequency = RecurrenceFrequency.Daily;
    private bool startWithWindows;
    private ThemeMode selectedThemeMode;
    private bool suppressStartWithWindowsChanged;
    private string newNoteText = string.Empty;
    private string newTopicName = string.Empty;
    private TopicOption? editTopic;
    private TopicOption? topicToManage;
    private readonly Dictionary<Guid, bool> topicExpansionStates = [];
    private bool noTopicExpanded = true;

    public MainViewModel(
        TaskService taskService,
        TaskQueryService taskQueryService,
        QuickTaskParser quickTaskParser,
        TopicService topicService,
        ThemeMode selectedThemeMode,
        bool startWithWindows)
    {
        this.taskService = taskService;
        this.taskQueryService = taskQueryService;
        this.quickTaskParser = quickTaskParser;
        this.topicService = topicService;
        this.selectedThemeMode = selectedThemeMode;
        this.startWithWindows = startWithWindows;

        AddTaskCommand = new AsyncRelayCommand(AddTaskAsync, CanAddTask);
        SaveSelectedCommand = new AsyncRelayCommand(SaveSelectedAsync, HasSelectedTask);
        CompleteSelectedCommand = new AsyncRelayCommand(CompleteSelectedAsync, HasSelectedTask);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, HasSelectedTask);
        AddNoteCommand = new AsyncRelayCommand(AddNoteAsync, CanAddNote);
        RemoveNoteCommand = new AsyncRelayCommand<NoteViewModel>(RemoveNoteAsync, note => note is not null);
        CreateTopicCommand = new AsyncRelayCommand(CreateTopicAsync, CanCreateTopic);
        RenameTopicCommand = new AsyncRelayCommand(RenameTopicAsync, CanRenameTopic);
        DeleteTopicCommand = new AsyncRelayCommand(DeleteTopicAsync, HasSelectedTopic);
        ToggleTopicCommand = new RelayCommand<TopicGroupViewModel>(ToggleTopic);
        HideCommand = new RelayCommand(() => HideRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));

        _ = InitializeAsync(CancellationToken.None);
    }

    public event EventHandler? HideRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<ThemeMode>? ThemeModeChanged;

    public event EventHandler<bool>? StartWithWindowsChanged;

    public ObservableCollection<TaskListItemViewModel> Tasks { get; } = [];

    public ObservableCollection<TopicGroupViewModel> TopicGroups { get; } = [];

    public ObservableCollection<NoteViewModel> SelectedTaskNotes { get; } = [];
    public ObservableCollection<TopicOption> Topics { get; } = [];
    public IReadOnlyCollection<TaskViewKind> ViewKinds { get; } =
    [
        TaskViewKind.Inbox,
        TaskViewKind.Today,
        TaskViewKind.Upcoming,
        TaskViewKind.Completed
    ];

    public IReadOnlyCollection<TaskPriority> PriorityValues { get; } =
    [
        TaskPriority.Low,
        TaskPriority.Normal,
        TaskPriority.High,
        TaskPriority.Urgent
    ];

    public IReadOnlyCollection<RecurrenceFrequency> RecurrenceValues { get; } =
    [
        RecurrenceFrequency.Daily,
        RecurrenceFrequency.Weekly,
        RecurrenceFrequency.Monthly
    ];

    public IReadOnlyCollection<ThemeMode> ThemeModes { get; } =
    [
        ThemeMode.System,
        ThemeMode.Light,
        ThemeMode.Dark
    ];

    public string NewTaskText
    {
        get => newTaskText;
        set
        {
            if (SetProperty(ref newTaskText, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public TaskViewKind SelectedView
    {
        get => selectedView;
        set
        {
            if (SetProperty(ref selectedView, value))
            {
                _ = RefreshAsync(CancellationToken.None);
            }
        }
    }

    public TopicOption? EditTopic
    {
        get => editTopic;
        set => SetProperty(ref editTopic, value);
    }

    public TopicOption? TopicToManage
    {
        get => topicToManage;
        set
        {
            if (SetProperty(ref topicToManage, value))
            {
                RaiseTopicCommandStates();
            }
        }
    }

    public string NewTopicName
    {
        get => newTopicName;
        set
        {
            if (SetProperty(ref newTopicName, value))
            {
                RaiseTopicCommandStates();
            }
        }
    }

    public TaskListItemViewModel? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (!SetProperty(ref selectedTask, value))
            {
                return;
            }

            PopulateEditor(value);
            RaiseCommandStates();
        }
    }

    public string EditTitle
    {
        get => editTitle;
        set => SetProperty(ref editTitle, value);
    }

    public TaskPriority EditPriority
    {
        get => editPriority;
        set => SetProperty(ref editPriority, value);
    }

    public DateTime? EditDueDate
    {
        get => editDueDate;
        set => SetProperty(ref editDueDate, value);
    }

    public bool EditIsRecurring
    {
        get => editIsRecurring;
        set => SetProperty(ref editIsRecurring, value);
    }

    public RecurrenceFrequency EditRecurrenceFrequency
    {
        get => editRecurrenceFrequency;
        set => SetProperty(ref editRecurrenceFrequency, value);
    }

    public string NewNoteText
    {
        get => newNoteText;
        set
        {
            if (SetProperty(ref newNoteText, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ThemeMode SelectedThemeMode
    {
        get => selectedThemeMode;
        set
        {
            if (SetProperty(ref selectedThemeMode, value))
            {
                ThemeModeChanged?.Invoke(this, value);
            }
        }
    }

    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (SetProperty(ref startWithWindows, value) && !suppressStartWithWindowsChanged)
            {
                StartWithWindowsChanged?.Invoke(this, value);
            }
        }
    }

    public ICommand AddTaskCommand { get; }

    public ICommand SaveSelectedCommand { get; }

    public ICommand CompleteSelectedCommand { get; }

    public ICommand DeleteSelectedCommand { get; }

    public ICommand AddNoteCommand { get; }

    public ICommand RemoveNoteCommand { get; }
    public ICommand CreateTopicCommand { get; }
    public ICommand RenameTopicCommand { get; }
    public ICommand DeleteTopicCommand { get; }
    public ICommand ToggleTopicCommand { get; }

    public async Task ReorderTasksAsync(Guid draggedTaskId, Guid targetTaskId, CancellationToken cancellationToken)
    {
        if (draggedTaskId == targetTaskId)
        {
            return;
        }

        List<Guid> orderedIds = Tasks.Select(task => task.Id).ToList();
        int draggedIndex = orderedIds.IndexOf(draggedTaskId);
        int originalTargetIndex = orderedIds.IndexOf(targetTaskId);

        if (draggedIndex < 0 || originalTargetIndex < 0)
        {
            return;
        }

        bool movingDown = draggedIndex < originalTargetIndex;

        orderedIds.RemoveAt(draggedIndex);
        int targetIndex = orderedIds.IndexOf(targetTaskId);
        // Moving down must land after the target; otherwise the removal shift
        // would put the dragged task right back where it started.
        orderedIds.Insert(movingDown ? targetIndex + 1 : targetIndex, draggedTaskId);
        await taskService.ReorderAsync(orderedIds, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task ReorderTopicsAsync(Guid draggedTopicId, Guid targetTopicId, CancellationToken cancellationToken)
    {
        if (draggedTopicId == targetTopicId)
        {
            return;
        }

        List<Guid> orderedIds = Topics
            .Where(topic => topic.Id.HasValue)
            .Select(topic => topic.Id!.Value)
            .ToList();
        int draggedIndex = orderedIds.IndexOf(draggedTopicId);
        int originalTargetIndex = orderedIds.IndexOf(targetTopicId);
        if (draggedIndex < 0 || originalTargetIndex < 0)
        {
            return;
        }

        bool movingDown = draggedIndex < originalTargetIndex;
        orderedIds.RemoveAt(draggedIndex);
        int targetIndex = orderedIds.IndexOf(targetTopicId);
        orderedIds.Insert(movingDown ? targetIndex + 1 : targetIndex, draggedTopicId);
        await topicService.ReorderAsync(orderedIds, cancellationToken);
        await RefreshTopicsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public ICommand HideCommand { get; }

    public ICommand ExitCommand { get; }

    public void SetStartWithWindowsSilently(bool value)
    {
        suppressStartWithWindowsChanged = true;

        try
        {
            StartWithWindows = value;
        }
        finally
        {
            suppressStartWithWindowsChanged = false;
        }
    }

    private async Task AddTaskAsync(CancellationToken cancellationToken)
    {
        TaskDraft draft = quickTaskParser.Parse(NewTaskText);
        await taskService.CreateAsync(draft, cancellationToken);
        NewTaskText = string.Empty;
        await RefreshAsync(cancellationToken);
    }

    private async Task SaveSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null)
        {
            return;
        }

        RecurrenceRule? recurrenceRule = EditIsRecurring
            ? new RecurrenceRule(EditRecurrenceFrequency, 1)
            : null;

        DateTimeOffset? dueAt = EditDueDate.HasValue
            ? new DateTimeOffset(EditDueDate.Value.Date.AddHours(9))
            : null;

        TaskUpdate update = new(
            EditTitle,
            SelectedTask.Description,
            EditPriority,
            dueAt,
            SelectedTask.ReminderAt,
            recurrenceRule,
            EditTopic?.Id);

        await taskService.UpdateAsync(SelectedTask.Id, update, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task CompleteSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null)
        {
            return;
        }

        await taskService.CompleteAsync(SelectedTask.Id, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null)
        {
            return;
        }

        await taskService.DeleteAsync(SelectedTask.Id, cancellationToken);
        SelectedTask = null;
        await RefreshAsync(cancellationToken);
    }

    private async Task AddNoteAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null || string.IsNullOrWhiteSpace(NewNoteText))
        {
            return;
        }

        Guid taskId = SelectedTask.Id;
        await taskService.AddNoteAsync(taskId, NewNoteText, cancellationToken);
        NewNoteText = string.Empty;
        await RefreshAsync(cancellationToken);
        SelectedTask = FindTaskById(taskId);
    }

    public async Task UpdateNoteAsync(NoteViewModel note, CancellationToken cancellationToken)
    {
        if (SelectedTask is null || string.IsNullOrWhiteSpace(note.Text))
        {
            return;
        }

        Guid taskId = SelectedTask.Id;
        await taskService.UpdateNoteAsync(taskId, note.Id, note.Text, cancellationToken);
        await RefreshAsync(cancellationToken);
        SelectedTask = FindTaskById(taskId);
    }

    private async Task RemoveNoteAsync(NoteViewModel? note, CancellationToken cancellationToken)
    {
        if (SelectedTask is null || note is null)
        {
            return;
        }

        Guid taskId = SelectedTask.Id;
        await taskService.RemoveNoteAsync(taskId, note.Id, cancellationToken);
        await RefreshAsync(cancellationToken);
        SelectedTask = FindTaskById(taskId);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshGroupedAsync(cancellationToken);
    }

    private async Task RefreshGroupedAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskItem> tasks = await taskQueryService.ListAsync(SelectedView, null, false, cancellationToken);
        Tasks.Clear();
        TopicGroups.Clear();

        int number = 1;
        foreach (TopicOption topic in Topics.Where(option => !option.IsNoTopic))
        {
            Guid topicId = topic.Id
                ?? throw new InvalidOperationException("A user-defined topic must have an id.");
            List<TaskListItemViewModel> topicTasks = tasks
                .Where(task => task.TopicId == topicId)
                .Select(task => new TaskListItemViewModel(task, number++))
                .ToList();
            TopicGroups.Add(new TopicGroupViewModel(
                topicId,
                topic.Name,
                topicTasks,
                topicExpansionStates.GetValueOrDefault(topicId, true)));
        }

        List<TaskListItemViewModel> unassignedTasks = tasks
            .Where(task => !task.TopicId.HasValue)
            .Select(task => new TaskListItemViewModel(task, number++))
            .ToList();
        TopicGroups.Add(new TopicGroupViewModel(
            null,
            "No topic",
            unassignedTasks,
            noTopicExpanded));
    }

    private TaskListItemViewModel? FindTaskById(Guid id)
    {
        return Tasks.SingleOrDefault(task => task.Id == id)
            ?? TopicGroups.SelectMany(group => group.Tasks).SingleOrDefault(task => task.Id == id);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RefreshTopicsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task RefreshTopicsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskTopic> topics = (await topicService.ListAsync(cancellationToken))
            .OrderBy(topic => topic.SortOrder)
            .ThenBy(topic => topic.CreatedAt)
            .ToArray();
        Topics.Clear();
        Topics.Add(new TopicOption(null, "No topic", isNoTopic: true));
        foreach (TaskTopic topic in topics)
        {
            Topics.Add(TopicOption.FromTopic(topic));
        }

        TopicToManage = Topics.FirstOrDefault(option => option.Id == TopicToManage?.Id)
            ?? Topics.FirstOrDefault(option => !option.IsNoTopic);

        if (SelectedTask is not null)
        {
            EditTopic = Topics.FirstOrDefault(option => option.Id == SelectedTask.TopicId);
        }
    }

    private void ToggleTopic(TopicGroupViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
        if (group.TopicId is Guid topicId)
        {
            topicExpansionStates[topicId] = group.IsExpanded;
        }
        else
        {
            noTopicExpanded = group.IsExpanded;
        }
    }

    private async Task CreateTopicAsync(CancellationToken cancellationToken)
    {
        await topicService.CreateAsync(NewTopicName, cancellationToken);
        NewTopicName = string.Empty;
        await RefreshTopicsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task RenameTopicAsync(CancellationToken cancellationToken)
    {
        if (TopicToManage?.Id is not Guid topicId)
        {
            return;
        }

        await topicService.RenameAsync(topicId, NewTopicName, cancellationToken);
        NewTopicName = string.Empty;
        await RefreshTopicsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task DeleteTopicAsync(CancellationToken cancellationToken)
    {
        if (TopicToManage?.Id is not Guid topicId)
        {
            return;
        }

        await topicService.DeleteAsync(topicId, cancellationToken);
        topicExpansionStates.Remove(topicId);
        await RefreshTopicsAsync(cancellationToken);
        TopicToManage = null;
        await RefreshAsync(cancellationToken);
    }

    private void PopulateEditor(TaskListItemViewModel? task)
    {
        EditTitle = task?.Title ?? string.Empty;
        EditPriority = task?.Priority ?? TaskPriority.Normal;
        EditDueDate = task?.DueAt?.DateTime.Date;
        EditIsRecurring = task?.RecurrenceRule is not null;
        EditRecurrenceFrequency = task?.RecurrenceRule?.Frequency ?? RecurrenceFrequency.Daily;
        EditTopic = Topics.FirstOrDefault(option => option.Id == task?.TopicId) ?? Topics.FirstOrDefault();
        NewNoteText = string.Empty;

        SelectedTaskNotes.Clear();
        if (task is not null)
        {
            foreach (TaskNote note in task.Notes)
            {
                SelectedTaskNotes.Add(new NoteViewModel(note));
            }
        }
    }

    private bool CanAddTask()
    {
        return !string.IsNullOrWhiteSpace(NewTaskText);
    }

    private bool HasSelectedTask()
    {
        return SelectedTask is not null;
    }

    private bool CanAddNote()
    {
        return SelectedTask is not null && !string.IsNullOrWhiteSpace(NewNoteText);
    }

    private void RaiseCommandStates()
    {
        if (AddTaskCommand is AsyncRelayCommand addTaskCommand)
        {
            addTaskCommand.RaiseCanExecuteChanged();
        }

        if (SaveSelectedCommand is AsyncRelayCommand saveSelectedCommand)
        {
            saveSelectedCommand.RaiseCanExecuteChanged();
        }

        if (CompleteSelectedCommand is AsyncRelayCommand completeSelectedCommand)
        {
            completeSelectedCommand.RaiseCanExecuteChanged();
        }

        if (DeleteSelectedCommand is AsyncRelayCommand deleteSelectedCommand)
        {
            deleteSelectedCommand.RaiseCanExecuteChanged();
        }

        if (AddNoteCommand is AsyncRelayCommand addNoteCommand)
        {
            addNoteCommand.RaiseCanExecuteChanged();
        }
        RaiseTopicCommandStates();
    }

    private bool CanCreateTopic() => !string.IsNullOrWhiteSpace(NewTopicName);
    private bool CanRenameTopic() => TopicToManage?.Id is not null && !string.IsNullOrWhiteSpace(NewTopicName);
    private bool HasSelectedTopic() => TopicToManage?.Id is not null;

    private void RaiseTopicCommandStates()
    {
        (CreateTopicCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RenameTopicCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteTopicCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}
