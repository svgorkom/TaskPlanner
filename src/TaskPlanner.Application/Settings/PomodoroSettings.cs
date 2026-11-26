namespace TaskPlanner.Application.Settings;

public sealed class PomodoroSettings
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 180;
    public const int MinSessionsBeforeLongBreak = 1;
    public const int MaxSessionsBeforeLongBreak = 12;

    public int FocusMinutes { get; init; } = 25;

    public int ShortBreakMinutes { get; init; } = 5;

    public int LongBreakMinutes { get; init; } = 15;

    public int SessionsBeforeLongBreak { get; init; } = 4;

    /// <summary>
    /// Returns a copy with every value clamped to its supported range, so a
    /// hand-edited or corrupt settings file can never produce a zero-length phase.
    /// </summary>
    public PomodoroSettings Normalize()
    {
        return new PomodoroSettings
        {
            FocusMinutes = Math.Clamp(FocusMinutes, MinMinutes, MaxMinutes),
            ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, MinMinutes, MaxMinutes),
            LongBreakMinutes = Math.Clamp(LongBreakMinutes, MinMinutes, MaxMinutes),
            SessionsBeforeLongBreak = Math.Clamp(SessionsBeforeLongBreak, MinSessionsBeforeLongBreak, MaxSessionsBeforeLongBreak)
        };
    }
}
