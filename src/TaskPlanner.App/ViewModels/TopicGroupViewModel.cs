using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskPlanner.App.ViewModels;

public sealed class TopicGroupViewModel : INotifyPropertyChanged
{
    private bool isExpanded;

    public TopicGroupViewModel(Guid? topicId, string topicName, IReadOnlyList<TaskListItemViewModel> tasks, bool isExpanded = true)
    {
        TopicId = topicId;
        TopicName = topicName;
        Tasks = tasks;
        this.isExpanded = isExpanded;
    }

    public Guid? TopicId { get; }

    public string TopicName { get; }

    public IReadOnlyList<TaskListItemViewModel> Tasks { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleGlyph)));
        }
    }

    public string ToggleGlyph => IsExpanded ? "⌃" : "⌄";

    public event PropertyChangedEventHandler? PropertyChanged;
}
