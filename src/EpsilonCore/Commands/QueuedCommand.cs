using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Kinematics;
using EpsilonCore.Units;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Commands;

/// <summary>
/// Result returned when queueing or sending commands.
/// </summary>
/// <param name="DisplayMessage">Message to indicate why this result occurred.
///     Exclude trailing periods from this string.</param>
/// <param name="ErrorLevel"><inheritdoc cref="Commands.ErrorLevel"/></param>
/// <param name="Command">The command to be enqueued/sent.</param>
/// <param name="InitialMachine">The machine that the command was queued to.
///     This will be null if the command failed to be queued.</param>
/// <param name="ResultantMachine">The machine that would result if the command was run.
///     This will be null if the command failed to be queued.</param>
/// <param name="DataPackets">The data to send data to the microcontroller.
///     This will be null if the command failed to be queued or if there is no data to send.</param>
public record QueuedCommand(
    ErrorLevel ErrorLevel,
    string DisplayMessage,
    ICommand Command,
    IImmutableList<DataPacket> DataPackets,
    Machine? InitialMachine = null,
    Machine? ResultantMachine = null)
{
    /// <summary>
    /// Constructs a new <see cref="QueuedCommand"/> with no data packets to send to the microcontroller.
    /// </summary>
    /// <param name="errorLevel"></param>
    /// <param name="displayMessage"></param>
    /// <param name="command"></param>
    /// <param name="initialMachine"></param>
    /// <param name="resultantMachine"></param>
    public QueuedCommand(
        ErrorLevel errorLevel,
        string displayMessage,
        ICommand command,
        Machine? initialMachine = null,
        Machine? resultantMachine = null)
        : this(
            errorLevel,
            displayMessage,
            command,
            ImmutableList<DataPacket>.Empty,
            initialMachine,
            resultantMachine)
    { }

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and no data needs to be sent to the microcontroller.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="initialMachine"><inheritdoc cref="InitialMachine"/></param>
    /// <param name="resultantMachine"><inheritdoc cref="ResultantMachine"/></param>
    /// <returns></returns>
    public static QueuedCommand Success(
        ICommand command,
        Machine initialMachine,
        Machine resultantMachine) => new(
            ErrorLevel.Success,
            $"Command queued",
            command,
            initialMachine,
            resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and data needs to be sent to the microcontroller.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="initialMachine"><inheritdoc cref="InitialMachine"/></param>
    /// <param name="resultantMachine"><inheritdoc cref="ResultantMachine"/></param>
    /// <param name="dataPackets"><inheritdoc cref="DataPackets"/></param>
    /// <returns></returns>
    public static QueuedCommand Success(
        ICommand command,
        Machine initialMachine,
        Machine resultantMachine,
        IImmutableList<DataPacket> dataPackets) => new(
            ErrorLevel.Success,
            $"Command queued",
            command,
            dataPackets,
            initialMachine,
            resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and no data needs to be sent to the microcontroller however something
    /// notable happened that should probably be reported.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="message">Warning message.</param>
    /// <param name="initialMachine"><inheritdoc cref="InitialMachine"/></param>
    /// <param name="resultantMachine"><inheritdoc cref="ResultantMachine"/></param>
    /// <returns></returns>
    public static QueuedCommand Warning(
        ICommand command,
        string message,
        Machine initialMachine,
        Machine resultantMachine) => new(
            ErrorLevel.Warning,
            message,
            command,
            initialMachine,
            resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and data needs to be sent to the microcontroller something
    /// notable happened that should probably be reported.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="message">Warning message.</param>
    /// <param name="initialMachine"><inheritdoc cref="InitialMachine"/></param>
    /// <param name="resultantMachine"><inheritdoc cref="ResultantMachine"/></param>
    /// <param name="dataPackets"><inheritdoc cref="DataPackets"/></param>
    /// <returns></returns>
    public static QueuedCommand Warning(
        ICommand command,
        string message,
        Machine initialMachine,
        Machine resultantMachine,
        IImmutableList<DataPacket> dataPackets) => new(
            ErrorLevel.Warning,
            message,
            command,
            dataPackets,
            initialMachine,
            resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was unnecessary.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="displayMessage"><inheritdoc cref="DisplayMessage"/></param>
    /// <returns></returns>
    public static QueuedCommand Unnecessary(ICommand command, string displayMessage) => new(
        ErrorLevel.Unnecessary,
        displayMessage,
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was already queued.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <returns></returns>
    public static QueuedCommand AlreadyQueued(ICommand command) => new(
        ErrorLevel.Unnecessary,
        "Command has already been queued",
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command failed.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="displayMessage"><inheritdoc cref="DisplayMessage"/></param>
    /// <returns></returns>
    public static QueuedCommand Error(ICommand command, string displayMessage) => new(
        ErrorLevel.Error,
        displayMessage,
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command failed
    /// because <see cref="Machine"/> doesn't contain the given <see cref="IKinematics"/>.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="machine"></param>
    /// <param name="kinematics"></param>
    /// <returns></returns>
    public static QueuedCommand ErrorMissingKinematicSystem(
        ICommand command,
        Machine machine,
        IKinematics kinematics) =>
            Error(command, $"Machine '{machine.Name}' doesn't contain kinematic system '{kinematics.Name}'");

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command failed
    /// because <see cref="Machine"/> doesn't contain the given <paramref name="kinematicSystemId"/>.
    /// </summary>
    /// <param name="command"><inheritdoc cref="Command"/></param>
    /// <param name="machine"></param>
    /// <param name="kinematicSystemId"></param>
    /// <returns></returns>
    public static QueuedCommand ErrorMissingKinematicSystem(
        ICommand command,
        Machine machine,
        uint kinematicSystemId) =>
            Error(command, $"Machine '{machine.Name}' doesn't contain kinematic system {kinematicSystemId}");

    public static QueuedCommand ErrorMissingEndstop(ICommand command, uint endstopId) =>
        Error(command, $"Endstop {endstopId} not found");

    public static QueuedCommand TargetTempTooHigh(ICommand command, Temperature targetTemp, Temperature maxTargetTemp, DisplayUnits displayUnits)
        => Error(command, $"Requested target temp ({displayUnits!.ToDisplay(targetTemp)}) exceeds temperature controller's max target temp({displayUnits!.ToDisplay(maxTargetTemp)})");

    public static QueuedCommand AlreadyAtTargetTemp(ICommand command, uint tempControllerIndex, Temperature targetTemp, DisplayUnits displayUnits)
        => Unnecessary(command, $"Temperarure conroller {tempControllerIndex} already at requested target temp ({displayUnits!.ToDisplay(targetTemp)})");

    public static QueuedCommand TempControllerNotAssignedToMachine(ICommand command, uint tempControllerIndex, string machineName)
        => Error(command, $"Temperarure conroller {tempControllerIndex} not assigned to machine '{machineName}'");
}
