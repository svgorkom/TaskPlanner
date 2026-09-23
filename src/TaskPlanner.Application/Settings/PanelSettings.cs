namespace TaskPlanner.Application.Settings;

public sealed class PanelSettings
{
    public PanelEdge SlideFrom { get; init; } = PanelEdge.Right;

    public double SizePercent { get; init; } = 33;

    public int AnimationMilliseconds { get; init; } = 160;

    public bool AutoHide { get; init; } = true;

    public bool AlwaysOnTop { get; init; } = true;
}
