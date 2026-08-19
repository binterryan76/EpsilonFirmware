using UnitsNet;
using UnitsNet.Units;


namespace UnitsNetHelpers;

public static class Helpers
{
    #region Function Aliases
    public static Length Mm(double value)
        => Length.FromMillimeters(value);

    public static Speed MmPerS(double value)
        => Speed.FromMillimetersPerSecond(value);

    public static Acceleration MmPerS2(double value)
        => Acceleration.FromMillimetersPerSecondSquared(value);

    public static Angle Radians(double value)
        => Angle.FromRadians(value);

    public static RotationalSpeed RadiansPerS(double value)
        => RotationalSpeed.FromRadiansPerSecond(value);

    public static RotationalAcceleration RadiansPerSs(double value)
        => RotationalAcceleration.FromRadiansPerSecondSquared(value);

    public static Temperature Celcius(double value)
        => Temperature.FromDegreesCelsius(value);

    public static Duration Sec(double value)
        => Duration.FromSeconds(value);
    #endregion

    #region Nullable Constructors
    public static Length? From(double? value, LengthUnit unit)
        => value is null ? null : Length.From(value.Value, unit);

    public static Speed? From(double? value, SpeedUnit unit)
        => value is null ? null : Speed.From(value.Value, unit);

    public static Acceleration? From(double? value, AccelerationUnit unit)
        => value is null ? null : Acceleration.From(value.Value, unit);

    public static Angle? From(double? value, AngleUnit unit)
        => value is null ? null : Angle.From(value.Value, unit);

    public static RotationalSpeed? From(double? value, RotationalSpeedUnit unit)
        => value is null ? null : RotationalSpeed.From(value.Value, unit);

    public static RotationalAcceleration? From(double? value, RotationalAccelerationUnit unit)
        => value is null ? null : RotationalAcceleration.From(value.Value, unit);

    public static Temperature? From(double? value, TemperatureUnit unit)
        => value is null ? null : Temperature.From(value.Value, unit);

    public static Length? FromMillimeters(double? value)
        => value is null ? null : Length.FromMillimeters(value.Value);

    public static Speed? FromMillimetersPerSecond(double? value)
        => value is null ? null : Speed.FromMillimetersPerSecond(value.Value);

    public static Acceleration? FromMillimetersPerSecondSquared(double? value)
        => value is null ? null : Acceleration.FromMillimetersPerSecondSquared(value.Value);

    public static Angle? FromRadians(double? value)
        => value is null ? null : Angle.FromRadians(value.Value);

    public static RotationalSpeed? FromRadiansPerSecond(double? value)
        => value is null ? null : RotationalSpeed.FromRadiansPerSecond(value.Value);

    public static RotationalAcceleration? FromRadiansPerSecondSquared(double? value)
        => value is null ? null : RotationalAcceleration.FromRadiansPerSecondSquared(value.Value);

    #endregion



    #region Nullable Arrays
    public static Length?[] ToLengths(this double?[] lengths, LengthUnit unit)
    {
        return [.. lengths.Select<double?, Length?>(len => len.HasValue ? Length.From(len.Value, unit) : null)];
    }

    public static Angle?[] ToAngles(this double?[] angles, AngleUnit unit)
    {
        return [.. angles.Select<double?, Angle?>(a => a.HasValue ? Angle.From(a.Value, unit) : null)];
    }
    #endregion

    #region Geometry

    public static Length Distance(Length x, Length y, Length z)
    {
        return Length.FromMeters(Math.Sqrt(
            Math.Pow(x.Meters, 2) +
            Math.Pow(y.Meters, 2) +
            Math.Pow(z.Meters, 2)));
    }

    public static Length Distance(IEnumerable<Length> components)
    {
        double sumOfSquares = 0.0;

        foreach (Length component in components)
            sumOfSquares += Math.Pow(component.Meters, 2);

        return Length.FromMeters(Math.Sqrt(sumOfSquares));
    }

    public static Length Distance(IEnumerable<Length?> components)
    {
        double sumOfSquares = 0.0;

        foreach (Length? component in components)
            sumOfSquares += component.HasValue ? Math.Pow(component.Value.Meters, 2) : 0.0;

        return Length.FromMeters(Math.Sqrt(sumOfSquares));
    }

    public static Angle Distance(IEnumerable<Angle> components)
    {
        double sumOfSquares = 0.0;

        foreach (Angle component in components)
            sumOfSquares += Math.Pow(component.Radians, 2);

        return Angle.FromRadians(Math.Sqrt(sumOfSquares));
    }

    public static Angle Distance(IEnumerable<Angle?> components)
    {
        double sumOfSquares = 0.0;

        foreach (Angle? component in components)
            sumOfSquares += component.HasValue ? Math.Pow(component.Value.Radians, 2) : 0.0;

        return Angle.FromRadians(Math.Sqrt(sumOfSquares));
    }

    public static Duration Distance(IEnumerable<Duration> components)
    {
        double sumOfSquares = 0.0;

        foreach (Duration component in components)
            sumOfSquares += Math.Pow(component.Seconds, 2);

        return Duration.FromSeconds(Math.Sqrt(sumOfSquares));
    }

    public static Duration Distance(IEnumerable<Duration?> components)
    {
        double sumOfSquares = 0.0;

        foreach (Duration? component in components)
            sumOfSquares += component.HasValue ? Math.Pow(component.Value.Seconds, 2) : 0.0;

        return Duration.FromSeconds(Math.Sqrt(sumOfSquares));
    }

    public static Length Sqrt(Area area)
    {
        return Length.FromMeters(Math.Sqrt(area.SquareMeters));
    }

    #endregion

    // I don't know why but the built-in abs function is slow.
    #region Absolute Value Functions
    public static Duration FastAbs(this Duration quantity) =>
        Duration.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Frequency FastAbs(this Frequency quantity) =>
        Frequency.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Frequency2 FastAbs(this Frequency2 quantity) =>
        Frequency2.FromHertzSquared(Math.Abs(quantity.HertzSquared));

    public static Length FastAbs(this Length quantity) =>
        Length.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Speed FastAbs(this Speed quantity) =>
        Speed.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Acceleration FastAbs(this Acceleration quantity) =>
        Acceleration.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Angle FastAbs(this Angle quantity) =>
        Angle.From(Math.Abs(quantity.Value), quantity.Unit);

    public static RotationalSpeed FastAbs(this RotationalSpeed quantity) =>
        RotationalSpeed.From(Math.Abs(quantity.Value), quantity.Unit);

    public static RotationalAcceleration FastAbs(this RotationalAcceleration quantity) =>
        RotationalAcceleration.From(Math.Abs(quantity.Value), quantity.Unit);

    public static Duration FastAbs(this Duration? quantity) =>
        quantity.HasValue ? Duration.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Duration.Zero;

    public static Frequency FastAbs(this Frequency? quantity) =>
        quantity.HasValue ? Frequency.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Frequency.Zero;

    public static Frequency2 FastAbs(this Frequency2? quantity) =>
        quantity.HasValue ? Frequency2.FromHertzSquared(Math.Abs(quantity.Value.HertzSquared)) : Frequency2.Zero;

    public static Length FastAbs(this Length? quantity) =>
        quantity.HasValue ? Length.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Length.Zero;

    public static Speed FastAbs(this Speed? quantity) =>
        quantity.HasValue ? Speed.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Speed.Zero;

    public static Acceleration FastAbs(this Acceleration? quantity) =>
        quantity.HasValue ? Acceleration.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Acceleration.Zero;

    public static Angle FastAbs(this Angle? quantity) =>
        quantity.HasValue ? Angle.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : Angle.Zero;

    public static RotationalSpeed FastAbs(this RotationalSpeed? quantity) =>
        quantity.HasValue ? RotationalSpeed.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : RotationalSpeed.Zero;

    public static RotationalAcceleration FastAbs(this RotationalAcceleration? quantity) =>
        quantity.HasValue ? RotationalAcceleration.From(Math.Abs(quantity.Value.Value), quantity.Value.Unit) : RotationalAcceleration.Zero;
    #endregion

    #region Extension Operators
    extension(RotationalSpeed r)
    {
        public static Duration operator /(RotationalSpeed rotationalSpeed, RotationalAcceleration acceleration)
            => Duration.FromSeconds(rotationalSpeed.RadiansPerSecond / acceleration.RadiansPerSecondSquared);

        public static RotationalAcceleration operator /(RotationalSpeed rotationalSpeed, Duration duration)
            => RotationalAcceleration.FromRadiansPerSecondSquared(rotationalSpeed.RadiansPerSecond / duration.Seconds);

        public static Frequency operator /(RotationalSpeed rotationalSpeed, Angle angle)
            => Frequency.FromHertz(rotationalSpeed.RadiansPerSecond / angle.Radians);

        public static RotationalAcceleration operator *(RotationalSpeed rotationalSpeed, Frequency frequency)
            => RotationalAcceleration.FromRadiansPerSecondSquared(rotationalSpeed.RadiansPerSecond * frequency.Hertz);
    }

    extension(RotationalAcceleration a)
    {
        public static RotationalSpeed operator *(RotationalAcceleration rotationalAcceleration, Duration duration)
            => RotationalSpeed.FromRadiansPerSecond(rotationalAcceleration.RadiansPerSecondSquared * duration.Seconds);

        public static Frequency2 operator /(RotationalAcceleration rotationalAcceleration, Angle angle)
            => Frequency2.FromHertzSquared(rotationalAcceleration.RadiansPerSecondSquared / angle.Radians);

    }

    extension(Angle a)
    {
        public static Duration operator /(Angle angle, RotationalSpeed rotationalSpeed)
            => Duration.FromSeconds(angle.Radians / rotationalSpeed.RadiansPerSecond);

        public static Duration2 operator /(Angle angle, RotationalAcceleration rotationalAcceleration)
            => Duration2.FromSeconds2(angle.Radians / rotationalAcceleration.RadiansPerSecondSquared);

        public static RotationalSpeed operator *(Angle angle, Frequency frequency)
            => RotationalSpeed.FromRadiansPerSecond(angle.Radians * frequency.Hertz);
    }

    extension(Length l)
    {
        public static double operator *(Length length, ReciprocalLength reciprocalLength)
            => length.Meters * reciprocalLength.InverseMeters;

        public static Duration2 operator /(Length length, Acceleration acceleration)
            => Duration2.FromSeconds2(length.Meters / acceleration.MetersPerSecondSquared);

        public static Speed operator *(Length length, Frequency frequency)
            => Speed.FromMetersPerSecond(length.Meters * frequency.Hertz);

        public static Frequency2 operator /(Length length, Speed speed)
            => Frequency2.FromHertzSquared(length.Meters / speed.MetersPerSecond);
    }

    extension(Speed s)
    {
        public static Frequency operator /(Speed speed, Length length)
            => Frequency.FromHertz(speed.MetersPerSecond / length.Meters);

        public static Acceleration operator *(Speed speed, Frequency frequency)
            => Acceleration.FromMetersPerSecondSquared(speed.MetersPerSecond * frequency.Hertz);
    }

    extension(Acceleration a)
    {
        public static Frequency2 operator /(Acceleration acceleration, Length length)
            => Frequency2.FromHertzSquared(acceleration.MetersPerSecondSquared / length.Meters);
    }

    extension(Frequency f)
    {
        public static Frequency2 operator *(Frequency frequency1, Frequency frequency2)
            => Frequency2.FromHertzSquared(frequency1.Hertz * frequency2.Hertz);

        public static Duration operator /(double scalar, Frequency frequency)
            => Duration.FromSeconds(scalar / frequency.Hertz);
    }
    #endregion

    #region extension methods
    public static T Min<T>(T quantity1, T quantity2) where T : IQuantity
            => quantity1.Value <= quantity2.Value ? quantity1 : quantity2;

    public static T Max<T>(T quantity1, T quantity2) where T : IQuantity
            => quantity1.Value >= quantity2.Value ? quantity1 : quantity2;
    #endregion
}
