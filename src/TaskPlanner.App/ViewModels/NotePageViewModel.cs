using TaskPlanner.Domain.Notes;

namespace TaskPlanner.App.ViewModels;

public sealed class NotePageViewModel : ObservableObject
{
    private string content;
    private string? customTitle;
    private bool isRenaming;
    private string renameText = string.Empty;
    private int editVersion;
    private int savedVersion;

    public NotePageViewModel(NotePage note)
    {
        Id = note.Id;
        content = note.Content;
        customTitle = note.CustomTitle;
    }

    /// <summary>Raised whenever the user edits the content, to (re)start the autosave delay.</summary>
    public event EventHandler? Edited;

    public Guid Id { get; }

    public string Content
    {
        get => content;
        set
        {
            if (SetProperty(ref content, value ?? string.Empty))
            {
                editVersion++;
                RaisePropertyChanged(nameof(DisplayTitle));
                RaisePropertyChanged(nameof(StatsText));
                Edited?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string? CustomTitle
    {
        get => customTitle;
        set
        {
            if (SetProperty(ref customTitle, string.IsNullOrWhiteSpace(value) ? null : value.Trim()))
            {
                RaisePropertyChanged(nameof(DisplayTitle));
                RaisePropertyChanged(nameof(HasCustomTitle));
            }
        }
    }

    public bool HasCustomTitle => CustomTitle is not null;

    public string DisplayTitle => NotePage.ResolveTitle(CustomTitle, Content);

    public string StatsText
    {
        get
        {
            int words = Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return $"{words} {(words == 1 ? "word" : "words")} · {Content.Length} characters";
        }
    }

    /// <summary>True when nothing would be lost by deleting this note without asking.</summary>
    public bool IsBlank => string.IsNullOrWhiteSpace(Content) && CustomTitle is null;

    public bool IsDirty => editVersion != savedVersion;

    public bool IsRenaming
    {
        get => isRenaming;
        set => SetProperty(ref isRenaming, value);
    }

    public string RenameText
    {
        get => renameText;
        set => SetProperty(ref renameText, value);
    }

    /// <summary>Takes a snapshot to save; pass the version to <see cref="MarkSaved"/> afterwards.</summary>
    public (string Content, int Version) Snapshot() => (Content, editVersion);

    /// <summary>
    /// Marks the given version as persisted. Edits made while the save was in flight
    /// have a higher version, so the note stays dirty and is saved again.
    /// </summary>
    public void MarkSaved(int version)
    {
        savedVersion = Math.Max(savedVersion, version);
    }
}
