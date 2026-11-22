using EpsilonCore.Commands;
using EpsilonCore.Communication;

namespace EpsilonCore.Engine;

/// <summary>
/// A command that has started being sent to its boards but hasn't been resolved yet.
/// A command resolves once every one of its data packets has been sent and completed, and every command sent before it
/// has resolved, so <see cref="MachineQueue.CurrentMachine"/> always advances in queue order even if boards finish out of order.
/// Commands with no data packets that were queued behind in-flight commands are also tracked here (already complete)
/// so they resolve in order.
/// </summary>
/// <param name="queuedCommand">The command being tracked.</param>
internal class InFlightCommand(QueuedCommand queuedCommand)
{
    /// <summary>
    /// The command being tracked.
    /// </summary>
    public QueuedCommand QueuedCommand { get; } = queuedCommand;

    /// <summary>
    /// Data packets that have been sent but not completed, as (communicator id, sequence number).
    /// </summary>
    public HashSet<(uint CommunicatorId, byte SequenceNumber)> PendingPackets { get; } = [];

    /// <summary>
    /// Results reported for this command's data packets, keyed by communicator id.
    /// If a command sends several packets to one board, the worst result is kept.
    /// </summary>
    public Dictionary<uint, CommandOutcome> Outcomes { get; } = [];

    /// <summary>
    /// Number of this command's data packets that have completed.
    /// </summary>
    public int CompletedPacketCount { get; set; }

    /// <summary>
    /// True once every data packet of the command has been sent.
    /// </summary>
    public bool AllPacketsSent { get; set; }

    /// <summary>
    /// True once every data packet has been sent and completed.
    /// </summary>
    public bool IsComplete => AllPacketsSent && PendingPackets.Count == 0;

    /// <summary>
    /// Records the result of one data packet.
    /// </summary>
    /// <param name="outcome">The reported result.</param>
    public void Complete(CommandOutcome outcome)
    {
        if (!PendingPackets.Remove((outcome.CommunicatorId, outcome.SequenceNumber)))
            return;

        CompletedPacketCount++;
        if (!Outcomes.TryGetValue(outcome.CommunicatorId, out CommandOutcome? existing) ||
            outcome.Status > existing.Status)
            Outcomes[outcome.CommunicatorId] = outcome;
    }
}
