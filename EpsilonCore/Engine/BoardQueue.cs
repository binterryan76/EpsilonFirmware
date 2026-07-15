using EpsilonCore.Commands;
using EpsilonCore.Communication;
using EpsilonCore.Helpers;

namespace EpsilonCore.Engine;

/// <summary>
/// Each microcontroller has it's own internal queue and <see cref="ICommunicator"/>.
/// This class helps ensure we don't fill the microcontroller's internal buffer.
/// </summary>
/// <param name="communicator"></param>
internal class BoardQueue(ICommunicator communicator)
{
    public enum BoardQueueStatus
    {
        Running,
        Stopped,
    }

    /// <summary>
    /// Keeps track of how many bytes are occupied in the microcontroller's command buffer.
    /// </summary>
    public int BoardCommandBufferBytesOccupied { get; set; } = 0;

    /// <summary>
    /// Keeps track of how many bytes are unoccupied in the microcontroller's command buffer.
    /// </summary>
    public int BoardCommandBufferBytesFree { get => Constants.BOARD_COMMAND_BUFFER_SIZE - BoardCommandBufferBytesOccupied; }

    /// <summary>
    /// Shares the same Id as the <see cref="ICommunicator"/> because 
    /// there is a 1 to 1 correspondance between 
    /// <see cref="ICommunicator"/>s and <see cref="BoardQueue"/>s.
    /// </summary>
    public uint Id { get; } = communicator.Id;

    public readonly Queue<QueuedCommand> Queued = [];
    public readonly Queue<QueuedCommand> Sent = [];
    public ICommunicator Communicator { get; } = communicator;
    public BoardQueueStatus Status { get; set; } = BoardQueueStatus.Running;
}
