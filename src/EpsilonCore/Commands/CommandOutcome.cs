using EpsilonCore.Communication;
using GenericHelpers;
using System.Collections.Immutable;

namespace EpsilonCore.Commands;

/// <summary>
/// How a queued command ended on a board.
/// </summary>
public enum CommandOutcomeStatus : byte
{
    /// <summary>
    /// The command finished normally (for example the endstop triggered or the temperature was reached).
    /// </summary>
    Success = 0,

    /// <summary>
    /// The command's condition wasn't met before its timeout.
    /// </summary>
    TimedOut = 1,

    /// <summary>
    /// The command failed on the board.
    /// </summary>
    Failed = 2,
}

/// <summary>
/// The result of one <see cref="DataPacket"/> of a queued command, reported by a board with either
/// <see cref="CommandCode.COMMAND_COMPLETE"/> or <see cref="CommandCode.COMMAND_RESULT"/>.
/// </summary>
/// <param name="CommunicatorId">The communicator the result came from.</param>
/// <param name="SequenceNumber">Sequence number of the completed data packet.</param>
/// <param name="Status">How the command ended.</param>
/// <param name="CompletionTick">Board clock tick when the command finished, or 0 if the board didn't report it.</param>
/// <param name="Data">Command specific result data, for example a measured temperature.</param>
public record CommandOutcome(
    uint CommunicatorId,
    byte SequenceNumber,
    CommandOutcomeStatus Status,
    ulong CompletionTick,
    ImmutableArray<byte> Data)
{
    /// <summary>
    /// Number of payload bytes before <see cref="Data"/> in a <see cref="CommandCode.COMMAND_RESULT"/> packet.
    /// </summary>
    public const int HEADER_LENGTH = 1 + sizeof(ulong);

    /// <summary>
    /// Parses a <see cref="CommandCode.COMMAND_RESULT"/> packet.
    /// Payload: 1 byte <see cref="CommandOutcomeStatus"/>, 8 bytes completion tick, then command specific data.
    /// </summary>
    /// <param name="dataPacket">The received data packet.</param>
    /// <returns>The outcome, or an error if the payload is invalid.</returns>
    public static Result<CommandOutcome> FromCommandResult(DataPacket dataPacket)
    {
        ReadOnlySpan<byte> payload = dataPacket.Payload;
        if (payload.Length < HEADER_LENGTH)
            return new ArgumentException($"{nameof(CommandCode.COMMAND_RESULT)} payload must be at least {HEADER_LENGTH} bytes but was {payload.Length}.");

        if (!Enum.IsDefined(typeof(CommandOutcomeStatus), payload[0]))
            return new ArgumentException($"Unrecognized {nameof(CommandOutcomeStatus)} {payload[0]}.");

        return new CommandOutcome(
            dataPacket.CommunicatorId,
            dataPacket.SequenceNumber,
            (CommandOutcomeStatus)payload[0],
            BitConverter.ToUInt64(payload[1..HEADER_LENGTH]),
            [.. payload[HEADER_LENGTH..]]);
    }

    /// <summary>
    /// Parses a <see cref="CommandCode.COMMAND_COMPLETE"/> packet.
    /// Payload: optionally 8 bytes completion tick.
    /// </summary>
    /// <param name="dataPacket">The received data packet.</param>
    /// <returns>A successful outcome.</returns>
    public static CommandOutcome FromCommandComplete(DataPacket dataPacket)
    {
        ReadOnlySpan<byte> payload = dataPacket.Payload;
        ulong completionTick = payload.Length >= sizeof(ulong) ? BitConverter.ToUInt64(payload[..sizeof(ulong)]) : 0;
        return new CommandOutcome(
            dataPacket.CommunicatorId,
            dataPacket.SequenceNumber,
            CommandOutcomeStatus.Success,
            completionTick,
            []);
    }
}
