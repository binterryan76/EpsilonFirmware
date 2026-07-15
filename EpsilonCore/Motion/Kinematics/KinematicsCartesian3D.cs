using EpsilonCore.Actuator;
using EpsilonCore.Motion.Axis;
using UnitsNet;

namespace EpsilonCore.Motion.Kinematics;

public record KinematicsCartesian3D : IKinematics
{
    public KinematicsCartesian3D(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Three <see cref="AxisLinear"/>s.
    /// </summary>
    public uint AxisLinearCount { get; } = 3;

    /// <summary>
    /// Zero <see cref="AxisRotational"/>s.
    /// </summary>
    public uint AxisRotationalCount { get; } = 0;

    /// <summary>
    /// Three <see cref="IActuatorLinear"/>s.
    /// </summary>
    public uint ActuatorLinearCount { get; } = 3;

    /// <summary>
    /// Zero <see cref="IActuatorRotational"/>s.
    /// </summary>
    public uint ActuatorRotationalCount { get; } = 0;

    /// <summary>
    /// Three degrees of freedom.
    /// </summary>
    public uint DegreesOfFreedom { get; } = 3;

    /// <inheritdoc cref="IKinematics.Name"/>
    public string Name { get; }

    /// <summary>
    /// Cartesian machines home axes since each endstop corresponds to only 1 axis.
    /// </summary>
    public bool AcceptsHomingActuators { get; } = false;

    /// <summary>
    /// Cartesian machines home axes since each endstop corresponds to only 1 axis.
    /// </summary>
    public bool AcceptsHomingAxes { get; } = true;

    /// <inheritdoc />
    public Exception? ForwardKinematics(
        Span<Length?> actuatorPositionsLinear,
        Span<Angle?> actuatorPositionsRotational,
        Memory<Length?> axisPositionsLinear,
        Memory<Angle?> axisPositionsRotational)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Exception? InverseKinematics(
        Span<Length?> axisPositionsLinear,
        Span<Angle?> axisPositionsRotational,
        Memory<Length?> actuatorPositionsLinear,
        Memory<Angle?> actuatorPositionsRotational)
    {
        throw new NotImplementedException();
    }
}
