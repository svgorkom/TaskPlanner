namespace TaskPlanner.Application.Abstractions;

public interface IClock
{
    DateTimeOffset Now { get; }
}
