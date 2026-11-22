using EpsilonCore.Communication;

namespace EpsilonCore.Engine;

/// <summary>
/// The host's bookkeeping for one board (one <see cref="ICommunicator"/>) of a machine.
/// Only accessed while holding <see cref="MachineQueue.SyncRoot"/>.
/// </summary>
/// <param name="communicator">The communicator used to talk to the board.</param>
internal class BoardState(ICommunicator communicator)
{
    /// <summary>
    /// The communicator used to talk to the board.
    /// </summary>
    public ICommunicator Communicator { get; } = communicator;

    /// <summary>
    /// Relationship between the host's clock and the board's clock.
    /// </summary>
    public ClockSync ClockSync { get; } = new();

    /// <summary>
    /// Commands with data packets on this board that haven't completed, keyed by sequence number.
    /// Sequence numbers are one byte, so at most 256 packets can be in flight per board.
    /// </summary>
    public Dictionary<byte, InFlightCommand> InFlightBySequenceNumber { get; } = [];

    /// <summary>
    /// Host time when the board is expected to finish the last command sent to it.
    /// </summary>
    public double ScheduledEndHostSeconds { get; set; } = double.NegativeInfinity;

    /// <summary>
    /// Number of <see cref="Commands.IConditionalCommand"/>s sent to this board that haven't resolved.
    /// While this is above zero the board's end time is unknown, so commands are sent with
    /// <see cref="CommandTimingMode.AfterPrevious"/>.
    /// </summary>
    public int UnresolvedConditionalCount { get; set; }

    /// <summary>
    /// Total expected duration of commands sent after an unresolved <see cref="Commands.IConditionalCommand"/>.
    /// Added to the conditional command's completion time to estimate when the board will be free.
    /// </summary>
    public double SecondsQueuedAfterConditional { get; set; }

    /// <summary>
    /// The last status the board reported.
    /// </summary>
    public BoardStatus? LastStatus { get; set; }

    /// <summary>
    /// Forgets everything the board had queued, used when the board discards its command buffer.
    /// </summary>
    /// <param name="now">Host time, in seconds.</param>
    public void ResetQueue(double now)
    {
        InFlightBySequenceNumber.Clear();
        Communicator.ResetBufferAccounting();
        UnresolvedConditionalCount = 0;
        SecondsQueuedAfterConditional = 0;
        ScheduledEndHostSeconds = now;
    }
}
