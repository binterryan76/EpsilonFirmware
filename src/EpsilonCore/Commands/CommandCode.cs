using EpsilonCore.Helpers;

namespace EpsilonCore.Commands;

/// <summary>
/// Note: These values should never exceed byte.MaxValue
/// </summary>
public enum CommandCode
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    MOVE = 1,
    SET_TARGET_TEMP = 20,
    BLINK_PIN = 100,
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
    /// Converts a <see cref="byte"/> to a <see cref="CommandCode"/> if a corresponding value exists.
    /// </summary>
    /// <param name="commandCodeByte"></param>
    /// <returns></returns>
    public static Result<CommandCode> ToCommandCode(this byte commandCodeByte)
    {
        if (Enum.IsDefined(typeof(CommandCode), (int)commandCodeByte))
            return (CommandCode)commandCodeByte;

        return new ArgumentException($"{nameof(CommandCode)} doesn't contain a value for {commandCodeByte}.");
    }
}