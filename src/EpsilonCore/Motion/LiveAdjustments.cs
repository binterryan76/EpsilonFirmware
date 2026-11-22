using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Motion;

/// <summary>
/// Adjustments made while the machine is running that the board applies on top of commands that were already planned,
/// such as babysteps. This is mutable measured-style state: it is never reverted when commands are cancelled,
/// and it is cleared by an emergency stop because the board loses its positions.
/// Thread safe.
/// </summary>
public class LiveAdjustments
{
    private readonly Lock syncRoot = new();
    private ImmutableDictionary<uint, Length> offsetsLinear = ImmutableDictionary<uint, Length>.Empty;
    private ImmutableDictionary<uint, Angle> offsetsRotational = ImmutableDictionary<uint, Angle>.Empty;

    /// <summary>
    /// Total babystep offset of each linear actuator, keyed by actuator id.
    /// </summary>
    public IImmutableDictionary<uint, Length> ActuatorOffsetsLinear => offsetsLinear;

    /// <summary>
    /// Total babystep offset of each rotational actuator, keyed by actuator id.
    /// </summary>
    public IImmutableDictionary<uint, Angle> ActuatorOffsetsRotational => offsetsRotational;

    /// <summary>
    /// Adds to the offset of a linear actuator.
    /// </summary>
    /// <param name="actuatorLinearId">Id of the linear actuator.</param>
    /// <param name="offset">How far to shift the actuator.</param>
    public void AddOffset(uint actuatorLinearId, Length offset)
    {
        lock (syncRoot)
            offsetsLinear = offsetsLinear.SetItem(actuatorLinearId,
                offsetsLinear.GetValueOrDefault(actuatorLinearId, Length.Zero) + offset);
    }

    /// <summary>
    /// Adds to the offset of a rotational actuator.
    /// </summary>
    /// <param name="actuatorRotationalId">Id of the rotational actuator.</param>
    /// <param name="offset">How far to shift the actuator.</param>
    public void AddOffset(uint actuatorRotationalId, Angle offset)
    {
        lock (syncRoot)
            offsetsRotational = offsetsRotational.SetItem(actuatorRotationalId,
                offsetsRotational.GetValueOrDefault(actuatorRotationalId, Angle.Zero) + offset);
    }

    /// <summary>
    /// Removes every offset.
    /// </summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            offsetsLinear = ImmutableDictionary<uint, Length>.Empty;
            offsetsRotational = ImmutableDictionary<uint, Angle>.Empty;
        }
    }
}
