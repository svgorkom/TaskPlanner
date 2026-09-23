namespace TaskPlanner.Application.Settings;

public sealed class UserSettings
{
    public ActivationSettings Activation { get; init; } = new();

    public PanelSettings Panel { get; init; } = new();

    public bool StartWithWindows { get; init; }

    public ThemeMode Theme { get; init; } = ThemeMode.System;
}
