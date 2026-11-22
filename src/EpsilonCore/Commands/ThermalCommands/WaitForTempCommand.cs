using EpsilonCore.Boards;
using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Thermal;
using EpsilonCore.Units;
using GenericHelpers;
using UnitsNet;

namespace EpsilonCore.Commands.ThermalCommands;

/// <summary>
/// Waits until a temperature controller's thermometer reads between <see cref="MinTemp"/> and <see cref="MaxTemp"/>.
/// This is an <see cref="IConditionalCommand"/> whose result is predictable: waiting doesn't change any planned state,
/// so the commands after it are planned and sent before it finishes and the board holds them until the wait is over.
///
/// Payload: 1 byte thermometer pin id, min temp (double, kelvin), max temp (double, kelvin), timeout (double, seconds).
/// Result data: optionally the measured temperature (double, kelvin), which is logged.
/// </summary>
/// <param name="TempControllerIndex">Zero based index of the temperature controller to wait for.</param>
/// <param name="MinTemp">Lowest temperature that ends the wait.</param>
/// <param name="MaxTemp">Highest temperature that ends the wait.</param>
/// <param name="Timeout"><inheritdoc cref="IConditionalCommand.Timeout"/></param>
/// <param name="LineNumber"><inheritdoc cref="ICommand.LineNumber"/></param>
public record WaitForTempCommand(
    uint TempControllerIndex,
    Temperature MinTemp,
    Temperature MaxTemp,
    Duration Timeout,
    uint? LineNumber = null)
    : IConditionalCommand
{
    /// <inheritdoc />
    public string Description { get; } =
        $"Wait for temperature controller {TempControllerIndex} to reach {MinTemp} to {MaxTemp}";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public bool IsResultPredictable => true;

    /// <summary>
    /// Waits until the temperature is within <paramref name="tolerance"/> of <paramref name="targetTemp"/>.
    /// </summary>
    /// <param name="tempControllerIndex">Zero based index of the temperature controller to wait for.</param>
    /// <param name="targetTemp">Temperature to wait for.</param>
    /// <param name="tolerance">How far from the target temperature still ends the wait.</param>
    /// <param name="timeout">The longest the wait may take once it starts.</param>
    /// <param name="lineNumber">Optional line number for commands that come from a program.</param>
    /// <returns>The wait command.</returns>
    public static WaitForTempCommand AroundTarget(
        uint tempControllerIndex,
        Temperature targetTemp,
        TemperatureDelta tolerance,
        Duration timeout,
        uint? lineNumber = null) =>
        new(tempControllerIndex, targetTemp - tolerance, targetTemp + tolerance, timeout, lineNumber);

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        if (MinTemp > MaxTemp)
            return QueuedCommand.Error(this, $"Min temp ({MinTemp}) is greater than max temp ({MaxTemp})");

        if (Timeout <= Duration.Zero)
            return QueuedCommand.Error(this, "Timeout must be positive");

        if (!initialMachine.Entities.TempControllers.TryGetValue(TempControllerIndex, out TempController? tempController))
            return QueuedCommand.TempControllerNotAssignedToMachine(this, TempControllerIndex, initialMachine.Name);

        if (!initialMachine.Entities.Boards.TryGetValue(tempController.BoardId, out Board? board))
            return QueuedCommand.Error(this, $"Machine '{initialMachine.Name}' doesn't contain board {tempController.BoardId} but that is the board the temperature controller belongs to");

        if (!initialMachine.Entities.Thermometers.TryGetValue(tempController.ThermometerId, out Thermometer? thermometer))
            return QueuedCommand.Error(this, $"Machine '{initialMachine.Name}' doesn't contain thermometer {tempController.ThermometerId} used by temperature controller {TempControllerIndex}");

        Result<DataPacket> dataPacket = DataPacket.New(
            board.CommunicatorId,
            0,
            CommandCode.WAIT_FOR_TEMP,
            [
                thermometer.Pin.BoardsPinId,
                .. board.ToByteArray(CommunicationUnits.ToCommunicationDouble(MinTemp)),
                .. board.ToByteArray(CommunicationUnits.ToCommunicationDouble(MaxTemp)),
                .. board.ToByteArray(CommunicationUnits.ToCommunicationDouble(Timeout)),
            ]);

        if (dataPacket.IsError)
            return QueuedCommand.Error(this, dataPacket.Exception.Message);

        return QueuedCommand.Success(this, initialMachine, initialMachine, [dataPacket.Value]);
    }

    /// <inheritdoc />
    public Result<Machine> Resolve(QueuedCommand queuedCommand, IReadOnlyDictionary<uint, CommandOutcome> outcomes)
    {
        Machine? machine = queuedCommand.ResultantMachine;
        if (machine is null)
            return new ArgumentException($"{nameof(QueuedCommand.ResultantMachine)} is required to resolve '{Description}'.");

        // The measured temperature is reported but not stored in the machine: measured values aren't part of the
        // immutable machine state, and the commands after this one were already planned from the predicted machine.
        CommandOutcome? outcome = outcomes.Values.FirstOrDefault();
        if (outcome is not null && outcome.Data.Length >= sizeof(double))
        {
            Temperature measuredTemp = CommunicationUnits.ToTemperature(BitConverter.ToDouble(outcome.Data.AsSpan()[..sizeof(double)]));
            machine.ResultMessageLogger.Log(
                $"Temperature controller {TempControllerIndex} reached {machine.DisplayUnits.ToDisplay(measuredTemp)}.");
        }

        return machine;
    }
}
