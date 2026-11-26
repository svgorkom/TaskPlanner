namespace TaskPlanner.Application.Pomodoro;

public enum PomodoroStatus
{
    /// <summary>The current phase has not started yet and has its full duration left.</summary>
    Ready,

    Running,

    Paused
}
