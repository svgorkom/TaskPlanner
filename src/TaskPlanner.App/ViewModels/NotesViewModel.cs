using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using TaskPlanner.App.Diagnostics;
using TaskPlanner.Application.Notes;
using TaskPlanner.Domain.Notes;

namespace TaskPlanner.App.ViewModels;

/// <summary>
/// Tabbed notes with autosave: edits are written one second after the last keystroke,
/// and immediately when switching tabs, hiding the panel or exiting the application.
/// All writes go through one gate so saves, deletes and reorders never interleave.
/// </summary>
public sealed class NotesViewModel : ObservableObject
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1);

    private readonly NoteService noteService;
    private readonly DispatcherTimer autosaveTimer;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private NotePageViewModel? selectedPage;
    private NotePageViewModel? pendingDelete;
    private string saveStatusText = string.Empty;
    private bool hasSaveError;

    public NotesViewModel(NoteService noteService)
    {
        this.noteService = noteService;

        autosaveTimer = new DispatcherTimer { Interval = AutosaveDelay };
        autosaveTimer.Tick += async (_, _) =>
        {
            autosaveTimer.Stop();
            await FlushAsync();
        };

        NewPageCommand = new AsyncRelayCommand(NewPageAsync);
        CloseCommand = new AsyncRelayCommand<NotePageViewModel>(CloseAsync, page => page is not null);
        ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
        CancelDeleteCommand = new RelayCommand(() => PendingDelete = null);
        StartRenameCommand = new RelayCommand<NotePageViewModel>(StartRename);
        UseFirstLineAsTitleCommand = new AsyncRelayCommand<NotePageViewModel>(UseFirstLineAsTitleAsync, page => page is not null);

        _ = InitializeAsync();
    }

    public ObservableCollection<NotePageViewModel> Pages { get; } = [];

    public ICommand NewPageCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand ConfirmDeleteCommand { get; }

    public ICommand CancelDeleteCommand { get; }

    public ICommand StartRenameCommand { get; }

    public ICommand UseFirstLineAsTitleCommand { get; }

    public NotePageViewModel? SelectedPage
    {
        get => selectedPage;
        set
        {
            NotePageViewModel? previous = selectedPage;
            if (!SetProperty(ref selectedPage, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasSelection));
            if (previous is not null)
            {
                CommitRename(previous);
                if (previous.IsDirty)
                {
                    _ = FlushAsync();
                }
            }
        }
    }

    public bool HasSelection => SelectedPage is not null;

    public bool IsEmpty => Pages.Count == 0;

    public NotePageViewModel? PendingDelete
    {
        get => pendingDelete;
        private set
        {
            if (SetProperty(ref pendingDelete, value))
            {
                RaisePropertyChanged(nameof(IsDeletePending));
                RaisePropertyChanged(nameof(DeletePromptText));
            }
        }
    }

    public bool IsDeletePending => PendingDelete is not null;

    public string DeletePromptText => PendingDelete is null
        ? string.Empty
        : $"Delete “{PendingDelete.DisplayTitle}”? This cannot be undone.";

    public string SaveStatusText
    {
        get => saveStatusText;
        private set => SetProperty(ref saveStatusText, value);
    }

    public bool HasSaveError
    {
        get => hasSaveError;
        private set => SetProperty(ref hasSaveError, value);
    }

    /// <summary>Saves every note with unsaved edits. Safe to call at any time.</summary>
    public async Task FlushAsync()
    {
        autosaveTimer.Stop();
        await writeGate.WaitAsync();
        try
        {
            List<NotePageViewModel> dirtyPages = Pages.Where(page => page.IsDirty).ToList();
            if (dirtyPages.Count == 0)
            {
                return;
            }

            SaveStatusText = "Saving…";
            foreach (NotePageViewModel page in dirtyPages)
            {
                (string content, int version) = page.Snapshot();
                await noteService.UpdateContentAsync(page.Id, content, CancellationToken.None);
                page.MarkSaved(version);
            }

            HasSaveError = false;
            SaveStatusText = $"Saved {DateTime.Now:HH:mm}";
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Saving notes failed", ex);
            HasSaveError = true;
            SaveStatusText = "Could not save — will retry";
            autosaveTimer.Start();
        }
        finally
        {
            writeGate.Release();
        }

        // Edits typed while the save was running are picked up by the next round.
        if (Pages.Any(page => page.IsDirty) && !autosaveTimer.IsEnabled)
        {
            autosaveTimer.Start();
        }
    }

    public async Task ReorderAsync(Guid draggedId, Guid targetId)
    {
        int draggedIndex = IndexOf(draggedId);
        int targetIndex = IndexOf(targetId);
        if (draggedId == targetId || draggedIndex < 0 || targetIndex < 0)
        {
            return;
        }

        NotePageViewModel selected = SelectedPage ?? Pages[draggedIndex];
        Pages.Move(draggedIndex, targetIndex);
        SelectedPage = selected;

        await RunWriteAsync(
            () => noteService.ReorderAsync(Pages.Select(page => page.Id).ToArray(), CancellationToken.None),
            "Reordering notes failed");
    }

    public async Task CommitRenameAsync(NotePageViewModel page)
    {
        if (!page.IsRenaming)
        {
            return;
        }

        page.IsRenaming = false;
        string? newTitle = string.IsNullOrWhiteSpace(page.RenameText) ? null : page.RenameText.Trim();
        if (newTitle == page.CustomTitle)
        {
            return;
        }

        page.CustomTitle = newTitle;
        await RunWriteAsync(
            () => noteService.SetTitleAsync(page.Id, newTitle, CancellationToken.None),
            "Renaming note failed");
    }

    /// <summary>Cancels an open rename or delete prompt. Returns false when there was nothing to cancel.</summary>
    public bool CancelInlineEdit()
    {
        NotePageViewModel? renaming = Pages.FirstOrDefault(page => page.IsRenaming);
        if (renaming is not null)
        {
            renaming.IsRenaming = false;
            return true;
        }

        if (PendingDelete is not null)
        {
            PendingDelete = null;
            return true;
        }

        return false;
    }

    private async Task InitializeAsync()
    {
        try
        {
            IReadOnlyList<NotePage> notes = await noteService.ListAsync(CancellationToken.None);
            foreach (NotePage note in notes)
            {
                Add(new NotePageViewModel(note));
            }

            SelectedPage = Pages.FirstOrDefault();
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Loading notes failed", ex);
            HasSaveError = true;
            SaveStatusText = "Could not load notes";
        }
    }

    private async Task NewPageAsync(CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            NotePage note = await noteService.CreateAsync(cancellationToken);
            NotePageViewModel page = new(note);
            Add(page);
            SelectedPage = page;
            PendingDelete = null;
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task CloseAsync(NotePageViewModel? page, CancellationToken cancellationToken)
    {
        if (page is null)
        {
            return;
        }

        if (page.IsBlank)
        {
            await DeleteAsync(page);
            return;
        }

        SelectedPage = page;
        PendingDelete = page;
    }

    private async Task ConfirmDeleteAsync(CancellationToken cancellationToken)
    {
        if (PendingDelete is { } page)
        {
            PendingDelete = null;
            await DeleteAsync(page);
        }
    }

    private async Task DeleteAsync(NotePageViewModel page)
    {
        int index = Pages.IndexOf(page);
        if (index < 0)
        {
            return;
        }

        await writeGate.WaitAsync();
        try
        {
            await noteService.DeleteAsync(page.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            FileLogger.LogException("Deleting note failed", ex);
            HasSaveError = true;
            SaveStatusText = "Could not delete note";
            return;
        }
        finally
        {
            writeGate.Release();
        }

        page.Edited -= Page_Edited;
        bool wasSelected = SelectedPage == page;
        Pages.Remove(page);
        RaisePropertyChanged(nameof(IsEmpty));

        if (wasSelected)
        {
            // Select the neighbour, like closing a browser tab.
            selectedPage = null;
            SelectedPage = Pages.Count == 0 ? null : Pages[Math.Min(index, Pages.Count - 1)];
        }
    }

    private void StartRename(NotePageViewModel? page)
    {
        if (page is null)
        {
            return;
        }

        SelectedPage = page;
        page.RenameText = page.DisplayTitle == NotePage.DefaultTitle && page.CustomTitle is null
            ? string.Empty
            : page.CustomTitle ?? page.DisplayTitle;
        page.IsRenaming = true;
    }

    private async Task UseFirstLineAsTitleAsync(NotePageViewModel? page, CancellationToken cancellationToken)
    {
        if (page is null || page.CustomTitle is null)
        {
            return;
        }

        page.CustomTitle = null;
        await RunWriteAsync(
            () => noteService.SetTitleAsync(page.Id, null, cancellationToken),
            "Resetting note title failed");
    }

    private void CommitRename(NotePageViewModel page)
    {
        _ = CommitRenameAsync(page);
    }

    private async Task RunWriteAsync(Func<Task> write, string failureMessage)
    {
        await writeGate.WaitAsync();
        try
        {
            await write();
        }
        catch (Exception ex)
        {
            FileLogger.LogException(failureMessage, ex);
            HasSaveError = true;
            SaveStatusText = "Could not save change";
        }
        finally
        {
            writeGate.Release();
        }
    }

    private void Add(NotePageViewModel page)
    {
        page.Edited += Page_Edited;
        Pages.Add(page);
        RaisePropertyChanged(nameof(IsEmpty));
    }

    private void Page_Edited(object? sender, EventArgs e)
    {
        SaveStatusText = "Editing…";
        autosaveTimer.Stop();
        autosaveTimer.Start();
    }

    private int IndexOf(Guid id)
    {
        for (int index = 0; index < Pages.Count; index++)
        {
            if (Pages[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }
}
