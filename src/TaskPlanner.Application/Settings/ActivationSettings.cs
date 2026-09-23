namespace TaskPlanner.Application.Settings;

public sealed class ActivationSettings
{
    public string Hotkey { get; init; } = "Ctrl+Shift+Space";

    public bool EnableTripleCtrl { get; init; } = true;

    public bool EnableDoubleShift { get; init; } = true;
}
