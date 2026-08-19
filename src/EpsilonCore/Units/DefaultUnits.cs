using UnitsNet;
using UnitsNet.Units;

namespace EpsilonCore.Units;

/// <summary>
/// Contains the units assumed to be used when creating units from doubles.
/// </summary>
public static class DefaultUnits
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static Temperature RoomTemp { get; } = Temperature.FromDegreesCelsius(20);
    public static LengthUnit LengthUnitDefault { get; } = LengthUnit.Millimeter;
    public static SpeedUnit SpeedUnitDefault { get; } = SpeedUnit.MillimeterPerSecond;
    public static AccelerationUnit AccelerationUnitDefault { get; } = AccelerationUnit.MillimeterPerSecondSquared;
    public static AngleUnit AngleUnitDefault { get; } = AngleUnit.Radian;
    public static RotationalSpeedUnit RotationalSpeedUnitDefault { get; } = RotationalSpeedUnit.RadianPerSecond;
    public static RotationalAccelerationUnit RotationalAccelerationUnitDefault { get; } = RotationalAccelerationUnit.RadianPerSecondSquared;
    public static TemperatureUnit TemperatureUnitDefault { get; } = TemperatureUnit.Kelvin;


    public static double ToDefault(Length length)

        => length.As(LengthUnitDefault);
    public static double ToDefault(Speed speed)
        => speed.As(SpeedUnitDefault);
    public static double ToDefault(Acceleration acceleration)
        => acceleration.As(AccelerationUnitDefault);
    public static double ToDefault(Angle angle)
        => angle.As(AngleUnitDefault);
    public static double ToDefault(RotationalSpeed rotationalSpeed)
        => rotationalSpeed.As(RotationalSpeedUnitDefault);
    public static double ToDefault(RotationalAcceleration rotationalAcceleration)
        => rotationalAcceleration.As(RotationalAccelerationUnitDefault);
    public static double ToDefault(Temperature temperature)
        => temperature.As(TemperatureUnitDefault);

    public static double? ToDefault(Length? length)
        => length.HasValue ? length.Value.As(LengthUnitDefault) : null;
    public static double? ToDefault(Speed? speed)
        => speed.HasValue ? speed.Value.As(SpeedUnitDefault) : null;
    public static double? ToDefault(Acceleration? acceleration)
        => acceleration.HasValue ? acceleration.Value.As(AccelerationUnitDefault) : null;
    public static double? ToDefault(Angle? angle)
        => angle.HasValue ? angle.Value.As(AngleUnitDefault) : null;
    public static double? ToDefault(RotationalSpeed? rotationalSpeed)
        => rotationalSpeed.HasValue ? rotationalSpeed.Value.As(RotationalSpeedUnitDefault) : null;
    public static double? ToDefault(RotationalAcceleration? rotationalAcceleration)
        => rotationalAcceleration.HasValue ? rotationalAcceleration.Value.As(RotationalAccelerationUnitDefault) : null;
    public static double? ToDefault(Temperature? temperature)
        => temperature.HasValue ? temperature.Value.As(TemperatureUnitDefault) : null;

    public static IList<double> ToDefault(IList<Length> lengths)
        => [.. lengths.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<Speed> speeds)
        => [.. speeds.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<Acceleration> accelerations)
        => [.. accelerations.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<Angle> angles)
        => [.. angles.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<RotationalSpeed> rotationalSpeeds)
        => [.. rotationalSpeeds.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<RotationalAcceleration> rotationalAccelerations)
        => [.. rotationalAccelerations.Select(x => ToDefault(x))];
    public static IList<double> ToDefault(IList<Temperature> temperatures)
        => [.. temperatures.Select(x => ToDefault(x))];

    public static IList<double?> ToDefault(IList<Length?> lengths)
        => [.. lengths.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<Speed?> speeds)
        => [.. speeds.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<Acceleration?> accelerations)
        => [.. accelerations.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<Angle?> angles)
        => [.. angles.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<RotationalSpeed?> rotationalSpeeds)
        => [.. rotationalSpeeds.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<RotationalAcceleration?> rotationalAccelerations)
        => [.. rotationalAccelerations.Select(x => ToDefault(x))];
    public static IList<double?> ToDefault(IList<Temperature?> temperatures)
        => [.. temperatures.Select(x => ToDefault(x))];

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
