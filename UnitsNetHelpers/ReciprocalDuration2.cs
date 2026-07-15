using UnitsNet;

namespace UnitsNetHelpers;

public enum ReciprocalDuration2Unit
{
    ReciprocalSeconds2,
    ReciprocalMinutes2,
    ReciprocalHours2,
}

public readonly struct ReciprocalDuration2(double value, ReciprocalDuration2Unit unit) : IQuantity
{
    readonly Enum IQuantity.Unit => Unit;
    public ReciprocalDuration2Unit Unit { get; } = unit;
    public double Value { get; } = value;

    #region Conversion factors (to ReciprocalSecond)

    private static double GetConversionFactor(ReciprocalDuration2Unit unit) => unit switch
    {
        ReciprocalDuration2Unit.ReciprocalSeconds2 => 1.0,
        ReciprocalDuration2Unit.ReciprocalMinutes2 => 3600.0, // 1/min2 = 3600.0/s2
        ReciprocalDuration2Unit.ReciprocalHours2 => 12960000.0, // 1/hour2 = 12960000.0/s2
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    #endregion

    #region IQuantity

    private static readonly ReciprocalDuration2 Zero = new(0, ReciprocalDuration2Unit.ReciprocalSeconds2);

    public BaseDimensions Dimensions => new(
        length: 0,
        mass: 0,
        time: -2,
        current: 0,
        temperature: 0,
        amount: 0,
        luminousIntensity: 0);

    //public QuantityType Type => QuantityType.Undefined; // No built-in enum value; use Undefined

    public QuantityInfo QuantityInfo => new QuantityInfo(
        "Reciprocal Duration^2",
        typeof(ReciprocalDuration2Unit),
        [
            new UnitInfo<ReciprocalDuration2Unit>(ReciprocalDuration2Unit.ReciprocalSeconds2, "Reciprocal Seconds^2", BaseUnits.Undefined, "Reciprocal Second^2"),
            new UnitInfo<ReciprocalDuration2Unit>(ReciprocalDuration2Unit.ReciprocalMinutes2, "Reciprocal Minutes^2", BaseUnits.Undefined, "Reciprocal Minute^2"),
            new UnitInfo<ReciprocalDuration2Unit>(ReciprocalDuration2Unit.ReciprocalHours2,   "Reciprocal Hours^2",   BaseUnits.Undefined, "Reciprocal Hour^2"  ),
        ],
        ReciprocalDuration2Unit.ReciprocalSeconds2,
        Zero,
        Dimensions);

    /// <summary>Convert this quantity to the given unit.</summary>
    public double As(Enum unit)
    {
        if (unit is ReciprocalDuration2Unit targetUnit)
        {
            // Convert: this → ReciprocalSecond → target
            double valueInBaseUnit = Value * GetConversionFactor(Unit);
            return valueInBaseUnit / GetConversionFactor(targetUnit);
        }
        throw new ArgumentException("Must be of type ReciprocalDuration2Unit.", nameof(unit));
    }

    public double As(UnitSystem unitSystem) => throw new NotImplementedException();

    public IQuantity ToUnit(Enum unit)
    {
        if (unit is ReciprocalDuration2Unit targetUnit)
            return new ReciprocalDuration2(As(unit), targetUnit);
        throw new ArgumentException("Must be of type ReciprocalDuration2Unit.", nameof(unit));
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

    public static ReciprocalDuration2 FromReciprocalSeconds2(double value) => new(value, ReciprocalDuration2Unit.ReciprocalSeconds2);
    public static ReciprocalDuration2 FromReciprocalMinutes2(double value) => new(value, ReciprocalDuration2Unit.ReciprocalMinutes2);
    public static ReciprocalDuration2 FromReciprocalHours2(double value) => new(value, ReciprocalDuration2Unit.ReciprocalHours2);

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

    public ReciprocalDuration Sqrt()
    {
        return ReciprocalDuration.FromReciprocalSeconds(Math.Sqrt(ReciprocalSeconds2));
    }

    // Convenience properties after construction
    public double ReciprocalSeconds2 => As(ReciprocalDuration2Unit.ReciprocalSeconds2);
    public double ReciprocalMinutes2 => As(ReciprocalDuration2Unit.ReciprocalMinutes2);
    public double ReciprocalHours2 => As(ReciprocalDuration2Unit.ReciprocalHours2);

    readonly QuantityValue IQuantity.Value => Value;

    #endregion

    #region Operators

    public static ReciprocalDuration2 operator +(ReciprocalDuration2 a, ReciprocalDuration2 b) =>
        new(a.ReciprocalSeconds2 + b.ReciprocalSeconds2, ReciprocalDuration2Unit.ReciprocalSeconds2);

    public static ReciprocalDuration2 operator -(ReciprocalDuration2 a, ReciprocalDuration2 b) =>
        new(a.ReciprocalSeconds2 - b.ReciprocalSeconds2, ReciprocalDuration2Unit.ReciprocalSeconds2);

    public static ReciprocalDuration2 operator *(double scalar, ReciprocalDuration2 q) =>
        new(scalar * q.Value, q.Unit);

    public static ReciprocalDuration2 operator *(ReciprocalDuration2 q, double scalar) =>
        new(q.Value * scalar, q.Unit);

    #endregion
}
