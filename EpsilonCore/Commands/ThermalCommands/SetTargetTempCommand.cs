using EpsilonCore.Boards;
using EpsilonCore.Communication;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Thermal;
using EpsilonCore.Units;
using System.Diagnostics;
using UnitsNet;

namespace EpsilonCore.Commands.ThermalCommands;

/// <summary>
/// Command to set the target temperature for a temperature controller.
/// </summary>
/// <param name="TempControllerIndex">Zero based index of the temperature controller to set the target temperature of.</param>
/// <param name="TargetTemp">The temperature that the target temperature will be changed to.</param>
/// <param name="LineNumber"><inheritdoc cref="ICommand.LineNumber"/></param>
public record SetTargetTempCommand(
    uint TempControllerIndex,
    Temperature TargetTemp,
    uint? LineNumber = null)
    : ICommand
{
    /// <inheritdoc />
    public string Description { get; } =
        $"Set temperarure conroller {TempControllerIndex} target temp to {TargetTemp}";

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        initialMachine.Entities.TempControllers.TryGetValue(TempControllerIndex, out TempController? initialTempController);

        if (initialTempController is null)
            return QueuedCommand.TempControllerNotAssignedToMachine(this, TempControllerIndex, initialMachine.Name);

        initialMachine.Entities.Boards.TryGetValue(initialTempController.BoardId, out Board? board);

        if (board is null)
            return QueuedCommand.Error(this, $"Machine '{initialMachine.Name}' doesn't contain board {initialTempController.BoardId} but that is the board the temperature controller belongs to.");

        if (TargetTemp > initialTempController.MaxTargetTemp)
            return QueuedCommand.TargetTempTooHigh(this, TargetTemp, initialTempController.MaxTargetTemp, initialMachine.DisplayUnits);
        else if (TargetTemp.Equals(initialTempController.TargetTemp, Temperature.FromKelvins(0.01))) // TODO: make precision settable 
            return QueuedCommand.AlreadyAtTargetTemp(this, TempControllerIndex, TargetTemp, initialMachine.DisplayUnits);

        Machine resultMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                TempControllers = initialMachine.Entities.TempControllers.SetItem(TempControllerIndex,
                    initialTempController with { TargetTemp = TargetTemp })
            }
        };

        double setTemp = CommunicationUnits.ToCommunicationDouble(TargetTemp);
        Result<DataPacket> dataPacket = DataPacket.New(
            board.CommunicatorId,
            0,
            CommandCode.SET_TARGET_TEMP,
            board.ToByteArray(setTemp));

        Debug.Assert(dataPacket.IsSuccess);

        return QueuedCommand.Success(this, resultMachine, dataPacket.Value);
    }
}
