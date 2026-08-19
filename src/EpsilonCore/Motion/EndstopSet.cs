namespace EpsilonCore.Motion;

/// <summary>
/// Represents a pair of min and max endstops.
/// They are nullable because some machines have just one and some have both.
/// </summary>
public record EndstopSet
{
    /// <summary>
    /// Id of the minimum endstop.
    /// </summary>
    public uint? MinEndstopId { get; init; } = null;

    /// <summary>
    /// Id of the minimum endstop.
    /// </summary>
    public uint? MaxEndstopId { get; init; } = null;

    private EndstopSet(uint? minEndstopId, uint? maxEndstopId)
    {
        MinEndstopId = minEndstopId;
        MaxEndstopId = maxEndstopId;
    }

    /// <summary>
    /// Returns a new <see cref="EndstopSet"/> with only a minimum endstop.
    /// </summary>
    /// <param name="minEndstopId"></param>
    /// <returns></returns>
    public static EndstopSet MinOnly(uint minEndstopId)
    {
        return new EndstopSet(minEndstopId, null);
    }

    /// <summary>
    /// Returns a new <see cref="EndstopSet"/> with only a maximum endstop.
    /// </summary>
    /// <param name="maxEndstopId"></param>
    /// <returns></returns>
    public static EndstopSet MaxOnly(uint maxEndstopId)
    {
        return new EndstopSet(null, maxEndstopId);
    }

    /// <summary>
    /// Returns a new <see cref="EndstopSet"/> with bowth a minimum and maximum endstop.
    /// </summary>
    /// <param name="minEndstopId"></param>
    /// <param name="maxEndstopId"></param>
    /// <returns></returns>
    public static EndstopSet Both(uint minEndstopId, uint maxEndstopId)
    {
        return new EndstopSet(minEndstopId, maxEndstopId);
    }

}

