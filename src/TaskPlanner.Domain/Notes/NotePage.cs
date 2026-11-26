namespace TaskPlanner.Domain.Notes;

/// <summary>
/// A free-form note shown as a tab in the notes view. Independent of tasks.
/// The tab title is the user's custom title when set, otherwise the note's first line.
/// </summary>
public sealed class NotePage
{
    public const int MaxTitleLength = 40;
    public const string DefaultTitle = "New note";

    public NotePage(
        Guid id,
        string? customTitle,
        string content,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int sortOrder = 0)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The note id cannot be empty.", nameof(id));
        }

        Id = id;
        CustomTitle = NormalizeTitle(customTitle);
        Content = content ?? string.Empty;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        SortOrder = sortOrder;
    }

    public Guid Id { get; }

    /// <summary>The user-chosen title, or <c>null</c> when the title follows the first line.</summary>
    public string? CustomTitle { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int SortOrder { get; private set; }

    public string DisplayTitle => ResolveTitle(CustomTitle, Content);

    public static NotePage Create(DateTimeOffset createdAt)
    {
        return new NotePage(Guid.NewGuid(), null, string.Empty, createdAt, createdAt);
    }

    public void UpdateContent(string content, DateTimeOffset updatedAt)
    {
        Content = content ?? string.Empty;
        UpdatedAt = updatedAt;
    }

    /// <summary>Sets a custom title; an empty or whitespace title switches back to the first line.</summary>
    public void SetCustomTitle(string? title, DateTimeOffset updatedAt)
    {
        CustomTitle = NormalizeTitle(title);
        UpdatedAt = updatedAt;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    public static string ResolveTitle(string? customTitle, string? content)
    {
        string? title = NormalizeTitle(customTitle) ?? FirstLine(content);
        if (string.IsNullOrEmpty(title))
        {
            return DefaultTitle;
        }

        return title.Length <= MaxTitleLength ? title : title[..(MaxTitleLength - 1)].TrimEnd() + "…";
    }

    private static string? FirstLine(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        foreach (string line in content.Split('\n'))
        {
            // Ignore simple markdown-style prefixes so "# Shopping" becomes "Shopping".
            string trimmed = line.Trim().TrimStart('#', '-', '*', '>', ' ', '\t').Trim();
            if (trimmed.Length > 0)
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string? NormalizeTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }
}
