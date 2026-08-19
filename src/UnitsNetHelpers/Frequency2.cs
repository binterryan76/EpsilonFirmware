using UnitsNet;

namespace UnitsNetHelpers;

/// <summary>
/// Units for a <see cref="Frequency2"/> object.
/// </summary>
public enum Frequency2Unit
{
    HertzSquared,
}

/// <summary>
/// Represents a frequency squared quantity.
/// </summary>
/// <param name="value"></param>
/// <param name="unit"></param>
public readonly struct Frequency2(double value, Frequency2Unit unit) : IQuantity
{
    readonly Enum IQuantity.Unit => Unit;
    public Frequency2Unit Unit { get; } = unit;
    public double Value { get; } = value;

    #region Conversion factors (to Second)

    private static double GetConversionFactor(Frequency2Unit unit) => unit switch
    {
        Frequency2Unit.HertzSquared => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    #endregion

    #region IQuantity

    public static readonly Frequency2 Zero = new(0, Frequency2Unit.HertzSquared);

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
        "Frequency^2",
        typeof(Frequency2Unit),
        [
            new UnitInfo<Frequency2Unit>(Frequency2Unit.HertzSquared, "Hertz^2", BaseUnits.Undefined, "Hertz^2"),
        ],
        Frequency2Unit.HertzSquared,
        Zero,
        Dimensions);

    /// <summary>Convert this quantity to the given unit.</summary>
    public double As(Enum unit)
    {
        if (unit is Frequency2Unit targetUnit)
        {
            // Convert: this → Second → target
            double valueInBaseUnit = Value * GetConversionFactor(Unit);
            return valueInBaseUnit / GetConversionFactor(targetUnit);
        }
        throw new ArgumentException("Must be of type Frequency2Unit.", nameof(unit));
    }

    public double As(UnitSystem unitSystem) => throw new NotImplementedException();

    public IQuantity ToUnit(Enum unit)
    {
        if (unit is Frequency2Unit targetUnit)
            return new Frequency2(As(unit), targetUnit);
        throw new ArgumentException("Must be of type Frequency2Unit.", nameof(unit));
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

    public static Frequency2 FromHertzSquared(double value) => new(value, Frequency2Unit.HertzSquared);

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

    public Frequency Sqrt()
    {
        return Frequency.FromHertz(Math.Sqrt(HertzSquared));
    }

    public Frequency2 Abs()
    {
        return Frequency2.FromHertzSquared(Math.Abs(HertzSquared));
    }

    // Convenience properties after construction
    public double HertzSquared => As(Frequency2Unit.HertzSquared);

    readonly QuantityValue IQuantity.Value => Value;

    #endregion

    #region Operators

    public static Frequency2 operator +(Frequency2 a, Frequency2 b) =>
        new(a.HertzSquared + b.HertzSquared, Frequency2Unit.HertzSquared);

    public static Frequency2 operator -(Frequency2 a, Frequency2 b) =>
        new(a.HertzSquared - b.HertzSquared, Frequency2Unit.HertzSquared);

    public static Frequency2 operator *(double scalar, Frequency2 q) =>
        new(scalar * q.Value, q.Unit);

    public static Frequency2 operator *(Frequency2 q, double scalar) =>
        new(q.Value * scalar, q.Unit);

    public static bool operator >(Frequency2 a, Frequency2 b) =>
        a.Value > b.Value;

    public static bool operator <(Frequency2 a, Frequency2 b) =>
        a.Value < b.Value;

    public static bool operator >=(Frequency2 a, Frequency2 b) =>
        a.Value >= b.Value;

    public static bool operator <=(Frequency2 a, Frequency2 b) =>
        a.Value <= b.Value;
    #endregion
}
