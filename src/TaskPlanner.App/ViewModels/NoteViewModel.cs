using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.App.ViewModels;

public sealed class NoteViewModel : ObservableObject
{
    private string text;

    public NoteViewModel(Guid taskId, TaskNote note)
    {
        TaskId = taskId;
        Id = note.Id;
        text = note.Text;
    }

    public Guid TaskId { get; }

    public Guid Id { get; }

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value);
    }
}
