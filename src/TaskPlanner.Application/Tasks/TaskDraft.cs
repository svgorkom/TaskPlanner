using TaskPlanner.Domain.Tasks;

namespace TaskPlanner.Application.Tasks;

public sealed record TaskDraft(
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTimeOffset? DueAt,
    DateTimeOffset? ReminderAt,
    RecurrenceRule? RecurrenceRule);
