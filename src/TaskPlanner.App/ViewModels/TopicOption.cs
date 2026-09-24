using TaskPlanner.Domain.Topics;

namespace TaskPlanner.App.ViewModels;

public sealed class TopicOption
{
    public TopicOption(Guid? id, string name, bool isAll = false, bool isNoTopic = false, bool isGroupByTopic = false)
    {
        Id = id;
        Name = name;
        IsAll = isAll;
        IsNoTopic = isNoTopic;
        IsGroupByTopic = isGroupByTopic;
    }

    public Guid? Id { get; }
    public string Name { get; }
    public bool IsAll { get; }
    public bool IsNoTopic { get; }
    public bool IsGroupByTopic { get; }

    public static TopicOption FromTopic(TaskTopic topic) => new(topic.Id, topic.Name);

    public override string ToString() => Name;
}
