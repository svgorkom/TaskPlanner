namespace TaskPlanner.Domain.Tasks;

public sealed class TaskNote
{
    public TaskNote(Guid id, string text, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The note id cannot be empty.", nameof(id));
        }

        Id = id;
        Text = RequireText(text);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; }

    public string Text { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TaskNote Create(string text, DateTimeOffset createdAt)
    {
        return new TaskNote(Guid.NewGuid(), text, createdAt, createdAt);
    }

    public void UpdateText(string text, DateTimeOffset updatedAt)
    {
        Text = RequireText(text);
        UpdatedAt = updatedAt;
    }

    private static string RequireText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("A note requires text.", nameof(text));
        }

        return text.Trim();
    }
}
