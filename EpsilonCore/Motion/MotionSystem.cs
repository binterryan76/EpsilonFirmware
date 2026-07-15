using EpsilonCore.Motion.Kinematics;
using UnitsNet;

namespace EpsilonCore.Motion;

public record MotionSystemPrecisions(
    Length PrecisionLinear,
    Angle PrecisionRotational,
    Duration PrecisionTemporal)
{
    public static MotionSystemPrecisions New => new(
        Length.FromMillimeters(0.0001),
        Angle.FromDegrees(0.0001),
        Duration.FromMicroseconds(1.0));
}

public record MotionSystemSpeeds
{
    public Speed SpeedLinearCurrent { get; }
    public RotationalSpeed SpeedRotationalCurrent { get; }

    public MotionSystemSpeeds()
    {
        SpeedLinearCurrent = Speed.FromMillimetersPerSecond(1.0);
        SpeedRotationalCurrent = RotationalSpeed.FromDegreesPerSecond(1.0);
    }
}


/// <summary>
/// A <see cref="Machines.Machine"/> has only one <see cref="MotionSystem"/> which
/// is composed of multiple <see cref="KinematicSystem.IKinematicSystem"/>s.
/// <see cref="MotionSystem"/> is not an <see cref="Machines.IEntity"/> because every 
/// machine has exactly 1 <see cref="MotionSystem"/> assigned in the constructor.
/// </summary>
public record MotionSystem
{
    public MotionSystem(MotionSystemPrecisions precisions)
    {
        Precisions = precisions;
    }
    public MotionSystemPrecisions Precisions { get; }
    public MotionSystemSpeeds Speeds = new();
    public CompositeKinematicSystem CompositeKinematicSystem { get; init; } = new();
}
