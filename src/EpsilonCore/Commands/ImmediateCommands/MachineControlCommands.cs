using EpsilonCore.Communication;
using GenericHelpers;
using System.Collections.Immutable;

namespace EpsilonCore.Commands.ImmediateCommands;

/// <summary>
/// Stops every board immediately: motors stop, heaters turn off and pins go to their safe states.
/// The boards discard their command buffers and ignore queued commands until <see cref="ClearFaultsCommand"/>.
/// On the host, every queued and in-flight command is discarded and all axes are marked as unhomed.
/// </summary>
public record EmergencyStopCommand : IImmediateCommand
{
    /// <inheritdoc />
    public string Description => "Emergency stop";

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.EmergencyStop;

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context) =>
        ImmediateCommandHelpers.ToAllCommunicators(context, CommandCode.EMERGENCY_STOP);
}

/// <summary>
/// Asks every board to stop after the command it is running, discard the rest of its command buffer and
/// report <see cref="BoardRunState.Paused"/>. The host then puts the discarded commands back in front of the
/// commands waiting to be queued so <see cref="ResumeCommand"/> can run them.
/// </summary>
public record PauseCommand : IImmediateCommand
{
    /// <inheritdoc />
    public string Description => "Pause";

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.Pause;

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context) =>
        ImmediateCommandHelpers.ToAllCommunicators(context, CommandCode.PAUSE);
}

/// <summary>
/// Lets paused boards run queued commands again and restarts the host's paused machine queue.
/// </summary>
public record ResumeCommand : IImmediateCommand
{
    /// <inheritdoc />
    public string Description => "Resume";

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.Resume;

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context) =>
        ImmediateCommandHelpers.ToAllCommunicators(context, CommandCode.RESUME);
}

/// <summary>
/// Clears an emergency stop or fault on every board (leaving them running with an empty command buffer)
/// and restarts the host's machine queue.
/// </summary>
public record ClearFaultsCommand : IImmediateCommand
{
    /// <inheritdoc />
    public string Description => "Clear faults";

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.ClearFaults;

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context) =>
        ImmediateCommandHelpers.ToAllCommunicators(context, CommandCode.CLEAR_FAULTS);
}

/// <summary>
/// Asks every board to send a <see cref="CommandCode.STATUS_REPORT"/>.
/// Reports are delivered through <see cref="Engine.EpsilonEngine.BoardStatusReceived"/>.
/// </summary>
public record StatusQueryCommand : IImmediateCommand
{
    /// <inheritdoc />
    public string Description => "Query board status";

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.None;

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context) =>
        ImmediateCommandHelpers.ToAllCommunicators(context, CommandCode.STATUS_QUERY);
}
