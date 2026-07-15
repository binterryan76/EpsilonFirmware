using UnitsNet;
using UnitsNet.Units;

namespace EpsilonCore.Units;

/// <summary>
/// Because microcontrollers run lower level code on weaker hardware, adding a 
/// unit system there will be impractical so these units will be used for 
/// communication with microcontrollers.
/// </summary>
internal static class CommunicationUnits
{
    public static LengthUnit LengthUnitCommunication { get; } = LengthUnit.Millimeter;
    public static SpeedUnit SpeedUnitCommunication { get; } = SpeedUnit.MillimeterPerSecond;
    public static AccelerationUnit AccelerationUnitCommunication { get; } = AccelerationUnit.MillimeterPerSecondSquared;
    public static AngleUnit AngleUnitCommunication { get; } = AngleUnit.Radian;
    public static RotationalSpeedUnit RotationalSpeedUnitCommunication { get; } = RotationalSpeedUnit.RadianPerSecond;
    public static RotationalAccelerationUnit RotationalAccelerationUnitCommunication { get; } = RotationalAccelerationUnit.RadianPerSecondSquared;
    public static TemperatureUnit TemperatureUnitCommunication { get; } = TemperatureUnit.Kelvin;
    public static DurationUnit DurationUnitCommunication { get; } = DurationUnit.Second;

    public static double ToCommunicationDouble(Length length)
        => length.As(LengthUnitCommunication);

    public static double ToCommunicationDouble(Speed speed)
        => speed.As(SpeedUnitCommunication);

    public static double ToCommunicationDouble(Acceleration acceleration)
        => acceleration.As(AccelerationUnitCommunication);

    public static double ToCommunicationDouble(Angle angle)
        => angle.As(AngleUnitCommunication);

    public static double ToCommunicationDouble(RotationalSpeed rotationalSpeed)
        => rotationalSpeed.As(RotationalSpeedUnitCommunication);

    public static double ToCommunicationDouble(RotationalAcceleration rotationalAcceleration)
        => rotationalAcceleration.As(RotationalAccelerationUnitCommunication);

    public static double ToCommunicationDouble(Duration duration)
        => duration.As(DurationUnitCommunication);

    public static double ToCommunicationDouble(Temperature temp)
        => temp.As(TemperatureUnitCommunication);

    public static Length ToLength(double value)
        => new(value, LengthUnitCommunication);

    public static Speed ToSpeed(double value)
        => new(value, SpeedUnitCommunication);

    public static Acceleration ToAcceleration(double value)
        => new(value, AccelerationUnitCommunication);

    public static Angle ToAngle(double value)
        => new(value, AngleUnitCommunication);

    public static RotationalSpeed ToRotationalSpeed(double value)
        => new(value, RotationalSpeedUnitCommunication);

    public static RotationalAcceleration ToRotationalAcceleration(double value)
        => new(value, RotationalAccelerationUnitCommunication);

    public static Temperature ToTemperature(double value)
        => new(value, TemperatureUnitCommunication);

    public static Duration ToDuration(double value)
        => new(value, DurationUnitCommunication);
}