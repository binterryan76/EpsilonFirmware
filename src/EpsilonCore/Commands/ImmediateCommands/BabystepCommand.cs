using EpsilonCore.Actuator;
using EpsilonCore.Boards;
using EpsilonCore.Communication;
using EpsilonCore.Motion;
using EpsilonCore.Units;
using GenericHelpers;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Commands.ImmediateCommands;

/// <summary>
/// Shifts one actuator by a small amount while the machine is running, for example to adjust the first layer height
/// during a print. The board applies the offset on top of whatever it is running, so already planned commands don't
/// change. The total offset is recorded in <see cref="LiveAdjustments"/>.
///
/// Payload: actuator id (uint, board int format), 1 byte (1 if linear, 0 if rotational),
/// offset (double, communication units: mm or radians).
/// </summary>
public record BabystepCommand : ILiveAdjustmentCommand
{
    /// <summary>
    /// Id of the actuator to shift.
    /// </summary>
    public uint ActuatorId { get; }

    /// <summary>
    /// True if <see cref="ActuatorId"/> is a linear actuator, false if it is rotational.
    /// </summary>
    public bool IsLinear { get; }

    /// <summary>
    /// Offset for a linear actuator.
    /// </summary>
    public Length OffsetLinear { get; }

    /// <summary>
    /// Offset for a rotational actuator.
    /// </summary>
    public Angle OffsetRotational { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public ImmediateHostAction HostAction => ImmediateHostAction.None;

    /// <summary>
    /// Creates a babystep. Use <see cref="Linear"/> or <see cref="Rotational"/>.
    /// </summary>
    /// <param name="actuatorId">Id of the actuator to shift.</param>
    /// <param name="isLinear">True if the actuator is linear, false if rotational.</param>
    /// <param name="offsetLinear">Offset for a linear actuator.</param>
    /// <param name="offsetRotational">Offset for a rotational actuator.</param>
    private BabystepCommand(uint actuatorId, bool isLinear, Length offsetLinear, Angle offsetRotational)
    {
        ActuatorId = actuatorId;
        IsLinear = isLinear;
        OffsetLinear = offsetLinear;
        OffsetRotational = offsetRotational;
        Description = isLinear
            ? $"Babystep linear actuator {actuatorId} by {offsetLinear}"
            : $"Babystep rotational actuator {actuatorId} by {offsetRotational}";
    }

    /// <summary>
    /// Babystep a linear actuator.
    /// </summary>
    /// <param name="actuatorLinearId">Id of the linear actuator.</param>
    /// <param name="offset">How far to shift the actuator.</param>
    /// <returns>The babystep command.</returns>
    public static BabystepCommand Linear(uint actuatorLinearId, Length offset) =>
        new(actuatorLinearId, true, offset, Angle.Zero);

    /// <summary>
    /// Babystep a rotational actuator.
    /// </summary>
    /// <param name="actuatorRotationalId">Id of the rotational actuator.</param>
    /// <param name="offset">How far to shift the actuator.</param>
    /// <returns>The babystep command.</returns>
    public static BabystepCommand Rotational(uint actuatorRotationalId, Angle offset) =>
        new(actuatorRotationalId, false, Length.Zero, offset);

    /// <inheritdoc />
    public Result<IImmutableList<DataPacket>> CreateDataPackets(ImmediateCommandContext context)
    {
        IActuator? actuator = IsLinear
            ? context.CurrentMachine.Entities.ActuatorsLinear.GetValueOrDefault(ActuatorId)
            : context.CurrentMachine.Entities.ActuatorsRotational.GetValueOrDefault(ActuatorId);

        if (actuator is null)
            return new ArgumentException($"{(IsLinear ? "Linear" : "Rotational")} actuator {ActuatorId} doesn't exist in machine '{context.CurrentMachine.Name}'.");

        if (actuator.BoardId < 0 || !context.CurrentMachine.Entities.Boards.TryGetValue((uint)actuator.BoardId, out Board? board))
            return new ArgumentException($"Board {actuator.BoardId} of actuator '{actuator.Name}' doesn't exist in machine '{context.CurrentMachine.Name}'.");

        double offset = IsLinear
            ? CommunicationUnits.ToCommunicationDouble(OffsetLinear)
            : CommunicationUnits.ToCommunicationDouble(OffsetRotational);

        Result<DataPacket> dataPacket = DataPacket.New(
            board.CommunicatorId,
            0,
            CommandCode.BABYSTEP,
            [.. board.ToByteArray(ActuatorId), IsLinear ? (byte)1 : (byte)0, .. board.ToByteArray(offset)]);

        if (dataPacket.IsError)
            return dataPacket.Exception;

        return ImmutableList.Create(dataPacket.Value);
    }

    /// <inheritdoc />
    public void ApplyTo(LiveAdjustments liveAdjustments)
    {
        if (IsLinear)
            liveAdjustments.AddOffset(ActuatorId, OffsetLinear);
        else
            liveAdjustments.AddOffset(ActuatorId, OffsetRotational);
    }
}
