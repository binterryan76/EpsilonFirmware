using EpsilonCore.Actuator;
using EpsilonCore.Motion.Axis;
using UnitsNet;

namespace EpsilonCore.Motion.Kinematics;

/// <summary>
/// Used to map actuator positions to and from axis positions.
/// </summary>
public interface IKinematics
{
    /// <inheritdoc cref="Machines.IEntity.Name"/>
    public string Name { get; }

    /// <summary>
    /// Number of <see cref="AxisLinear"/> objects involved in the kinematics.
    /// </summary>
    public uint AxisLinearCount { get; }

    /// <summary>
    /// Number of <see cref="AxisRotational"/> objects involved in the kinematics.
    /// </summary>
    public uint AxisRotationalCount { get; }

    /// <summary>
    /// Number of <see cref="IActuatorLinear"/> objects involved in the kinematics.
    /// </summary>
    public uint ActuatorLinearCount { get; }

    /// <summary>
    /// Number of <see cref="IActuatorRotational"/> objects involved in the kinematics.
    /// </summary>
    public uint ActuatorRotationalCount { get; }

    /// <summary>
    /// True if homing the first <see cref="EndstopSet"/> results in the 
    /// first actuator being homed, but not necessarily the first axis.
    /// Scara printers home actuators but core XY printers home axes.
    /// If true, the first <see cref="EndstopSet"/> corresponds to the first actuator.
    /// </summary>
    public bool AcceptsHomingActuators { get; }

    /// <summary>
    /// True if homing the first <see cref="EndstopSet"/> results in the 
    /// first axis being homed, but not necessarily the first actuator.
    /// Scara printers home actuators but core XY printers home axes.
    /// If true, the first <see cref="EndstopSet"/> corresponds to the first axis.
    /// </summary>
    public bool AcceptsHomingAxes { get; }

    /// <summary>
    /// Used to convert actuator positions to axis positions.
    /// Enters the axis positions into the provided memory.
    /// </summary>
    /// <param name="actuatorPositionsLinear"></param>
    /// <param name="actuatorPositionsRotational"></param>
    /// <param name="axisPositionsLinear"></param>
    /// <param name="axisPositionsRotational"></param>
    /// <returns></returns>
    public Exception? ForwardKinematics(
        Span<Length?> actuatorPositionsLinear,
        Span<Angle?> actuatorPositionsRotational,
        Memory<Length?> axisPositionsLinear,
        Memory<Angle?> axisPositionsRotational);

    /// <summary>
    /// Used to convert axis positions to actuator positions.
    /// Enters the actuator positions into the provided memory.
    /// </summary>
    /// <param name="actuatorPositionsLinear"></param>
    /// <param name="actuatorPositionsRotational"></param>
    /// <param name="axisPositionsLinear"></param>
    /// <param name="axisPositionsRotational"></param>
    /// <returns></returns>
    public Exception? InverseKinematics(
        Span<Length?> axisPositionsLinear,
        Span<Angle?> axisPositionsRotational,
        Memory<Length?> actuatorPositionsLinear,
        Memory<Angle?> actuatorPositionsRotational);

    /// <summary>
    /// How many actuators and axes there are.
    /// A 2D Core XY machine will have 2 degrees of freedom 
    /// and each actuator affects both axes.
    /// </summary>
    public uint DegreesOfFreedom { get; }

}
