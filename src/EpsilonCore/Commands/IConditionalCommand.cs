using EpsilonCore.Machines;
using GenericHelpers;
using UnitsNet;

namespace EpsilonCore.Commands;

/// <summary>
/// A queued command that runs in order on the board but finishes at an unknown time because it waits for a
/// sensor, such as moving until an endstop triggers or waiting until a heater reaches a temperature.
///
/// The board answers each of the command's data packets with <see cref="CommandCode.COMMAND_RESULT"/>.
/// Once every packet has a result, the engine calls <see cref="Resolve"/> to build the real resultant machine.
/// If any result isn't <see cref="CommandOutcomeStatus.Success"/> or <see cref="Resolve"/> returns an error,
/// the machine queue faults and stops.
/// </summary>
public interface IConditionalCommand : ICommand
{
    /// <summary>
    /// True if the <see cref="QueuedCommand.ResultantMachine"/> returned by <see cref="ICommand.EnqueueCommandSpecific"/>
    /// is correct for planning the commands after this one, so they can be planned and sent before this command finishes
    /// (for example waiting for a temperature doesn't change any planned positions).
    ///
    /// When this is true, <see cref="Resolve"/> must not change state that later commands were planned from;
    /// it should return the predicted machine (measured values belong in mutable state, not the machine).
    ///
    /// False if the result can only be known once the board reports back (for example the position after moving into
    /// an endstop). The engine then stops planning commands after this one until it resolves (a barrier).
    /// </summary>
    public bool IsResultPredictable { get; }

    /// <summary>
    /// The longest the command may take once it starts. The board fails the command with
    /// <see cref="CommandOutcomeStatus.TimedOut"/> after this, and the host faults the machine queue if it hears
    /// nothing a short time after this.
    /// </summary>
    public Duration Timeout { get; }

    /// <summary>
    /// Returns the machine that resulted from running this command, using the results reported by the boards.
    /// Only called when every result has <see cref="CommandOutcomeStatus.Success"/>.
    /// </summary>
    /// <param name="queuedCommand">The <see cref="QueuedCommand"/> returned when this command was queued.</param>
    /// <param name="outcomes">One result per communicator the command was sent to, keyed by communicator id.</param>
    /// <returns>The machine after the command, or an error that faults the machine queue.</returns>
    public Result<Machine> Resolve(QueuedCommand queuedCommand, IReadOnlyDictionary<uint, CommandOutcome> outcomes);
}
