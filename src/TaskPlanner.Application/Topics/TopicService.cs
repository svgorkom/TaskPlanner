using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;
using TaskPlanner.Domain.Topics;

namespace TaskPlanner.Application.Topics;

public sealed class TopicService
{
    private readonly IClock clock;
    private readonly ITaskRepository taskRepository;
    private readonly ITopicRepository topicRepository;

    public TopicService(ITopicRepository topicRepository, ITaskRepository taskRepository, IClock clock)
    {
        this.topicRepository = topicRepository;
        this.taskRepository = taskRepository;
        this.clock = clock;
    }

    public Task<IReadOnlyCollection<TaskTopic>> ListAsync(CancellationToken cancellationToken)
    {
        return topicRepository.ListAsync(cancellationToken);
    }

    public async Task<TaskTopic> CreateAsync(string name, CancellationToken cancellationToken)
    {
        await EnsureNameAvailableAsync(name, null, cancellationToken);
        IReadOnlyCollection<TaskTopic> topics = await topicRepository.ListAsync(cancellationToken);
        DateTimeOffset now = clock.Now;
        TaskTopic topic = TaskTopic.Create(name, now);
        topic.SetSortOrder(topics.Count == 0 ? 0 : topics.Max(existing => existing.SortOrder) + 1);
        await topicRepository.SaveAsync(topic, cancellationToken);
        return topic;
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskTopic> topics = await topicRepository.ListAsync(cancellationToken);
        TaskTopic topic = topics.SingleOrDefault(candidate => candidate.Id == id)
            ?? throw new InvalidOperationException($"Topic '{id}' does not exist.");
        await EnsureNameAvailableAsync(name, id, cancellationToken);
        topic.Rename(name, clock.Now);
        await topicRepository.SaveAsync(topic, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskTopic> topics = await topicRepository.ListAsync(cancellationToken);
        if (topics.All(topic => topic.Id != id))
        {
            throw new InvalidOperationException($"Topic '{id}' does not exist.");
        }

        IReadOnlyCollection<TaskItem> tasks = await taskRepository.ListAsync(cancellationToken);
        foreach (TaskItem task in tasks.Where(task => task.TopicId == id))
        {
            task.SetTopic(null, clock.Now);
        }

        await taskRepository.SaveAllAsync(tasks, cancellationToken);
        await topicRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task ReorderAsync(IReadOnlyList<Guid> orderedTopicIds, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<TaskTopic> topics = await topicRepository.ListAsync(cancellationToken);
        if (orderedTopicIds.Count != topics.Count || orderedTopicIds.Distinct().Count() != topics.Count ||
            orderedTopicIds.Any(id => topics.All(topic => topic.Id != id)))
        {
            throw new ArgumentException("The topic order is invalid.", nameof(orderedTopicIds));
        }

        foreach ((Guid id, int index) in orderedTopicIds.Select((id, index) => (id, index)))
        {
            topics.Single(topic => topic.Id == id).SetSortOrder(index);
        }

        await topicRepository.SaveAllAsync(topics, cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        string normalizedName = name.Trim();
        IReadOnlyCollection<TaskTopic> topics = await topicRepository.ListAsync(cancellationToken);
        if (topics.Any(topic => topic.Id != excludedId && string.Equals(topic.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Topic '{name}' already exists.");
        }
    }
}
