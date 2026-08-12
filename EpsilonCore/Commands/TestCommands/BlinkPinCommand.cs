using EpsilonCore.Boards;
using EpsilonCore.Communication;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Units;
using System.Diagnostics;
using UnitsNet;

namespace EpsilonCore.Commands.TestCommands;

public record BlinkPinCommand(
    uint PinId,
    UInt32 BlinkCount,
    Duration BlinkTimeHigh,
    Duration BlinkTimeLow,
    uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Blinks pin {PinId} {BlinkCount} times holding pin high for {BlinkTimeHigh} and low for {BlinkTimeLow}";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => false;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        if (BlinkCount == 0)
            return QueuedCommand.Unnecessary(this, $"Blinking an pin 0 times is unnecessary");

        if (BlinkTimeHigh <= Duration.Zero)
            return QueuedCommand.Error(this, $"Durration high must be positive");

        if (BlinkTimeLow <= Duration.Zero)
            return QueuedCommand.Error(this, $"Durration low must be positive");

        bool success = initialMachine.Entities.Pins.TryGetValue(PinId, out Pin? pinToBlink);

        if (!success || pinToBlink is null)
            return QueuedCommand.Error(this, $"Pin {PinId} not assigned to machine '{initialMachine.Name}'");

        success = initialMachine.Entities.Boards.TryGetValue(pinToBlink.BoardId, out Board? board);

        if (!success || board is null)
            return QueuedCommand.Error(this, $"Board {pinToBlink.BoardId} not assigned to machine '{initialMachine.Name}'");

        Duration durationTotal = (BlinkTimeHigh + BlinkTimeLow) * BlinkCount;

        if (durationTotal > board.TimerLowPrecision.MaxDuration)
            return QueuedCommand.Error(this, $"Full command would take {durationTotal} which is longer than the board's timer can go: {board.TimerLowPrecision.MaxDuration}");

        if (durationTotal > board.TimerLowPrecision.MaxDuration)
            return QueuedCommand.Error(this, $"Full command would take {durationTotal} which is longer than the board's timer can go: {board.TimerLowPrecision.MaxDuration}");

        if (BlinkTimeHigh < board.TimerLowPrecision.TickPeriod)
            return QueuedCommand.Error(this, $"Durration high must be at least one timer tick: {board.TimerLowPrecision.TickPeriod}");

        if (BlinkTimeLow < board.TimerLowPrecision.TickPeriod)
            return QueuedCommand.Error(this, $"Durration low must be at least one timer tick: {board.TimerLowPrecision.TickPeriod}");

        byte[] blinkCountArr = board.ToByteArray(BlinkCount);
        float blinkTimeHigh = (float)CommunicationUnits.ToCommunicationDouble(BlinkTimeHigh);
        float blinkTimeLow = (float)CommunicationUnits.ToCommunicationDouble(BlinkTimeLow);
        byte[] blinkTimeHighArr = board.ToByteArray(blinkTimeHigh);
        byte[] blinkTimeLowArr = board.ToByteArray(blinkTimeLow);

        Result<DataPacket> dataPacket = DataPacket.New(
            board.CommunicatorId,
            0,
            CommandCode.BLINK_PIN,
            [pinToBlink.BoardsPinId, .. blinkCountArr, .. blinkTimeHighArr, .. blinkTimeLowArr]);

        Debug.Assert(dataPacket.IsSuccess);

        return QueuedCommand.Success(this, initialMachine, initialMachine, [dataPacket.Value]);
    }
}
