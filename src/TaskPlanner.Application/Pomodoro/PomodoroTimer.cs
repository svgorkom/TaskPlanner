using TaskPlanner.Application.Abstractions;
using TaskPlanner.Application.Settings;

namespace TaskPlanner.Application.Pomodoro;

/// <summary>
/// Pomodoro state machine: Focus → Short break → Focus … → Long break after every
/// N focus sessions. Time is derived from an end timestamp on <see cref="IClock"/>
/// rather than by counting ticks, so the countdown stays correct when the UI
/// thread stalls or the machine sleeps. The next phase never starts on its own.
/// </summary>
public sealed class PomodoroTimer
{
    private readonly IClock clock;
    private PomodoroSettings settings;
    private DateTimeOffset? endsAt;
    private TimeSpan pausedRemaining;

    public PomodoroTimer(IClock clock, PomodoroSettings settings)
    {
        this.clock = clock;
        this.settings = settings.Normalize();
    }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Focus;

    public PomodoroStatus Status { get; private set; } = PomodoroStatus.Ready;

    public int CompletedFocusSessions { get; private set; }

    public PomodoroSettings Settings => settings;

    public TimeSpan Remaining => Status switch
    {
        PomodoroStatus.Running => Max(endsAt!.Value - clock.Now, TimeSpan.Zero),
        PomodoroStatus.Paused => pausedRemaining,
        _ => DurationOf(Phase)
    };

    public TimeSpan CurrentPhaseDuration => DurationOf(Phase);

    public void Start()
    {
        if (Status == PomodoroStatus.Running)
        {
            return;
        }

        endsAt = clock.Now + Remaining;
        Status = PomodoroStatus.Running;
    }

    public void Pause()
    {
        if (Status != PomodoroStatus.Running)
        {
            return;
        }

        pausedRemaining = Remaining;
        endsAt = null;
        Status = PomodoroStatus.Paused;
    }

    /// <summary>Restarts the current phase from its full duration and stops the countdown.</summary>
    public void Reset()
    {
        endsAt = null;
        Status = PomodoroStatus.Ready;
    }

    /// <summary>Ends the current phase early. A skipped focus session does not count as completed.</summary>
    public void Skip()
    {
        MoveToNextPhase(countFocus: false);
    }

    /// <summary>
    /// Call periodically. Returns the completion when the running phase has just
    /// reached zero; the timer then waits in <see cref="PomodoroStatus.Ready"/> for the next phase.
    /// </summary>
    public PomodoroPhaseCompleted? Update()
    {
        if (Status != PomodoroStatus.Running || clock.Now < endsAt!.Value)
        {
            return null;
        }

        PomodoroPhase completed = Phase;
        MoveToNextPhase(countFocus: true);

        return new PomodoroPhaseCompleted(completed, Phase, CompletedFocusSessions);
    }

    /// <summary>
    /// Applies new durations. A phase that is not yet started picks up the new length;
    /// a running or paused phase keeps its current countdown.
    /// </summary>
    public void UpdateSettings(PomodoroSettings newSettings)
    {
        settings = newSettings.Normalize();
    }

    private void MoveToNextPhase(bool countFocus)
    {
        if (Phase == PomodoroPhase.Focus)
        {
            if (countFocus)
            {
                CompletedFocusSessions++;
            }

            bool longBreakDue = countFocus
                && CompletedFocusSessions % settings.SessionsBeforeLongBreak == 0;
            Phase = longBreakDue ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak;
        }
        else
        {
            Phase = PomodoroPhase.Focus;
        }

        endsAt = null;
        Status = PomodoroStatus.Ready;
    }

    private TimeSpan DurationOf(PomodoroPhase phase)
    {
        int minutes = phase switch
        {
            PomodoroPhase.Focus => settings.FocusMinutes,
            PomodoroPhase.ShortBreak => settings.ShortBreakMinutes,
            PomodoroPhase.LongBreak => settings.LongBreakMinutes,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };

        return TimeSpan.FromMinutes(minutes);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
