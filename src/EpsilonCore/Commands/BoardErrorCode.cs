using EpsilonCore.Communication;
using EpsilonCore.Helpers;
using System.Diagnostics;

namespace EpsilonCore.Commands;

/// <summary>
/// Indicates an error sent from a board.
/// Note: These values should never exceed byte.MaxValue
/// </summary>
public enum BoardErrorCode
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    SEND_TIMEOUT = 0,
    SEND_FAIL,
    COMMAND_BUFFER_FULL,
    DATA_PACKET_LENGTH_TOO_SMALL,
    TIMED_ACTION_NEXT_SHOULD_BE_NULL,
    PIN_ID_OUT_OF_RANGE,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// Helpers for <see cref="BoardErrorCode"/>.
/// </summary>
public static class BoardErrorCodeHelpers
{
    /// <summary>
    /// Converts a <see cref="byte"/> to a <see cref="BoardErrorCode"/> if a corresponding value exists.
    /// </summary>
    /// <param name="boardErrorCodeByte"></param>
    /// <returns></returns>
    public static Result<BoardErrorCode> ToBoardErrorCode(this byte boardErrorCodeByte)
    {
        if (Enum.IsDefined(typeof(BoardErrorCode), boardErrorCodeByte))
            return (BoardErrorCode)boardErrorCodeByte;

        return new ArgumentException($"{nameof(BoardErrorCode)} doesn't contain a value for {boardErrorCodeByte}.");
    }

    /// <summary>
    /// Returns a message explaining what a given <see cref="BoardErrorCode"/> means.
    /// </summary>
    /// <param name="boardErrorCode"></param>
    /// <returns></returns>
    public static string DisplayMessage(this BoardErrorCode boardErrorCode)
    {
        string displayMessage = boardErrorCode switch
        {
            BoardErrorCode.SEND_TIMEOUT =>
                "The board tried to send a command but timed out",
            BoardErrorCode.SEND_FAIL =>
                "The board tried to send a command but the send failed",
            BoardErrorCode.COMMAND_BUFFER_FULL =>
                "The boards command buffer is full",
            BoardErrorCode.DATA_PACKET_LENGTH_TOO_SMALL =>
                $"The board received a command with a data packet data length that was smaller than {DataPacket.MIN_DATA_LENGTH}",
            BoardErrorCode.TIMED_ACTION_NEXT_SHOULD_BE_NULL =>
                "The board has a bug where the next TimedAction in it's queue should be NULL but isn't",
            BoardErrorCode.PIN_ID_OUT_OF_RANGE =>
                "The board received a reference to a pin that was out of range",
            _ => string.Empty
        };

        Debug.Assert(!string.IsNullOrEmpty(displayMessage),
            $"{nameof(DisplayMessage)} is missing value in switch statment for {nameof(BoardErrorCode)}.{Enum.GetName(boardErrorCode)}");

        return displayMessage;
    }
}