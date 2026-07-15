using UnitsNet;

namespace UnitsNetHelpers;

public enum Duration2Unit
{
    Seconds2,
    Minutes2,
    Hours2,
}

public readonly struct Duration2(double value, Duration2Unit unit) : IQuantity
{
    readonly Enum IQuantity.Unit => Unit;
    public Duration2Unit Unit { get; } = unit;
    public double Value { get; } = value;

    #region Conversion factors (to Second)

    private static double GetConversionFactor(Duration2Unit unit) => unit switch
    {
        Duration2Unit.Seconds2 => 1.0,
        Duration2Unit.Minutes2 => 3600.0, // 1/min2 = 3600.0/s2
        Duration2Unit.Hours2 => 12960000.0, // 1/hour2 = 12960000.0/s2
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    #endregion

    #region IQuantity

    private static readonly Duration2 Zero = new(0, Duration2Unit.Seconds2);

    public BaseDimensions Dimensions => new(
        length: 0,
        mass: 0,
        time: 2,
        current: 0,
        temperature: 0,
        amount: 0,
        luminousIntensity: 0);

    //public QuantityType Type => QuantityType.Undefined; // No built-in enum value; use Undefined

    public QuantityInfo QuantityInfo => new QuantityInfo(
        "Duration^2",
        typeof(Duration2Unit),
        [
            new UnitInfo<Duration2Unit>(Duration2Unit.Seconds2, "Seconds^2", BaseUnits.Undefined, "Second^2"),
            new UnitInfo<Duration2Unit>(Duration2Unit.Minutes2, "Minutes^2", BaseUnits.Undefined, "Minute^2"),
            new UnitInfo<Duration2Unit>(Duration2Unit.Hours2,   "Hours^2",   BaseUnits.Undefined, "Hour^2"  ),
        ],
        Duration2Unit.Seconds2,
        Zero,
        Dimensions);

    /// <summary>Convert this quantity to the given unit.</summary>
    public double As(Enum unit)
    {
        if (unit is Duration2Unit targetUnit)
        {
            // Convert: this → Second → target
            double valueInBaseUnit = Value * GetConversionFactor(Unit);
            return valueInBaseUnit / GetConversionFactor(targetUnit);
        }
        throw new ArgumentException("Must be of type Duration2Unit.", nameof(unit));
    }

    public double As(UnitSystem unitSystem) => throw new NotImplementedException();

    public IQuantity ToUnit(Enum unit)
    {
        if (unit is Duration2Unit targetUnit)
            return new Duration2(As(unit), targetUnit);
        throw new ArgumentException("Must be of type Duration2Unit.", nameof(unit));
    }

    public IQuantity ToUnit(UnitSystem unitSystem) => throw new NotImplementedException();

    public override string ToString() =>
        $"{Value} {UnitAbbreviationsCache.Default.GetDefaultAbbreviation(Unit)}";

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"{Value.ToString(format, formatProvider)} {UnitAbbreviationsCache.Default.GetDefaultAbbreviation(Unit)}";

    public string ToString(IFormatProvider? provider) =>
        $"{Value.ToString(provider)} {UnitAbbreviationsCache.Default.GetDefaultAbbreviation(Unit)}";

    public string ToString(IFormatProvider provider, int significantDigitsAfterRadix) =>
        $"{Value.ToString(provider)} {UnitAbbreviationsCache.Default.GetDefaultAbbreviation(Unit)}";

    public string ToString(IFormatProvider provider, string format, params object[] args) =>
        string.Format(provider, format, args);

    #endregion

    #region Convenience factory methods

    public static Duration2 FromSeconds2(double value) => new(value, Duration2Unit.Seconds2);
    public static Duration2 FromMinutes2(double value) => new(value, Duration2Unit.Minutes2);
    public static Duration2 FromHours2(double value) => new(value, Duration2Unit.Hours2);

    public bool Equals(IQuantity? other, IQuantity tolerance)
    {
        if (other is null) return false;
        if (other.QuantityInfo.UnitType != this.QuantityInfo.UnitType) return false;
        if (other.QuantityInfo.UnitType != tolerance.QuantityInfo.UnitType)
            throw new ArgumentException("Tolerance must be of the same quantity type.");

        // Convert both to the same unit (e.g., base unit) to compare
        double leftValue = other.As(other.Unit);
        double rightValue = this.As(other.Unit); // Convert right to left's unit
        double tolValue = tolerance.As(other.Unit);

        return Math.Abs(leftValue - rightValue) <= tolValue;
    }

    public Duration Sqrt()
    {
        return Duration.FromSeconds(Math.Sqrt(Seconds2));
    }

    // Convenience properties after construction
    public double Seconds2 => As(Duration2Unit.Seconds2);
    public double Minutes2 => As(Duration2Unit.Minutes2);
    public double Hours2 => As(Duration2Unit.Hours2);

    readonly QuantityValue IQuantity.Value => Value;

    #endregion

    #region Operators

    public static Duration2 operator +(Duration2 a, Duration2 b) =>
        new(a.Seconds2 + b.Seconds2, Duration2Unit.Seconds2);

    public static Duration2 operator -(Duration2 a, Duration2 b) =>
        new(a.Seconds2 - b.Seconds2, Duration2Unit.Seconds2);

    public static Duration2 operator *(double scalar, Duration2 q) =>
        new(scalar * q.Value, q.Unit);

    public static Duration2 operator *(Duration2 q, double scalar) =>
        new(q.Value * scalar, q.Unit);


    #endregion
}
