namespace TaskPlanner.Domain.Topics;

public sealed class TaskTopic
{
    public TaskTopic(Guid id, string name, DateTimeOffset createdAt, DateTimeOffset updatedAt, int sortOrder = 0)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The topic id cannot be empty.", nameof(id));
        }

        Id = id;
        Name = RequireName(name);
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        SortOrder = sortOrder;
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int SortOrder { get; private set; }

    public static TaskTopic Create(string name, DateTimeOffset createdAt)
    {
        return new TaskTopic(Guid.NewGuid(), name, createdAt, createdAt);
    }

    public void Rename(string name, DateTimeOffset updatedAt)
    {
        Name = RequireName(name);
        UpdatedAt = updatedAt;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    private static string RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A topic name is required.", nameof(name));
        }

        return name.Trim();
    }
}
