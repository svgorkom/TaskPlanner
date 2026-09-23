using TaskPlanner.Application.Abstractions;
using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Tasks;

public sealed class QuickTaskParser
{
    private readonly IClock clock;

    public QuickTaskParser(IClock clock)
    {
        this.clock = clock;
    }

    public TaskDraft Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("A task title is required.", nameof(input));
        }

        string title = input.Trim();
        TaskPriority priority = ExtractPriority(ref title);
        DateTimeOffset? dueAt = ExtractDueDate(ref title);

        return new TaskDraft(title.Trim(), null, priority, dueAt, null, null);
    }

    private static TaskPriority ExtractPriority(ref string title)
    {
        (string Marker, TaskPriority Priority)[] markers =
        [
            ("!urgent", TaskPriority.Urgent),
            ("!high", TaskPriority.High),
            ("!normal", TaskPriority.Normal),
            ("!low", TaskPriority.Low)
        ];

        foreach ((string marker, TaskPriority priority) in markers)
        {
            if (!title.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            title = title.Replace(marker, string.Empty, StringComparison.OrdinalIgnoreCase);
            return priority;
        }

        return TaskPriority.Normal;
    }

    private DateTimeOffset? ExtractDueDate(ref string title)
    {
        DateTimeOffset now = clock.Now;
        DateTimeOffset today = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

        if (title.Contains(" tomorrow", StringComparison.OrdinalIgnoreCase))
        {
            title = title.Replace(" tomorrow", string.Empty, StringComparison.OrdinalIgnoreCase);
            return today.AddDays(1).AddHours(9);
        }

        if (title.Contains(" today", StringComparison.OrdinalIgnoreCase))
        {
            title = title.Replace(" today", string.Empty, StringComparison.OrdinalIgnoreCase);
            return today.AddHours(Math.Max(now.Hour + 1, 9));
        }

        return null;
    }
}
