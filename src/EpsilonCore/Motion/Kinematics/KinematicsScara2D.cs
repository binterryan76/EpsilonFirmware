using EpsilonCore.Actuator;
using EpsilonCore.Motion.Axis;
using UnitsNet;

namespace EpsilonCore.Motion.Kinematics;

public record KinematicsScara2D : IKinematics
{
    public KinematicsScara2D(string name, Length lengthProximal, Length lengthDistal)
    {
        Name = name;
        LengthProximal = lengthProximal;
        LengthDistal = lengthDistal;

        LengthProximalMm = lengthProximal.Millimeters;
        LengthDistalMm = lengthDistal.Millimeters;

        // precompute some frequently used constants
        L1L2 = LengthProximalMm * LengthDistalMm;
        L1Squared = Math.Pow(LengthProximalMm, 2);
        L2Squared = Math.Pow(LengthDistalMm, 2);
        L1SquaredL2Squared = Math.Pow(LengthProximalMm, 2) * Math.Pow(LengthDistalMm, 2);
        SumSquaredLengths = Math.Pow(LengthProximalMm, 2) + Math.Pow(LengthDistalMm, 2);
    }
    public string Name { get; init; }

    /// <summary>
    /// Two <see cref="AxisLinear"/>s.
    /// </summary>
    public uint AxisLinearCount { get; } = 2;

    /// <summary>
    /// Zero <see cref="AxisRotational"/>s.
    /// </summary>
    public uint AxisRotationalCount { get; } = 0;

    /// <summary>
    /// Zero <see cref="IActuatorLinear"/>s.
    /// </summary>
    public uint ActuatorLinearCount { get; } = 0;

    /// <summary>
    /// Two <see cref="IActuatorRotational"/>s.
    /// </summary>
    public uint ActuatorRotationalCount { get; } = 2;

    /// <summary>
    /// Two degrees of freedom.
    /// </summary>
    public uint DegreesOfFreedom { get; } = 2;

    /// <summary>
    /// Length of the proximal arm.
    /// </summary>
    public Length LengthProximal { get; init; }

    /// <summary>
    /// Length of the distal arm.
    /// </summary>
    public Length LengthDistal { get; init; }

    /// <summary>
    /// Scara machines home actuators since the machine won't know how to move 
    /// motors to home the X/Y axes without knowing it's position.
    /// </summary>
    public bool AcceptsHomingActuators { get; } = true;

    /// <summary>
    /// Scara machines home actuators since the machine won't know how to move 
    /// motors to home the X/Y axes without knowing it's position.
    /// </summary>
    public bool AcceptsHomingAxes { get; } = false;

    private double LengthProximalMm { get; }
    private double LengthDistalMm { get; }

    // precompute some frequently used constants
    private readonly double L1L2;
    private readonly double L1Squared;
    private readonly double L2Squared;
    private readonly double L1SquaredL2Squared;
    private readonly double SumSquaredLengths;

    public (Length x, Length y) ForwardKinematics(Angle angleProximal, Angle angleDistal)
    {
        Length x =
            (LengthProximal * Math.Cos(angleProximal.Radians)) +
            (LengthDistal * Math.Cos(angleProximal.Radians + angleDistal.Radians));
        Length y =
            (LengthProximal * Math.Sin(angleProximal.Radians)) +
            (LengthDistal * Math.Sin(angleProximal.Radians + angleDistal.Radians));
        return (x, y);
    }

    public (Angle angleProximal, Angle angleDistal) InverseKinematics(Length x, Length y)
    {
        double numerator = Math.Pow(x.Millimeters, 2) + Math.Pow(y.Millimeters, 2) - SumSquaredLengths;
        double denominator = 2 * L1L2;
        double ratio = numerator / denominator;

        if (ratio > 1 || ratio < -1)
            throw new ArgumentException($"X and Y are outside workspace. X: {x}, Y: {y}");

        Angle angleDistal = Angle.FromRadians(Math.Acos(ratio));

        double part1 = Math.Atan2(y.Millimeters, x.Millimeters);
        double part2 = Math.Atan2(LengthDistalMm * Math.Sin(angleDistal.Radians), LengthProximal.Millimeters + LengthDistalMm * Math.Cos(angleDistal.Radians));
        Angle angleProximal = Angle.FromRadians(part1 - part2);
        return (angleProximal, angleDistal);
    }

    /// <summary>
    /// Returns the rate of change of the actuators based on cartesian x and y speeds and the current proximal angle.
    /// </summary>
    /// <param name="vx"></param>
    /// <param name="vy"></param>
    /// <param name="angleProximal"></param>
    /// <returns></returns>
    public (RotationalSpeed speedProximal, RotationalSpeed speedDistal) InverseSpeeds(Speed velocityX, Speed velocityY, Angle angleProximal)
    {
        if (velocityX.Equals(Speed.Zero, Speed.FromMillimetersPerSecond(0.01)) && velocityY.Equals(Speed.Zero, Speed.FromMillimetersPerSecond(0.01)))
            return (RotationalSpeed.Zero, RotationalSpeed.Zero);

        double vx = velocityX.MillimetersPerSecond;
        double vy = velocityY.MillimetersPerSecond;
        double sumVelocitiesSquared = Math.Pow(vx, 2) + Math.Pow(vy, 2);

        // these are m^4
        double numerator = Math.Pow(-SumSquaredLengths + sumVelocitiesSquared, 2);
        double denominator = 4 * L1SquaredL2Squared;

        // this is m^2
        double fullDenominator = L1L2 * Math.Sqrt(1 - (numerator / denominator));

        // this is in Angle per meter
        double dThetaDx = -vx / fullDenominator;
        double dThetaDy = -vy / fullDenominator;

        // dθ/dt = dθ/dx * dx/dt + dθ/dy * dy/dt
        RotationalSpeed speedDistal = RotationalSpeed.FromRadiansPerSecond(dThetaDx * vx + dThetaDy * vy);

        double dTheta2Dx = -vy / sumVelocitiesSquared;
        double dTheta2Dy = vx / sumVelocitiesSquared;

        double cos = Math.Cos(angleProximal.Radians);
        double sin = Math.Sin(angleProximal.Radians);
        double cos2 = Math.Pow(cos, 2);
        double sin2 = Math.Pow(sin, 2);
        double sum = L2Squared * sin2 + L2Squared * cos2;
        double product = L1L2 * cos;
        double numerator2 = product + sum;
        double denominator2 = L1Squared + (2 * product) + sum;
        double dTheta2Dtheta = -numerator2 / denominator2;

        // dθ2/dt = dθ2/dx * dx/dt + dθ2/dy * dy/dt + dθ2/dθ * dθ/dt
        RotationalSpeed speedProximal =
            RotationalSpeed.FromRadiansPerSecond(dTheta2Dx * vx + dTheta2Dy * vy) +
            dTheta2Dtheta * speedDistal;

        return (speedProximal, speedDistal);
    }

    /// <inheritdoc />
    public Exception? ForwardKinematics(
        Span<Length?> actuatorPositionsLinear,
        Span<Angle?> actuatorPositionsRotational,
        Memory<Length?> axisPositionsLinear,
        Memory<Angle?> axisPositionsRotational)
    {
        Angle? angleProximal = actuatorPositionsRotational[0];
        Angle? angleDistal = actuatorPositionsRotational[1];

        if (angleProximal is null || angleDistal is null)
            return new ArgumentException($"Cannot calculate forward kinematics using null actuator positions.");

        (Length x, Length y) = ForwardKinematics(angleProximal.Value, angleDistal.Value);
        axisPositionsLinear.Span[0] = x;
        axisPositionsLinear.Span[1] = y;
        return null;
    }

    /// <inheritdoc />
    public Exception? InverseKinematics(
        Span<Length?> axisPositionsLinear,
        Span<Angle?> axisPositionsRotational,
        Memory<Length?> actuatorPositionsLinear,
        Memory<Angle?> actuatorPositionsRotational)
    {
        Length? x = axisPositionsLinear[0];
        Length? y = axisPositionsLinear[1];

        if (x is null || y is null)
            return new ArgumentException($"Cannot calculate inverse kinematics using null axis positions.");

        (Angle angleProximal, Angle angleDistal) = InverseKinematics(x.Value, y.Value);
        actuatorPositionsRotational.Span[0] = angleProximal;
        actuatorPositionsRotational.Span[1] = angleDistal;
        return null;
    }
}
