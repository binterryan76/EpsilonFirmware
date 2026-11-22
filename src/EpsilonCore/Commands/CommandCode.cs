using EpsilonCore.Helpers; using GenericHelpers;

namespace EpsilonCore.Commands;

/// <summary>
/// Identifies what a <see cref="Communication.DataPacket"/> contains.
/// These values must match the microcontroller firmware.
/// See the "Communication Protocol" page of the Obsidian documentation for the full packet formats.
/// Note: These values should never exceed byte.MaxValue.
///
/// Ranges:
/// <list type="bullet">
/// <item>1-127: Queued commands (host to board). They are stored in the board's command buffer, run in order,
/// carry a <see cref="Communication.CommandTiming"/> header and are answered with <see cref="COMMAND_COMPLETE"/>
/// or <see cref="COMMAND_RESULT"/>.</item>
/// <item>128-191: Immediate commands (host to board). The board acts on them as soon as they are received,
/// they never enter the command buffer and are never answered with a completion message.</item>
/// <item>240-255: Messages from the board to the host.</item>
/// </list>
/// </summary>
public enum CommandCode
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    // Queued commands (host to board).
    MOVE = 1,
    MOVE_INTO_ENDSTOPS = 2,
    SET_TARGET_TEMP = 20,
    WAIT_FOR_TEMP = 21,
    BLINK_PIN = 100,

    // Immediate commands (host to board).
    EMERGENCY_STOP = 128,
    PAUSE = 129,
    RESUME = 130,
    CLEAR_FAULTS = 131,
    BABYSTEP = 132,
    STATUS_QUERY = 133,
    CLOCK_SYNC_REQUEST = 134,

    // Messages from the board to the host.
    CLOCK_SYNC_RESPONSE = 248,
    STATUS_REPORT = 249,
    COMMAND_RESULT = 250,
    BUFFER_BYTES_FREED = 251,
    DISPLAY_TEXT = 253,
    ERROR = 254,
    COMMAND_COMPLETE = 255,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// Helpers for <see cref="CommandCode"/>.
/// </summary>
public static class CommandCodeHelpers
{
    /// <summary>
    /// First value of the immediate command range.
    /// </summary>
    public const byte IMMEDIATE_RANGE_START = 128;

    /// <summary>
    /// Last value of the immediate command range.
    /// </summary>
    public const byte IMMEDIATE_RANGE_END = 191;

    /// <summary>
    /// Converts a <see cref="byte"/> to a <see cref="CommandCode"/> if a corresponding value exists.
    /// </summary>
    /// <param name="commandCodeByte">The byte to convert.</param>
    /// <returns>The command code, or an error if the byte isn't a known command code.</returns>
    public static Result<CommandCode> ToCommandCode(this byte commandCodeByte)
    {
        if (Enum.IsDefined(typeof(CommandCode), (int)commandCodeByte))
            return (CommandCode)commandCodeByte;

        return new ArgumentException($"{nameof(CommandCode)} doesn't contain a value for {commandCodeByte}.");
    }

    /// <summary>
    /// Returns true if the board acts on this command as soon as it is received instead of queueing it.
    /// </summary>
    /// <param name="commandCode">The command code to check.</param>
    /// <returns>True for codes 128 to 191.</returns>
    public static bool IsImmediate(this CommandCode commandCode) =>
        (byte)commandCode >= IMMEDIATE_RANGE_START && (byte)commandCode <= IMMEDIATE_RANGE_END;

    /// <summary>
    /// Returns true if this command is stored in the board's command buffer and run in order.
    /// </summary>
    /// <param name="commandCode">The command code to check.</param>
    /// <returns>True for codes 1 to 127.</returns>
    public static bool IsQueued(this CommandCode commandCode) =>
        (byte)commandCode > 0 && (byte)commandCode < IMMEDIATE_RANGE_START;
}
