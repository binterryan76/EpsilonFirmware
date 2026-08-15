using EpsilonCore.Motion.Axis;

namespace EpsilonDesktop.ViewModels;

public class AxisLinearButtonsViewModel(AxisLinear axis, bool buttonsEnabled = false)
{
    public AxisLinear Axis { get; init; } = axis;
    public bool ButtonsEnabled { get; set; } = buttonsEnabled;
}
