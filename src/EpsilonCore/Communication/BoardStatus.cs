using EpsilonCore.Commands;
using GenericHelpers;

namespace EpsilonCore.Communication;

/// <summary>
/// The state a board reports in a <see cref="CommandCode.STATUS_REPORT"/>.
/// </summary>
public enum BoardRunState : byte
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Running = 0,
    Paused = 1,
    EmergencyStopped = 2,
    Faulted = 3,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// A <see cref="CommandCode.STATUS_REPORT"/> received from a board.
/// Payload: 1 byte <see cref="BoardRunState"/>, 1 byte flag (1 if any queued command has run since the board started),
/// 1 byte sequence number of the last queued command the board finished.
/// </summary>
/// <param name="MachineId">Id of the machine queue the board belongs to.</param>
/// <param name="CommunicatorId">Id of the communicator the report came from.</param>
/// <param name="State">The board's state.</param>
/// <param name="LastCompletedSequenceNumber">Null if the board hasn't finished any queued command yet.</param>
public record BoardStatus(
    uint MachineId,
    uint CommunicatorId,
    BoardRunState State,
    byte? LastCompletedSequenceNumber)
{
    /// <summary>
    /// Number of payload bytes in a <see cref="CommandCode.STATUS_REPORT"/>.
    /// </summary>
    public const int PAYLOAD_LENGTH = 3;

    /// <summary>
    /// Parses a <see cref="CommandCode.STATUS_REPORT"/> packet.
    /// </summary>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <param name="dataPacket">The received data packet.</param>
    /// <returns>The status, or an error if the payload is invalid.</returns>
    public static Result<BoardStatus> FromDataPacket(uint machineId, DataPacket dataPacket)
    {
        ReadOnlySpan<byte> payload = dataPacket.Payload;
        if (payload.Length < PAYLOAD_LENGTH)
            return new ArgumentException($"{nameof(CommandCode.STATUS_REPORT)} payload must be {PAYLOAD_LENGTH} bytes but was {payload.Length}.");

        if (!Enum.IsDefined(typeof(BoardRunState), payload[0]))
            return new ArgumentException($"Unrecognized {nameof(BoardRunState)} {payload[0]}.");

        return new BoardStatus(
            machineId,
            dataPacket.CommunicatorId,
            (BoardRunState)payload[0],
            payload[1] != 0 ? payload[2] : null);
    }
}
