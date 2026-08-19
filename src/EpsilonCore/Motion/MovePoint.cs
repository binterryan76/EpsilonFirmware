using EpsilonCore.Actuator;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using UnitsNet;
using UnitsNetHelpers;

namespace EpsilonCore.Motion;

/// <summary>
/// Represents a sample point along a linear move. 
/// </summary>
public class MovePoint
{
    /// <summary>
    /// Instantiates a new <see cref="MovePoint"/>.
    /// </summary>
    /// <param name="pathRatio"><inheritdoc cref="PathRatio"/></param>
    /// <param name="axisPositionsLinear"></param>
    /// <param name="axisPositionsRotational"></param>
    /// <param name="actuatorPositionsLinear"></param>
    /// <param name="actuatorPositionsRotational"></param>
    public MovePoint(
        double pathRatio,
        IReadOnlyList<Length?> axisPositionsLinear,
        IReadOnlyList<Angle?> axisPositionsRotational,
        IReadOnlyList<Length?> actuatorPositionsLinear,
        IReadOnlyList<Angle?> actuatorPositionsRotational)
    {
        PathRatio = pathRatio;
        AxisPositionsLinear = axisPositionsLinear;
        AxisPositionsRotational = axisPositionsRotational;
        ActuatorPositionsLinear = actuatorPositionsLinear;
        ActuatorPositionsRotational = actuatorPositionsRotational;
        ActuatorPositionDerivatives1Linear = [.. Enumerable.Repeat<Length?>(null, ActuatorPositionsLinear.Count)];
        ActuatorPositionDerivatives1Rotational = [.. Enumerable.Repeat<Angle?>(null, ActuatorPositionsRotational.Count)];
        ActuatorPositionDerivatives2Linear = [.. Enumerable.Repeat<Length?>(null, ActuatorPositionsLinear.Count)];
        ActuatorPositionDerivatives2Rotational = [.. Enumerable.Repeat<Angle?>(null, ActuatorPositionsRotational.Count)];
    }

    /// <summary>
    /// Number 0-1 representing how far the point lies on the given move.
    /// This is the function parameter s and derivatives are calculated with respect to s.
    /// 0 means it's the first point 0.5 is the midpoint and 1 is the end point.
    /// </summary>
    public double PathRatio { get; init; }

    /// <summary>
    /// Time it will take to move from the first point of a move to this point.
    /// </summary>
    public Duration Time { get; set; } = Duration.Zero;

    /// <summary>
    /// Positions of all the <see cref="AxisLinear"/> at this point.
    /// A position will be null if the axis isn't homed.
    /// </summary>
    public IReadOnlyList<Length?> AxisPositionsLinear { get; init; }

    /// <summary>
    /// Positions of all the <see cref="AxisRotational"/> at this point.
    /// A position will be null if the axis isn't homed.
    /// </summary>
    public IReadOnlyList<Angle?> AxisPositionsRotational { get; init; }

    /// <summary>
    /// Positions of all the <see cref="IActuatorLinear"/> at this point.
    /// A position will be null if the actuator is involved with an axis that isn't homed.
    /// This will be calculated with inverse kinematics.
    /// </summary>
    public IReadOnlyList<Length?> ActuatorPositionsLinear { get; init; }

    /// <summary>
    /// Positions of all the <see cref="IActuatorRotational"/> at this point.
    /// A position will be null if the actuator is involved with an axis that isn't homed.
    /// This will be calculated with inverse kinematics.
    /// </summary>
    public IReadOnlyList<Angle?> ActuatorPositionsRotational { get; init; }

    /// <summary>
    /// First derivative of actuator positions linear with respect to path ratio (dx/ds).
    /// This has units of length because dx has units of length and ds is unitless because s is just a function parameter.
    /// </summary>
    public List<Length?> ActuatorPositionDerivatives1Linear { get; init; }

    /// <summary>
    /// First derivative of actuator positions rotational with respect to path ratio (dƟ/ds).
    /// This has units of angle because dƟ has units of angle and ds is unitless because s is just a function parameter.
    /// </summary>
    public List<Angle?> ActuatorPositionDerivatives1Rotational { get; init; }

    /// <summary>
    /// Second derivative of actuator positions linear with respect to path ratio (d2x/ds2).
    /// This has units of length because dx has units of length and ds is unitless because s is just a function parameter.
    /// </summary>
    public List<Length?> ActuatorPositionDerivatives2Linear { get; init; }

    /// <summary>
    /// Second derivative of actuator positions rotational with respect to path ratio (d2Ɵ/ds2).
    /// This has units of angle because dƟ has units of angle and ds is unitless because s is just a function parameter.
    /// </summary>
    public List<Angle?> ActuatorPositionDerivatives2Rotational { get; init; }

    /// <summary>
    /// This is the maximum height of the ds/dt trapezoid at the given point.
    /// It is first set to the minimum value of ds/dt computed by dividing 
    /// dx/dt by dx/ds or by dividing dƟ/dt by dƟ/ds for every actuator.
    /// It may then be set even lower to obey junction speed constraints.
    /// </summary>
    public Frequency ActuatorLimitedDsDt { get; set; } = Frequency.Zero;

    /// <summary>
    /// This is the actual value of ds/dt at this point calculated by doing a forward and backward pass
    /// and taking the minimum value to ensure it can be reached from the starting speed while also
    /// still being able to reach the final speed.
    /// This value may never exceed <see cref="ActuatorLimitedDsDt"/>.
    /// </summary>
    public Frequency ActualDsDt { get; set; } = Frequency.Zero;

    /// <summary>
    /// Normalizes the actuator first derivatives based on the actuator's square corner speed.
    /// Normalizing by square corner speed is a design choice, not a physical law: there is no
    /// single "correct" way to compare a rotary actutors's motion against a linear actuator's
    /// motion, since they are not physically commensurate quantities. Square corner speed was chosen
    /// because if an actuator cannot handle a large square corner speed, it should probably be the one 
    /// limiting the square corner speed for the other actuators.
    /// </summary>
    /// <param name="kinematicSystem"></param>
    /// <returns></returns>
    public List<Duration?> NormalizedActuatorDerivatives(CompositeKinematicSystem kinematicSystem)
    {
        List<Duration?> normalizedActuatorDerivatives1 =
            new(kinematicSystem.ActuatorsLinear.Count + kinematicSystem.ActuatorsRotational.Count);

        normalizedActuatorDerivatives1.AddRange(
            ActuatorPositionDerivatives1Linear
            .Zip(kinematicSystem.ActuatorsLinear,
                (derivative, actuator) => derivative / actuator.SquareCornerSpeed));

        normalizedActuatorDerivatives1.AddRange(
            ActuatorPositionDerivatives1Rotational
            .Zip(kinematicSystem.ActuatorsRotational,
                (derivative, actuator) => derivative / actuator.SquareCornerSpeed));

        return normalizedActuatorDerivatives1;
    }

}
