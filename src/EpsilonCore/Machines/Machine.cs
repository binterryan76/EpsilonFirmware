using EpsilonCore.Display;
using EpsilonCore.Motion;
using EpsilonCore.Units;

namespace EpsilonCore.Machines;


/// <summary>
/// Represents a physical machine with all of it's hardware.
/// Every machine has one or more microcontroller boards, a motion system, and a command queue.
/// </summary>
public record Machine
{
    public Machine(
        string name,
        DisplayUnits displayUnits,
        IResultMessageLogger resultMessageLogger,
        MotionSystemPrecisions precisions)
    {
        Name = name;
        DisplayUnits = displayUnits;
        ResultMessageLogger = resultMessageLogger;
        Entities = new Entities();
        MotionSystem = new MotionSystem(precisions);
    }


    public string Name { get; init; }

    public Entities Entities { get; init; }

    /// <summary>
    /// Set of units used when displaying physical quantities.
    /// </summary>
    public DisplayUnits DisplayUnits { get; init; }

    /// <summary>
    /// The logger used to display messages.
    /// </summary>
    public IResultMessageLogger ResultMessageLogger { get; init; }

    public MotionSystem MotionSystem { get; init; }

    /// <summary>
    /// Returns true if the machine contains either a linear or rotational axis with the given name.
    /// </summary>
    /// <param name="axisName"></param>
    /// <returns></returns>
    public bool ContainsAxisName(string axisName) =>
        Entities.AxesLinear.Any(kvp => kvp.Value.Name == axisName) ||
        Entities.AxesRotational.Any(kvp => kvp.Value.Name == axisName);
}
