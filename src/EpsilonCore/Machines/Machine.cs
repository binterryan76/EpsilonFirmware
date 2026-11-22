using EpsilonCore.Display;
using EpsilonCore.Motion;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using EpsilonCore.Units;
using System.Collections.Immutable;

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

    /// <summary>
    /// Returns a copy of this machine with the given axes.
    /// Axes are stored both in <see cref="Entities"/> and in the <see cref="CompositeKinematicSystem"/>,
    /// so this keeps both copies in sync.
    /// </summary>
    /// <param name="axesLinear">All linear axes, keyed by id.</param>
    /// <param name="axesRotational">All rotational axes, keyed by id.</param>
    /// <returns>The machine with the new axes.</returns>
    public Machine WithAxes(
        IImmutableDictionary<uint, AxisLinear> axesLinear,
        IImmutableDictionary<uint, AxisRotational> axesRotational)
    {
        CompositeKinematicSystem kinematicSystem = MotionSystem.CompositeKinematicSystem;
        return this with
        {
            Entities = Entities with
            {
                AxesLinear = axesLinear,
                AxesRotational = axesRotational
            },
            MotionSystem = MotionSystem with
            {
                CompositeKinematicSystem = kinematicSystem with
                {
                    AxesLinear = kinematicSystem.AxesLinear
                        .Select(axis => axesLinear.TryGetValue(axis.Id, out AxisLinear? updated) ? updated : axis)
                        .ToImmutableList(),
                    AxesRotational = kinematicSystem.AxesRotational
                        .Select(axis => axesRotational.TryGetValue(axis.Id, out AxisRotational? updated) ? updated : axis)
                        .ToImmutableList()
                }
            }
        };
    }

    /// <summary>
    /// Returns a copy of this machine where no axis has a known position.
    /// Used when the true positions are lost, for example after an emergency stop.
    /// </summary>
    /// <returns>The machine with every axis position unknown.</returns>
    public Machine WithAllAxesUnhomed() => WithAxes(
        Entities.AxesLinear.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value with { Pos = null }),
        Entities.AxesRotational.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value with { Pos = null }));
}
