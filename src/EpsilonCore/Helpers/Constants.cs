using UnitsNet;

namespace EpsilonCore.Helpers;

public static class Constants
{
    public const string EPSILON_CORE_VERSION = "0.1";

    /// <summary>
    /// Maximum number of bytes that can be stored in the command buffer.
    /// This corresponds to EpsilonFollower Constants.h COMMAND_BUFFER_SIZE.
    /// </summary>
    public const int BOARD_COMMAND_BUFFER_SIZE = 2048;

    public static readonly double ROOT_2_MINUS_1 = Math.Sqrt(2.0) - 1.0;
    public const double VERY_SMALL_NUMBER = 1e-9;
    public static readonly Length VERY_SMALL_LENGTH = Length.FromMillimeters(VERY_SMALL_NUMBER);
    public static readonly Angle VERY_SMALL_ANGLE = Angle.FromRadians(VERY_SMALL_NUMBER);
    public static readonly Speed VERY_SMALL_SPEED = Speed.FromMillimetersPerSecond(VERY_SMALL_NUMBER);
    public static readonly RotationalSpeed VERY_SMALL_ROTATIONAL_SPEED = RotationalSpeed.FromRadiansPerSecond(VERY_SMALL_NUMBER);
    public static readonly Acceleration VERY_SMALL_ACCELERATION = Acceleration.FromMillimetersPerSecondSquared(VERY_SMALL_NUMBER);
    public static readonly RotationalAcceleration VERY_SMALL_ROTATIONAL_ACCELERATION = RotationalAcceleration.FromRadiansPerSecondSquared(VERY_SMALL_NUMBER);
    public static readonly Duration VERY_SMALL_DURATION = Duration.FromSeconds(VERY_SMALL_NUMBER);
    public static readonly Frequency VERY_SMALL_FREQUENCY = Frequency.FromHertz(VERY_SMALL_NUMBER);

    public static readonly Length LARGEST_LENGTH = Length.FromMillimeters(double.MaxValue);
    public static readonly Angle LARGEST_ANGLE = Angle.FromRadians(double.MaxValue);
    public static readonly Speed LARGEST_SPEED = Speed.FromMillimetersPerSecond(double.MaxValue);
    public static readonly RotationalSpeed LARGEST_ROTATIONAL_SPEED = RotationalSpeed.FromRadiansPerSecond(double.MaxValue);
    public static readonly Acceleration LARGEST_ACCELERATION = Acceleration.FromMillimetersPerSecondSquared(double.MaxValue);
    public static readonly RotationalAcceleration LARGEST_ROTATIONAL_ACCELERATION = RotationalAcceleration.FromRadiansPerSecondSquared(double.MaxValue);
    public static readonly Duration LARGEST_DURATION = Duration.FromSeconds(double.MaxValue);
    public static readonly Frequency LARGEST_FREQUENCY = Frequency.FromHertz(double.MaxValue);
}
