using UnitsNet;

namespace UnitsNetHelpers;

public enum ReciprocalDurationUnit
{
    ReciprocalSeconds,
    ReciprocalMinutes,
    ReciprocalHours,
}

public readonly struct ReciprocalDuration(double value, ReciprocalDurationUnit unit) : IQuantity
{
    readonly Enum IQuantity.Unit => Unit;
    public ReciprocalDurationUnit Unit { get; } = unit;
    public double Value { get; } = value;

    #region Conversion factors (to ReciprocalSecond)

    private static double GetConversionFactor(ReciprocalDurationUnit unit) => unit switch
    {
        ReciprocalDurationUnit.ReciprocalSeconds => 1.0,
        ReciprocalDurationUnit.ReciprocalMinutes => 60.0, // 1/min = 60/s
        ReciprocalDurationUnit.ReciprocalHours => 3600.0, // 1/hour = 3600.0/s
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    #endregion

    #region IQuantity

    private static readonly ReciprocalDuration Zero = new(0, ReciprocalDurationUnit.ReciprocalSeconds);

    public BaseDimensions Dimensions => new(
        length: 0,
        mass: 0,
        time: -1,
        current: 0,
        temperature: 0,
        amount: 0,
        luminousIntensity: 0);

    //public QuantityType Type => QuantityType.Undefined; // No built-in enum value; use Undefined

    public QuantityInfo QuantityInfo => new QuantityInfo(
        "Reciprocal Duration",
        typeof(ReciprocalDurationUnit),
        [
            new UnitInfo<ReciprocalDurationUnit>(ReciprocalDurationUnit.ReciprocalSeconds, "Reciprocal Seconds", BaseUnits.Undefined, "Reciprocal Second"),
            new UnitInfo<ReciprocalDurationUnit>(ReciprocalDurationUnit.ReciprocalMinutes, "Reciprocal Minutes", BaseUnits.Undefined, "Reciprocal Minute"),
            new UnitInfo<ReciprocalDurationUnit>(ReciprocalDurationUnit.ReciprocalHours,   "Reciprocal Hours",   BaseUnits.Undefined, "Reciprocal Hour"  ),
        ],
        ReciprocalDurationUnit.ReciprocalSeconds,
        Zero,
        Dimensions);

    /// <summary>Convert this quantity to the given unit.</summary>
    public double As(Enum unit)
    {
        if (unit is ReciprocalDurationUnit targetUnit)
        {
            // Convert: this → ReciprocalSecond → target
            double valueInBaseUnit = Value * GetConversionFactor(Unit);
            return valueInBaseUnit / GetConversionFactor(targetUnit);
        }
        throw new ArgumentException("Must be of type ReciprocalDurationUnit.", nameof(unit));
    }

    public double As(UnitSystem unitSystem) => throw new NotImplementedException();

    public IQuantity ToUnit(Enum unit)
    {
        if (unit is ReciprocalDurationUnit targetUnit)
            return new ReciprocalDuration(As(unit), targetUnit);
        throw new ArgumentException("Must be of type ReciprocalDurationUnit.", nameof(unit));
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

    public static ReciprocalDuration FromReciprocalSeconds(double value) => new(value, ReciprocalDurationUnit.ReciprocalSeconds);
    public static ReciprocalDuration FromReciprocalMinutes(double value) => new(value, ReciprocalDurationUnit.ReciprocalMinutes);
    public static ReciprocalDuration FromReciprocalHours(double value) => new(value, ReciprocalDurationUnit.ReciprocalHours);

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

    // Convenience properties after construction
    public double ReciprocalSeconds => As(ReciprocalDurationUnit.ReciprocalSeconds);
    public double ReciprocalMinutes => As(ReciprocalDurationUnit.ReciprocalMinutes);
    public double ReciprocalHours => As(ReciprocalDurationUnit.ReciprocalHours);

    readonly QuantityValue IQuantity.Value => Value;

    #endregion

    #region Operators

    public static ReciprocalDuration operator +(ReciprocalDuration a, ReciprocalDuration b) =>
        new(a.ReciprocalSeconds + b.ReciprocalSeconds, ReciprocalDurationUnit.ReciprocalSeconds);

    public static ReciprocalDuration operator -(ReciprocalDuration a, ReciprocalDuration b) =>
        new(a.ReciprocalSeconds - b.ReciprocalSeconds, ReciprocalDurationUnit.ReciprocalSeconds);

    public static ReciprocalDuration operator *(double scalar, ReciprocalDuration q) =>
        new(scalar * q.Value, q.Unit);

    public static ReciprocalDuration operator *(ReciprocalDuration q, double scalar) =>
        new(q.Value * scalar, q.Unit);


    #endregion
}
