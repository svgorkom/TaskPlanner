namespace TaskPlanner.Application.Pomodoro;

public sealed record PomodoroPhaseCompleted(PomodoroPhase CompletedPhase, PomodoroPhase NextPhase, int CompletedFocusSessions);
