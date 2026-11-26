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
    private bool showCompleted;
    private bool startWithWindows;
    private ThemeMode selectedThemeMode;
    private bool suppressStartWithWindowsChanged;
    private string newTopicName = string.Empty;
    private TopicOption? topicToManage;
    private readonly Dictionary<Guid, bool> topicExpansionStates = [];
    private readonly Dictionary<Guid, bool> taskExpansionStates = [];
    private bool noTopicExpanded = true;

    public MainViewModel(
        TaskService taskService,
        TaskQueryService taskQueryService,
        QuickTaskParser quickTaskParser,
        TopicService topicService,
        ThemeMode selectedThemeMode,
        bool startWithWindows,
        PomodoroViewModel pomodoro)
    {
        Pomodoro = pomodoro;
        this.taskService = taskService;
        this.taskQueryService = taskQueryService;
        this.quickTaskParser = quickTaskParser;
        this.topicService = topicService;
        this.selectedThemeMode = selectedThemeMode;
        this.startWithWindows = startWithWindows;

        AddTaskCommand = new AsyncRelayCommand(AddTaskAsync, CanAddTask);
        SaveTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(SaveTaskAsync, task => task is not null);
        CompleteTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(CompleteTaskAsync, task => task is not null);
        ReopenTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(ReopenTaskAsync, task => task is not null);
        DeleteTaskCommand = new AsyncRelayCommand<TaskListItemViewModel>(DeleteTaskAsync, task => task is not null);
        ToggleTaskExpandedCommand = new RelayCommand<TaskListItemViewModel>(ToggleTaskExpanded);
        AddNoteCommand = new AsyncRelayCommand<TaskListItemViewModel>(AddNoteAsync, task => task is not null);
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

    public PomodoroViewModel Pomodoro { get; }

    public ObservableCollection<TaskListItemViewModel> Tasks { get; } = [];

    public ObservableCollection<TopicGroupViewModel> TopicGroups { get; } = [];

    public ObservableCollection<TopicOption> Topics { get; } = [];

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

    public bool ShowCompleted
    {
        get => showCompleted;
        set
        {
            if (SetProperty(ref showCompleted, value))
            {
                _ = RefreshAsync(CancellationToken.None);
            }
        }
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

    public ICommand SaveTaskCommand { get; }

    public ICommand CompleteTaskCommand { get; }

    public ICommand ReopenTaskCommand { get; }

    public ICommand DeleteTaskCommand { get; }

    public ICommand ToggleTaskExpandedCommand { get; }

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

    public async Task SaveTaskAsync(TaskListItemViewModel? task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            return;
        }

        TaskUpdate update = new(
            task.EditTitle,
            task.Description,
            task.Priority,
            task.DueAt,
            task.ReminderAt,
            task.RecurrenceRule,
            task.EditTopic?.Id);

        await taskService.UpdateAsync(task.Id, update, cancellationToken);
        taskExpansionStates[task.Id] = true;
        await RefreshAsync(cancellationToken);
    }

    private async Task CompleteTaskAsync(TaskListItemViewModel? task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            return;
        }

        await taskService.CompleteAsync(task.Id, cancellationToken);
        taskExpansionStates.Remove(task.Id);
        await RefreshAsync(cancellationToken);
    }

    private async Task ReopenTaskAsync(TaskListItemViewModel? task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            return;
        }

        await taskService.ReopenAsync(task.Id, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private async Task DeleteTaskAsync(TaskListItemViewModel? task, CancellationToken cancellationToken)
    {
        if (task is null)
        {
            return;
        }

        taskExpansionStates.Remove(task.Id);
        await taskService.DeleteAsync(task.Id, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    private void ToggleTaskExpanded(TaskListItemViewModel? task)
    {
        if (task is null)
        {
            return;
        }

        bool expanding = !task.IsExpanded;
        task.IsExpanded = expanding;
        taskExpansionStates[task.Id] = expanding;

        if (!expanding)
        {
            task.ResetEdits(Topics);
        }
    }

    private async Task AddNoteAsync(TaskListItemViewModel? task, CancellationToken cancellationToken)
    {
        if (task is null || string.IsNullOrWhiteSpace(task.NewNoteText))
        {
            return;
        }

        await taskService.AddNoteAsync(task.Id, task.NewNoteText, cancellationToken);
        taskExpansionStates[task.Id] = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task UpdateNoteAsync(NoteViewModel note, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(note.Text))
        {
            return;
        }

        await taskService.UpdateNoteAsync(note.TaskId, note.Id, note.Text, cancellationToken);
        taskExpansionStates[note.TaskId] = true;
        await RefreshAsync(cancellationToken);
    }

    private async Task RemoveNoteAsync(NoteViewModel? note, CancellationToken cancellationToken)
    {
        if (note is null)
        {
            return;
        }

        await taskService.RemoveNoteAsync(note.TaskId, note.Id, cancellationToken);
        taskExpansionStates[note.TaskId] = true;
        await RefreshAsync(cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await RefreshGroupedAsync(cancellationToken);
    }

    private async Task RefreshGroupedAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskItem> activeTasks = await taskQueryService.ListAsync(TaskViewKind.Active, null, false, cancellationToken);
        IReadOnlyCollection<TaskItem> tasks = ShowCompleted
            ? [.. activeTasks, .. await taskQueryService.ListAsync(TaskViewKind.Completed, null, false, cancellationToken)]
            : activeTasks;
        Tasks.Clear();
        TopicGroups.Clear();

        int number = 1;
        foreach (TopicOption topic in Topics.Where(option => !option.IsNoTopic))
        {
            Guid topicId = topic.Id
                ?? throw new InvalidOperationException("A user-defined topic must have an id.");
            List<TaskListItemViewModel> topicTasks = tasks
                .Where(task => task.TopicId == topicId)
                .Select(task => new TaskListItemViewModel(task, number++, Topics, taskExpansionStates.GetValueOrDefault(task.Id, false)))
                .ToList();
            TopicGroups.Add(new TopicGroupViewModel(
                topicId,
                topic.Name,
                topicTasks,
                topicExpansionStates.GetValueOrDefault(topicId, true)));
        }

        List<TaskListItemViewModel> unassignedTasks = tasks
            .Where(task => !task.TopicId.HasValue)
            .Select(task => new TaskListItemViewModel(task, number++, Topics, taskExpansionStates.GetValueOrDefault(task.Id, false)))
            .ToList();
        TopicGroups.Add(new TopicGroupViewModel(
            null,
            "No topic",
            unassignedTasks,
            noTopicExpanded));
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

    private bool CanAddTask()
    {
        return !string.IsNullOrWhiteSpace(NewTaskText);
    }

    private void RaiseCommandStates()
    {
        if (AddTaskCommand is AsyncRelayCommand addTaskCommand)
        {
            addTaskCommand.RaiseCanExecuteChanged();
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
