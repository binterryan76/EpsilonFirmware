using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using GenericHelpers;
using System.Collections.Immutable;

namespace EpsilonCore.Commands;

/// <summary>
/// What the engine does on the host after sending an <see cref="IImmediateCommand"/>.
/// </summary>
public enum ImmediateHostAction
{
    /// <summary>
    /// Nothing besides sending the data packets.
    /// </summary>
    None,

    /// <summary>
    /// Clears every queued and in-flight command, marks all axes as unhomed and stops the machine queue
    /// until <see cref="ClearFaults"/> is sent.
    /// </summary>
    EmergencyStop,

    /// <summary>
    /// Stops sending commands. Once every board reports it is paused, the commands the boards didn't run are put back
    /// in front of the commands waiting to be queued so <see cref="Resume"/> can run them again.
    /// </summary>
    Pause,

    /// <summary>
    /// Restarts a paused machine queue.
    /// </summary>
    Resume,

    /// <summary>
    /// Restarts a machine queue that was stopped by an error or an emergency stop.
    /// </summary>
    ClearFaults,
}

/// <summary>
/// Information an <see cref="IImmediateCommand"/> needs to build its data packets.
/// </summary>
/// <param name="CurrentMachine">The machine after the last resolved command.</param>
/// <param name="CommunicatorIds">Ids of every communicator connected to the machine.</param>
public record ImmediateCommandContext(Machine CurrentMachine, IImmutableList<uint> CommunicatorIds);

/// <summary>
/// A command the board acts on as soon as it receives it, such as an emergency stop.
/// Immediate commands skip every queue on the host and on the board: they are sent straight away from the caller's
/// thread with <see cref="Engine.EpsilonEngine.SendImmediate"/>, don't wait for buffer space, don't get a sequence
/// number and are never answered with a completion message.
/// Their data packets must use a <see cref="CommandCode"/> in the immediate range (see <see cref="CommandCodeHelpers.IsImmediate"/>).
/// </summary>
public interface IImmediateCommand
{
    /// <summary>
    /// Summary of what the command does.
    /// Exclude trailing periods from this string.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// What the engine does on the host after sending the data packets.
    /// </summary>
    public ImmediateHostAction HostAction { get; }

    /// <summary>
    /// Returns the data packets to send, or an error if the command can't be sent to this machine.
    /// </summary>
    /// <param name="context">The current machine and its communicators.</param>
    /// <returns>The data packets to send, or an error if the command can't be sent.</returns>
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context);
}

/// <summary>
/// An <see cref="IImmediateCommand"/> that changes a <see cref="LiveAdjustments"/> value, such as a babystep.
/// Live adjustments are applied by the board on top of commands that were already planned, so the host records them
/// separately instead of re-planning queued commands.
/// </summary>
public interface ILiveAdjustmentCommand : IImmediateCommand
{
    /// <summary>
    /// Records this command's change once its data packets have been sent.
    /// </summary>
    /// <param name="liveAdjustments">The machine's live adjustments.</param>
    public void ApplyTo(LiveAdjustments liveAdjustments);
}

/// <summary>
/// Helpers for building <see cref="IImmediateCommand"/> data packets.
/// </summary>
public static class ImmediateCommandHelpers
{
    /// <summary>
    /// Returns one data packet with the given code and payload for every communicator in the context.
    /// </summary>
    /// <param name="context">The current machine and its communicators.</param>
    /// <param name="commandCode">Immediate command code to send.</param>
    /// <param name="payload">Bytes after the command code, or null for none.</param>
    /// <returns>One data packet per communicator, or an error if the code isn't an immediate command code.</returns>
    public static Result<IImmutableList<DataPacket>> ToAllCommunicators(
        ImmediateCommandContext context,
        CommandCode commandCode,
        byte[]? payload = null)
    {
        if (!commandCode.IsImmediate())
            return new ArgumentException($"{commandCode} is not an immediate command code.");

        List<DataPacket> dataPackets = [];
        foreach (uint communicatorId in context.CommunicatorIds)
        {
            Result<DataPacket> dataPacket = DataPacket.New(communicatorId, 0, commandCode, payload ?? []);
            if (dataPacket.IsError)
                return dataPacket.Exception;
            dataPackets.Add(dataPacket.Value);
        }

        return ImmutableList.CreateRange(dataPackets);
    }
}
