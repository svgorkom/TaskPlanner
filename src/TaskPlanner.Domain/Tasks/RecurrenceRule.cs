namespace TaskPlanner.Domain.Tasks;

public sealed class RecurrenceRule
{
    public RecurrenceRule(RecurrenceFrequency frequency, int interval)
    {
        if (interval < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The recurrence interval must be at least 1.");
        }

        Frequency = frequency;
        Interval = interval;
    }

    public RecurrenceFrequency Frequency { get; }

    public int Interval { get; }

    public DateTimeOffset GetNextOccurrence(DateTimeOffset occurrence)
    {
        return Frequency switch
        {
            RecurrenceFrequency.Daily => occurrence.AddDays(Interval),
            RecurrenceFrequency.Weekly => occurrence.AddDays(Interval * 7),
            RecurrenceFrequency.Monthly => occurrence.AddMonths(Interval),
            _ => throw new InvalidOperationException($"Unsupported recurrence frequency: {Frequency}.")
        };
    }
}
