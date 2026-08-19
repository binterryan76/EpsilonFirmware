using UnitsNet;
using UnitsNet.Units;

namespace EpsilonCore.Units;

/// <summary>
/// Contains the units to use when displaying in messages or in the UI.
/// </summary>
public class DisplayUnits
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public required LengthUnit LengthUnitDisplay { get; set; }
    public required SpeedUnit SpeedUnitDisplay { get; set; }
    public required AccelerationUnit AccelerationUnitDisplay { get; set; }
    public required AngleUnit AngleUnitDisplay { get; set; }
    public required RotationalSpeedUnit RotationalSpeedUnitDisplay { get; set; }
    public required RotationalAccelerationUnit RotationalAccelerationUnitDisplay { get; set; }
    public required TemperatureUnit TemperatureUnitDisplay { get; set; }

    public static DisplayUnits DefaultMetric { get; } = new()
    {
        LengthUnitDisplay = LengthUnit.Millimeter,
        SpeedUnitDisplay = SpeedUnit.MillimeterPerSecond,
        AccelerationUnitDisplay = AccelerationUnit.MillimeterPerSecondSquared,
        AngleUnitDisplay = AngleUnit.Degree,
        RotationalSpeedUnitDisplay = RotationalSpeedUnit.DegreePerSecond,
        RotationalAccelerationUnitDisplay = RotationalAccelerationUnit.DegreePerSecondSquared,
        TemperatureUnitDisplay = TemperatureUnit.DegreeCelsius,
    };

    public static DisplayUnits DefaultImperial { get; } = new()
    {
        LengthUnitDisplay = LengthUnit.Inch,
        SpeedUnitDisplay = SpeedUnit.InchPerSecond,
        AccelerationUnitDisplay = AccelerationUnit.InchPerSecondSquared,
        AngleUnitDisplay = AngleUnit.Degree,
        RotationalSpeedUnitDisplay = RotationalSpeedUnit.DegreePerSecond,
        RotationalAccelerationUnitDisplay = RotationalAccelerationUnit.DegreePerSecondSquared,
        TemperatureUnitDisplay = TemperatureUnit.DegreeFahrenheit,
    };

    public Length ToLength(double lengthDefault)
        => Length.From(lengthDefault, DefaultUnits.LengthUnitDefault).ToUnit(LengthUnitDisplay);
    public Length? ToLength(double? lengthDefault)
        => UnitsNetHelpers.Helpers.From(lengthDefault, DefaultUnits.LengthUnitDefault)?.ToUnit(LengthUnitDisplay);
    public string ToDisplayLength(double lengthDefault)
        => ToLength(lengthDefault).ToString();
    public string ToDisplay(Length length)
        => length.As(LengthUnitDisplay).ToString();

    public Speed ToSpeed(double speedDefault)
        => Speed.From(speedDefault, DefaultUnits.SpeedUnitDefault).ToUnit(SpeedUnitDisplay);
    public Speed? ToSpeed(double? speedDefault)
        => UnitsNetHelpers.Helpers.From(speedDefault, DefaultUnits.SpeedUnitDefault)?.ToUnit(SpeedUnitDisplay);
    public string ToDisplaySpeed(double speedDefault)
        => ToSpeed(speedDefault).ToString();
    public string ToDisplay(Speed speed)
        => speed.As(SpeedUnitDisplay).ToString();

    public Acceleration ToAcceleration(double accelerationDefault)
        => Acceleration.From(accelerationDefault, DefaultUnits.AccelerationUnitDefault).ToUnit(AccelerationUnitDisplay);
    public Acceleration? ToAcceleration(double? accelerationDefault)
        => UnitsNetHelpers.Helpers.From(accelerationDefault, DefaultUnits.AccelerationUnitDefault)?.ToUnit(AccelerationUnitDisplay);
    public string ToDisplayAcceleration(double accelerationDefault)
        => ToAcceleration(accelerationDefault).ToString();
    public string ToDisplay(Acceleration acceleration)
        => acceleration.As(AccelerationUnitDisplay).ToString();

    public Angle ToAngle(double angleDefault)
        => Angle.From(angleDefault, DefaultUnits.AngleUnitDefault).ToUnit(AngleUnitDisplay);
    public Angle? ToAngle(double? angleDefault)
        => UnitsNetHelpers.Helpers.From(angleDefault, DefaultUnits.AngleUnitDefault)?.ToUnit(AngleUnitDisplay);
    public string ToDisplayAngle(double angleDefault)
        => ToAngle(angleDefault).ToString();
    public string ToDisplay(Angle angle)
        => angle.As(AngleUnitDisplay).ToString();

    public RotationalSpeed ToRotationalSpeed(double rotationalSpeedDefault)
        => RotationalSpeed.From(rotationalSpeedDefault, DefaultUnits.RotationalSpeedUnitDefault).ToUnit(RotationalSpeedUnitDisplay);
    public RotationalSpeed? ToRotationalSpeed(double? rotationalSpeedDefault)
        => UnitsNetHelpers.Helpers.From(rotationalSpeedDefault, DefaultUnits.RotationalSpeedUnitDefault)?.ToUnit(RotationalSpeedUnitDisplay);
    public string ToDisplayRotationalSpeed(double rotationalSpeedDefault)
        => ToRotationalSpeed(rotationalSpeedDefault).ToString();
    public string ToDisplay(RotationalSpeed rotationalSpeed)
        => rotationalSpeed.As(RotationalSpeedUnitDisplay).ToString();

    public RotationalAcceleration ToRotationalAcceleration(double rotationalAccelerationDefault)
        => RotationalAcceleration.From(rotationalAccelerationDefault, DefaultUnits.RotationalAccelerationUnitDefault).ToUnit(RotationalAccelerationUnitDisplay);
    public RotationalAcceleration? ToRotationalAcceleration(double? rotationalAccelerationDefault)
        => UnitsNetHelpers.Helpers.From(rotationalAccelerationDefault, DefaultUnits.RotationalAccelerationUnitDefault)?.ToUnit(RotationalAccelerationUnitDisplay);
    public string ToDisplayRotationalAcceleration(double rotationalAccelerationDefault)
        => ToRotationalAcceleration(rotationalAccelerationDefault).ToString();
    public string ToDisplay(RotationalAcceleration rotationalAcceleration)
        => rotationalAcceleration.As(RotationalAccelerationUnitDisplay).ToString();

    public Temperature ToTemperature(double temperatureDefault)
        => Temperature.From(temperatureDefault, DefaultUnits.TemperatureUnitDefault).ToUnit(TemperatureUnitDisplay);
    public Temperature? ToTemperature(double? temperatureDefault)
        => UnitsNetHelpers.Helpers.From(temperatureDefault, DefaultUnits.TemperatureUnitDefault)?.ToUnit(TemperatureUnitDisplay);
    public string ToDisplayTemperature(double temperatureDefault)
        => ToTemperature(temperatureDefault).ToString();
    public string ToDisplay(Temperature temperature)
        => temperature.As(TemperatureUnitDisplay).ToString();

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
