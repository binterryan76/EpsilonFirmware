using EpsilonCore.Boards;
using EpsilonCore.Engine;
using EpsilonCore.Machines;

namespace EpsilonCore.Communication;

/// <summary>
/// Used to communicate with a microcontroller board.
/// </summary>
public abstract class ICommunicator : IEntity
{
    /// <inheritdoc/>
    public uint Id { get; internal set; } = 0;

    /// <summary>
    /// The ID of the microcontroller <see cref="Board"/> that this <see cref="ICommunicator"/> is for.
    /// </summary>
    public uint BoardId { get => Id; }

    /// <summary>
    /// The ID of the <see cref="Machine"/> that this <see cref="ICommunicator"/> is for.
    /// </summary>
    public required uint MachineId { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <summary>
    /// Connects to a a microcontroller board.
    /// </summary>
    /// <returns>True if successful, false if unsuccessful.</returns>
    public abstract bool Connect();

    /// <summary>
    /// Disconnects from a a microcontroller board.
    /// </summary>
    public abstract void Disconnect();

    /// <summary>
    /// Sends a data packet to a microcontroller board.
    /// Must be safe to call from several threads at once because immediate commands are sent from the caller's thread
    /// while the engine's main loop sends queued commands.
    /// Must not check <see cref="BoardCommandBufferBytesFree"/>: the engine does that for queued commands, and
    /// immediate commands never use the command buffer.
    /// </summary>
    /// <param name="packet"></param>
    /// <returns>True if successful, false if unsuccessful.</returns>
    public abstract bool Send(DataPacket packet);

    /// <summary>
    /// Reads zero or more <see cref="DataPacket"/>s from a microcontroller board.
    /// </summary>
    /// <returns></returns>
    public abstract IEnumerable<DataPacket> Read();

    /// <summary>
    /// Stores the last used sequence number used by a <see cref="DataPacket"/> when it is sent.
    /// Start at <see cref="byte.MaxValue"/> that way the first time 
    /// <see cref="IncrementAndReturnNextSequenceNumber"/> is called, it will wrap to 0 before being returned.
    /// </summary>
    private byte lastUsedSequenceNumber = byte.MaxValue;

    /// <summary>
    /// Returns the next usable sequence number for a command being sent with this <see cref="ICommunicator"/>.
    /// </summary>
    /// <returns></returns>
    public byte IncrementAndReturnNextSequenceNumber()
    {
        if (lastUsedSequenceNumber == byte.MaxValue)
            lastUsedSequenceNumber = 0;
        else
            lastUsedSequenceNumber++;

        return lastUsedSequenceNumber;
    }

    /// <summary>
    /// Returns the sequence number <see cref="IncrementAndReturnNextSequenceNumber"/> will return next, without using it.
    /// </summary>
    /// <returns>The next sequence number.</returns>
    public byte PeekNextSequenceNumber() =>
        lastUsedSequenceNumber == byte.MaxValue ? (byte)0 : (byte)(lastUsedSequenceNumber + 1);

    /// <summary>
    /// Raised when one or more <see cref="DataPacket"/>s are received from a microcontroller board.
    /// Automatically assigned when an <see cref="ICommunicator"/> is assigned to a <see cref="MachineQueue"/>.
    /// </summary>
    public abstract event EventHandler<IEnumerable<DataPacket>>? DataPacketsReceivedHandler;

    /// <summary>
    /// The size of the microcontroller's command buffer in bytes. 
    /// </summary>
    public uint BoardCommandBufferSize { get; init; }

    /// <summary>
    /// Keeps track of how many bytes are occupied in the microcontroller's command buffer.
    /// </summary>
    public uint BoardCommandBufferBytesOccupied { get; internal set; } = 0;

    /// <summary>
    /// Keeps track of how many bytes are unoccupied in the microcontroller's command buffer.
    /// </summary>
    public uint BoardCommandBufferBytesFree { get => BoardCommandBufferSize - BoardCommandBufferBytesOccupied; }

    /// <summary>
    /// Marks bytes of the microcontroller's command buffer as free after a <see cref="Commands.CommandCode.BUFFER_BYTES_FREED"/> message.
    /// </summary>
    /// <param name="bytesFreed">Number of bytes the board freed.</param>
    internal void FreeBufferBytes(uint bytesFreed) =>
        BoardCommandBufferBytesOccupied = bytesFreed >= BoardCommandBufferBytesOccupied ? 0 : BoardCommandBufferBytesOccupied - bytesFreed;

    /// <summary>
    /// Marks the whole command buffer as free, used when the board discards its buffer
    /// (emergency stop, pause or fault).
    /// </summary>
    internal void ResetBufferAccounting() => BoardCommandBufferBytesOccupied = 0;
}







