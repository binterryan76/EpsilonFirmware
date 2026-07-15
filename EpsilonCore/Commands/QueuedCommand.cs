using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Kinematics;
using EpsilonCore.Units;
using UnitsNet;

namespace EpsilonCore.Commands;

/// <summary>
/// Result returned when queueing or sending commands.
/// </summary>
/// <param name="DisplayMessage">Message to indicate why this result occurred.
///     Exclude trailing periods from this string.</param>
/// <param name="ErrorLevel"><inheritdoc cref="Commands.ErrorLevel"/></param>
/// <param name="Command">The command to be enqueued/sent.</param>
/// <param name="ResultantMachine">The machine that would result if the command was run.
///     This will be null if the command failed to be queued.</param>
/// <param name="DataPacket">The data to send data to the microcontroller.
///     This will be null if the command failed to be queued or if there is no data to send.</param>
public record QueuedCommand(
    ErrorLevel ErrorLevel,
    string DisplayMessage,
    ICommand Command,
    Machine? ResultantMachine = null,
    DataPacket? DataPacket = null)
{
    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and no data needs to be sent to the microcontroller.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="resultantMachine">The machine after the command runs.</param>
    /// <returns></returns>
    public static QueuedCommand Success(ICommand command, Machine resultantMachine) => new(
        ErrorLevel.Success,
        $"Command queued",
        command,
        resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and data needs to be sent to the microcontroller.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="resultantMachine">The machine after the command runs.</param>
    /// <param name="dataPacket"><inheritdoc cref="DataPacket"/></param>
    /// <returns></returns>
    public static QueuedCommand Success(
        ICommand command,
        Machine resultantMachine,
        DataPacket dataPacket) => new(
            ErrorLevel.Success,
            $"Command queued",
            command,
            resultantMachine,
            dataPacket);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and no data needs to be sent to the microcontroller however something
    /// notable happened that should probably be reported.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="message">Warning message.</param>
    /// <param name="resultantMachine">The machine after the command runs.</param>
    /// <returns></returns>
    public static QueuedCommand Warning(ICommand command, string message, Machine resultantMachine) => new(
        ErrorLevel.Warning,
        message,
        command,
        resultantMachine);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was successful
    /// and data needs to be sent to the microcontroller something
    /// notable happened that should probably be reported.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="message">Warning message.</param>
    /// <param name="resultantMachine">The machine after the command runs.</param>
    /// <param name="dataPacket"><inheritdoc cref="DataPacket"/></param>
    /// <returns></returns>
    public static QueuedCommand Warning(
        ICommand command,
        string message,
        Machine resultantMachine,
        DataPacket dataPacket) => new(
            ErrorLevel.Warning,
            message,
            command,
            resultantMachine,
            dataPacket);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was unnecessary.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="displayMessage"></param>
    /// <returns></returns>
    public static QueuedCommand Unnecessary(ICommand command, string displayMessage) => new(
        ErrorLevel.Unnecessary,
        displayMessage,
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command was already queued.
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public static QueuedCommand AlreadyQueued(ICommand command) => new(
        ErrorLevel.Unnecessary,
        "Command has already been queued",
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command failed.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="displayMessage"></param>
    /// <returns></returns>
    public static QueuedCommand Error(ICommand command, string displayMessage) => new(
        ErrorLevel.Error,
        displayMessage,
        command);

    /// <summary>
    /// Returns a new <see cref="QueuedCommand"/> indicating the command failed
    /// because <see cref="Machine"/> doesn't contain the given <see cref="IKinematics"/>.
    /// </summary>
    /// <param name="command"></param>
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
    /// <param name="command"></param>
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
