namespace EpsilonCore.Communication;

/// <summary>
/// How a board decides when to start a queued command.
/// </summary>
public enum CommandTimingMode : byte
{
    /// <summary>
    /// Start <see cref="CommandTiming.Value"/> board clock ticks after the previous queued command on this board finishes.
    /// Used when the host can't predict the board's clock, such as before the clocks are synchronized or
    /// while a command with an unknown duration is still running.
    /// </summary>
    AfterPrevious = 0,

    /// <summary>
    /// Start when the board's clock reaches <see cref="CommandTiming.Value"/> ticks.
    /// If that time has already passed when the command reaches the front of the board's queue, start immediately.
    /// </summary>
    AtClock = 1,
}

/// <summary>
/// Timing header inserted by the engine right after the command code of every queued <see cref="DataPacket"/>
/// just before it is sent.
/// Wire format: 1 byte <see cref="Mode"/> followed by 8 bytes <see cref="Value"/> (little endian).
/// </summary>
/// <param name="Mode">How the board decides when to start the command.</param>
/// <param name="Value">Board clock ticks. Meaning depends on <see cref="Mode"/>.</param>
public readonly record struct CommandTiming(CommandTimingMode Mode, ulong Value)
{
    /// <summary>
    /// Number of bytes the timing header adds to a <see cref="DataPacket"/>.
    /// </summary>
    public const int LENGTH = 1 + sizeof(ulong);

    /// <summary>
    /// Start as soon as the previous queued command finishes.
    /// </summary>
    /// <param name="delayTicks">Board clock ticks to wait after the previous command finishes.</param>
    /// <returns>The timing header.</returns>
    public static CommandTiming AfterPrevious(ulong delayTicks = 0) => new(CommandTimingMode.AfterPrevious, delayTicks);

    /// <summary>
    /// Start at the given board clock tick.
    /// </summary>
    /// <param name="boardTick">Board clock tick to start at.</param>
    /// <returns>The timing header.</returns>
    public static CommandTiming AtClock(ulong boardTick) => new(CommandTimingMode.AtClock, boardTick);

    /// <summary>
    /// Returns the wire bytes of this header.
    /// </summary>
    /// <returns>The 9 bytes inserted after the command code.</returns>
    public byte[] ToBytes() => [(byte)Mode, .. BitConverter.GetBytes(Value)];
}
