using TaskPlanner.Application.Abstractions;

namespace TaskPlanner.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
