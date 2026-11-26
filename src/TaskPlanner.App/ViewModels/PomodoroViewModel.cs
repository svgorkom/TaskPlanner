using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using TaskPlanner.Application.Pomodoro;
using TaskPlanner.Application.Settings;

namespace TaskPlanner.App.ViewModels;

/// <summary>
/// Presents a <see cref="PomodoroTimer"/>. The dispatcher timer only refreshes the
/// display and polls for phase completion; the timer itself owns the time keeping.
/// </summary>
public sealed class PomodoroViewModel : ObservableObject
{
    private readonly PomodoroTimer timer;
    private readonly DispatcherTimer ticker;
    private bool isSettingsExpanded;
    private string focusMinutesText = string.Empty;
    private string shortBreakMinutesText = string.Empty;
    private string longBreakMinutesText = string.Empty;
    private string sessionsBeforeLongBreakText = string.Empty;

    public PomodoroViewModel(PomodoroTimer timer)
    {
        this.timer = timer;

        ticker = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        ticker.Tick += (_, _) => Tick();

        StartPauseCommand = new RelayCommand(StartPause);
        ResetCommand = new RelayCommand(Reset);
        SkipCommand = new RelayCommand(Skip);
        ToggleSettingsCommand = new RelayCommand(ToggleSettings);
        SaveSettingsCommand = new RelayCommand(SaveSettings, CanSaveSettings);

        LoadSettingsFields(timer.Settings);
    }

    /// <summary>Raised when a running phase reaches zero.</summary>
    public event EventHandler<PomodoroPhaseCompleted>? PhaseCompleted;

    /// <summary>Raised when the user saves new durations, so they can be persisted.</summary>
    public event EventHandler<PomodoroSettings>? SettingsChanged;

    public ICommand StartPauseCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand ToggleSettingsCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public string PhaseText => PhaseName(timer.Phase);

    public string RemainingText => FormatRemaining(timer.Remaining);

    public bool IsRunning => timer.Status == PomodoroStatus.Running;

    public bool IsBreak => timer.Phase != PomodoroPhase.Focus;

    public string StartPauseGlyph => IsRunning ? "⏸" : "▶";

    public string StartPauseToolTip => timer.Status switch
    {
        PomodoroStatus.Running => "Pause",
        PomodoroStatus.Paused => "Resume",
        _ => $"Start {PhaseName(timer.Phase).ToLowerInvariant()}"
    };

    public string SessionText => timer.Status == PomodoroStatus.Paused
        ? $"Paused · {CompletedText}"
        : CompletedText;

    /// <summary>Elapsed fraction of the current phase, 0–1, for the progress bar.</summary>
    public double Progress
    {
        get
        {
            double total = timer.CurrentPhaseDuration.TotalSeconds;
            return total <= 0 ? 0 : Math.Clamp(1 - (timer.Remaining.TotalSeconds / total), 0, 1);
        }
    }

    /// <summary>Short status for the tray icon tooltip (Windows limits it to 63 characters).</summary>
    public string TrayText => timer.Status == PomodoroStatus.Ready
        ? "Task Planner"
        : $"Task Planner · {PhaseText} {RemainingText}{(IsRunning ? string.Empty : " (paused)")}";

    public bool IsSettingsExpanded
    {
        get => isSettingsExpanded;
        private set => SetProperty(ref isSettingsExpanded, value);
    }

    public string FocusMinutesText
    {
        get => focusMinutesText;
        set => SetSettingsField(ref focusMinutesText, value);
    }

    public string ShortBreakMinutesText
    {
        get => shortBreakMinutesText;
        set => SetSettingsField(ref shortBreakMinutesText, value);
    }

    public string LongBreakMinutesText
    {
        get => longBreakMinutesText;
        set => SetSettingsField(ref longBreakMinutesText, value);
    }

    public string SessionsBeforeLongBreakText
    {
        get => sessionsBeforeLongBreakText;
        set => SetSettingsField(ref sessionsBeforeLongBreakText, value);
    }

    public string SettingsHint =>
        $"Minutes {PomodoroSettings.MinMinutes}–{PomodoroSettings.MaxMinutes}, sessions {PomodoroSettings.MinSessionsBeforeLongBreak}–{PomodoroSettings.MaxSessionsBeforeLongBreak}. A running timer keeps its current length.";

    private string CompletedText => timer.CompletedFocusSessions == 1
        ? "1 pomodoro done"
        : $"{timer.CompletedFocusSessions} pomodoros done";

    private void StartPause()
    {
        if (IsRunning)
        {
            timer.Pause();
            ticker.Stop();
        }
        else
        {
            timer.Start();
            ticker.Start();
        }

        RaiseDisplayChanged();
    }

    private void Reset()
    {
        timer.Reset();
        ticker.Stop();
        RaiseDisplayChanged();
    }

    private void Skip()
    {
        timer.Skip();
        ticker.Stop();
        RaiseDisplayChanged();
    }

    private void Tick()
    {
        PomodoroPhaseCompleted? completed = timer.Update();

        if (completed is not null)
        {
            ticker.Stop();
        }

        RaiseDisplayChanged();

        if (completed is not null)
        {
            PhaseCompleted?.Invoke(this, completed);
        }
    }

    private void ToggleSettings()
    {
        if (!IsSettingsExpanded)
        {
            LoadSettingsFields(timer.Settings);
        }

        IsSettingsExpanded = !IsSettingsExpanded;
    }

    private void SaveSettings()
    {
        if (!TryParseSettings(out PomodoroSettings settings))
        {
            return;
        }

        timer.UpdateSettings(settings);
        LoadSettingsFields(timer.Settings);
        IsSettingsExpanded = false;
        RaiseDisplayChanged();
        SettingsChanged?.Invoke(this, timer.Settings);
    }

    private bool CanSaveSettings() => TryParseSettings(out _);

    private bool TryParseSettings(out PomodoroSettings settings)
    {
        settings = new PomodoroSettings();

        if (!TryParseInRange(FocusMinutesText, PomodoroSettings.MinMinutes, PomodoroSettings.MaxMinutes, out int focus)
            || !TryParseInRange(ShortBreakMinutesText, PomodoroSettings.MinMinutes, PomodoroSettings.MaxMinutes, out int shortBreak)
            || !TryParseInRange(LongBreakMinutesText, PomodoroSettings.MinMinutes, PomodoroSettings.MaxMinutes, out int longBreak)
            || !TryParseInRange(SessionsBeforeLongBreakText, PomodoroSettings.MinSessionsBeforeLongBreak, PomodoroSettings.MaxSessionsBeforeLongBreak, out int sessions))
        {
            return false;
        }

        settings = new PomodoroSettings
        {
            FocusMinutes = focus,
            ShortBreakMinutes = shortBreak,
            LongBreakMinutes = longBreak,
            SessionsBeforeLongBreak = sessions
        };

        return true;
    }

    private static bool TryParseInRange(string text, int min, int max, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            && value >= min
            && value <= max;
    }

    private void LoadSettingsFields(PomodoroSettings settings)
    {
        FocusMinutesText = settings.FocusMinutes.ToString(CultureInfo.CurrentCulture);
        ShortBreakMinutesText = settings.ShortBreakMinutes.ToString(CultureInfo.CurrentCulture);
        LongBreakMinutesText = settings.LongBreakMinutes.ToString(CultureInfo.CurrentCulture);
        SessionsBeforeLongBreakText = settings.SessionsBeforeLongBreak.ToString(CultureInfo.CurrentCulture);
    }

    private void SetSettingsField(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            (SaveSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void RaiseDisplayChanged()
    {
        RaisePropertyChanged(nameof(PhaseText));
        RaisePropertyChanged(nameof(RemainingText));
        RaisePropertyChanged(nameof(IsRunning));
        RaisePropertyChanged(nameof(IsBreak));
        RaisePropertyChanged(nameof(StartPauseGlyph));
        RaisePropertyChanged(nameof(StartPauseToolTip));
        RaisePropertyChanged(nameof(SessionText));
        RaisePropertyChanged(nameof(Progress));
        RaisePropertyChanged(nameof(TrayText));
    }

    private static string PhaseName(PomodoroPhase phase) => phase switch
    {
        PomodoroPhase.Focus => "Focus",
        PomodoroPhase.ShortBreak => "Short break",
        PomodoroPhase.LongBreak => "Long break",
        _ => phase.ToString()
    };

    private static string FormatRemaining(TimeSpan remaining)
    {
        // Round up so the display shows 25:00 at start and only hits 00:00 at the end.
        int totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
