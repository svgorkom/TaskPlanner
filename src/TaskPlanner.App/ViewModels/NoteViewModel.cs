using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.App.ViewModels;

public sealed class NoteViewModel : ObservableObject
{
    private string text;

    public NoteViewModel(TaskNote note)
    {
        Id = note.Id;
        text = note.Text;
    }

    public Guid Id { get; }

    public string Text
    {
        get => text;
        set => SetProperty(ref text, value);
    }
}
