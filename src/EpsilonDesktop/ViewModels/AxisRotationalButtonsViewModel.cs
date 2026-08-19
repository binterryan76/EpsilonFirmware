using EpsilonCore.Motion.Axis;

namespace EpsilonDesktop.ViewModels;

public class AxisRotationalButtonsViewModel(AxisRotational axis, bool buttonsEnabled = false)
{
    public AxisRotational Axis { get; init; } = axis;
    public bool ButtonsEnabled { get; set; } = buttonsEnabled;
}
