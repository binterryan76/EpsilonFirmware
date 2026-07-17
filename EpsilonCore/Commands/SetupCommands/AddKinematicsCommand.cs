using EpsilonCore.Actuator;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add a new set of kinematics to a machine.
/// A single machine has only one <see cref="MotionSystem"/> and 
/// <see cref="CompositeKinematicSystem"/> but can contain multiple <see cref="IKinematics"/>.
/// </summary>
/// <param name="Kinematics">Kinematic to add.</param>
/// <param name="LineNumber"><inheritdoc cref="ICommand.LineNumber"/></param>
/// <param name="ActuatorIdsLinear">The ids of the <see cref="IActuatorLinear"/>s used by the <paramref name="Kinematics"/>.</param>
/// <param name="ActuatorIdsRotational">The ids of the <see cref="IActuatorRotational"/>s used by the <paramref name="Kinematics"/>.</param>
/// <param name="AxisIdsLinear">The ids of the <see cref="AxisLinear"/>s used by the <paramref name="Kinematics"/>.</param>
/// <param name="AxisIdsRotational">The ids of the <see cref="AxisRotational"/>s used by the <paramref name="Kinematics"/>.</param>
/// <param name="EndstopSets">The <see cref="EndstopSet"/>s used by the <paramref name="Kinematics"/>.</param>
public record AddKinematicsCommand(
    IKinematics Kinematics,
    IReadOnlyList<uint> ActuatorIdsLinear,
    IReadOnlyList<uint> ActuatorIdsRotational,
    IReadOnlyList<uint> AxisIdsLinear,
    IReadOnlyList<uint> AxisIdsRotational,
    IReadOnlyList<EndstopSet> EndstopSets,
    uint? LineNumber = null)
    : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add kinematics '{Kinematics.Name}' to a motion system";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        CompositeKinematicSystem initialKinematicSystem = initialMachine.MotionSystem.CompositeKinematicSystem;
        Result<CompositeKinematicSystem> newKinematicSystem = initialKinematicSystem.AddKinematics(
            initialMachine,
            Kinematics,
            ActuatorIdsLinear,
            ActuatorIdsRotational,
            AxisIdsLinear,
            AxisIdsRotational,
            EndstopSets);

        if (newKinematicSystem.IsError)
            return QueuedCommand.Error(this, newKinematicSystem.Exception.Message);

        Machine resultantMachine = initialMachine with
        {
            MotionSystem = initialMachine.MotionSystem with
            {
                CompositeKinematicSystem = newKinematicSystem.Value

            }
        };

        return QueuedCommand.Success(this, resultantMachine);
    }
}