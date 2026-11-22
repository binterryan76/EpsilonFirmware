using EpsilonCore.Actuator;
using EpsilonCore.Boards;
using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using EpsilonCore.Units;
using GenericHelpers;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Commands.MotionCommands;

/// <summary>
/// Command to move actuators until they hit the given endstops (homing).
/// This is an <see cref="IConditionalCommand"/>: it finishes when every endstop has triggered, which can't be predicted,
/// and the commands after it aren't planned until the boards report back.
///
/// Endstop to actuator mapping: endstop set <c>i</c> of the <see cref="CompositeKinematicSystem.EndstopSets"/>
/// homes actuator <c>i</c>, counting the composite kinematic system's linear actuators first and then its rotational
/// actuators. A min endstop moves the actuator in the negative direction at its homing speed, a max endstop in the
/// positive direction. Each endstop and its actuator must be on the same board.
///
/// When the command resolves, each homed endstop is marked activated and axis <c>i</c> (linear axes first, then rotational)
/// is set to its <see cref="AxisLinear.PosAtMinEndstop"/> / <see cref="AxisLinear.PosAtMaxEndstop"/>
/// (or its rotational equivalent). If that position isn't defined the axis is left unhomed, and a separate command
/// is needed to set its true position.
///
/// Payload per board: timeout (double, seconds), 1 byte entry count, then for each endstop:
/// 1 byte endstop pin id, 1 byte inverted flag, actuator id (uint, board int format), 1 byte (1 if linear actuator),
/// 1 byte direction (0 negative, 1 positive), homing speed (double, communication units).
/// Result data per board: 1 byte entry count, then for each endstop: 1 byte pin id, 1 byte (1 if triggered).
/// </summary>
/// <param name="EndstopIdsToHomeTo">Ids of the endstops to move into.</param>
/// <param name="LineNumber">Optional line number for commands that come from a program.</param>
public record MoveIntoEndstopsCommand(
    IImmutableList<uint> EndstopIdsToHomeTo,
    uint? LineNumber = null)
    : IConditionalCommand
{
    /// <summary>
    /// One endstop to home and the actuator it stops.
    /// </summary>
    private record HomingEntry(Endstop Endstop, int EndstopSetIndex, bool IsMinEndstop, IActuator Actuator, Board Board);

    /// <summary>
    /// Timeout used when <see cref="Timeout"/> isn't set.
    /// </summary>
    public static readonly Duration DefaultTimeout = Duration.FromSeconds(60);

    /// <inheritdoc />
    public string Description { get; } =
        $"Moves machine into endstops [{string.Join(", ", EndstopIdsToHomeTo)}]";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public bool IsResultPredictable => false;

    /// <inheritdoc />
    public Duration Timeout { get; init; } = DefaultTimeout;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Thrown if an actuator is neither an <see cref="IActuatorLinear"/> nor an <see cref="IActuatorRotational"/>.</exception>
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        Result<List<HomingEntry>> entries = GetHomingEntries(initialMachine);
        if (entries.IsError)
            return QueuedCommand.Error(this, entries.Exception.Message);

        if (Timeout <= Duration.Zero)
            return QueuedCommand.Error(this, "Timeout must be positive");

        List<DataPacket> dataPackets = [];
        foreach (IGrouping<uint, HomingEntry> boardEntries in entries.Value.GroupBy(entry => entry.Board.Id))
        {
            Board board = boardEntries.First().Board;
            List<byte> payload = [.. board.ToByteArray(CommunicationUnits.ToCommunicationDouble(Timeout)), (byte)boardEntries.Count()];

            foreach (HomingEntry entry in boardEntries)
            {
                double homingSpeed = entry.Actuator switch
                {
                    IActuatorLinear linear => CommunicationUnits.ToCommunicationDouble(linear.HomingSpeed),
                    IActuatorRotational rotational => CommunicationUnits.ToCommunicationDouble(rotational.HomingSpeed),
                    _ => throw new NotSupportedException($"Unsupported actuator type {entry.Actuator.GetType().Name}."),
                };

                payload.Add(entry.Endstop.Pin.BoardsPinId);
                payload.Add(entry.Endstop.Inverted ? (byte)1 : (byte)0);
                payload.AddRange(board.ToByteArray(entry.Actuator.Id));
                payload.Add(entry.Actuator is IActuatorLinear ? (byte)1 : (byte)0);
                payload.Add(entry.IsMinEndstop ? (byte)0 : (byte)1);
                payload.AddRange(board.ToByteArray(homingSpeed));
            }

            // Leave room for the timing header the engine adds when sending.
            if (payload.Count + DataPacket.MIN_DATA_LENGTH + CommandTiming.LENGTH > DataPacket.MAX_DATA_LENGTH)
                return QueuedCommand.Error(this, $"Too many endstops on board '{board.Name}' to fit in one data packet");

            Result<DataPacket> dataPacket = DataPacket.New(board.CommunicatorId, 0, CommandCode.MOVE_INTO_ENDSTOPS, [.. payload]);
            if (dataPacket.IsError)
                return QueuedCommand.Error(this, dataPacket.Exception.Message);

            dataPackets.Add(dataPacket.Value);
        }

        // The resultant machine can't be known until the endstops trigger, so the initial machine is used as a placeholder.
        // The engine won't plan anything after this command until Resolve provides the real machine.
        return QueuedCommand.Success(this, initialMachine, initialMachine, [.. dataPackets]);
    }

    /// <summary>
    /// Works out which actuator, direction and board each endstop homes, or returns an error if the endstops can't be homed.
    /// </summary>
    /// <param name="machine">The machine to home.</param>
    /// <returns>One entry per endstop, or an error.</returns>
    private Result<List<HomingEntry>> GetHomingEntries(Machine machine)
    {
        if (EndstopIdsToHomeTo.Count <= 0)
            return new ArgumentException("No endstops were given to move into.");

        CompositeKinematicSystem kinematicSystem = machine.MotionSystem.CompositeKinematicSystem;
        List<IActuator> actuators = [.. kinematicSystem.ActuatorsLinear, .. kinematicSystem.ActuatorsRotational];

        List<HomingEntry> entries = [];
        foreach (uint endstopId in EndstopIdsToHomeTo.Distinct())
        {
            if (!machine.Entities.Endstops.TryGetValue(endstopId, out Endstop? endstop))
                return new ArgumentException($"Endstop {endstopId} doesn't exist in machine '{machine.Name}'.");

            int setIndex = -1;
            for (int i = 0; i < kinematicSystem.EndstopSets.Count; i++)
            {
                EndstopSet set = kinematicSystem.EndstopSets[i];
                if (set.MinEndstopId == endstopId || set.MaxEndstopId == endstopId)
                {
                    setIndex = i;
                    break;
                }
            }

            if (setIndex < 0)
                return new ArgumentException($"Endstop '{endstop.Name}' isn't part of the kinematic system of machine '{machine.Name}'.");

            if (setIndex >= actuators.Count)
                return new ArgumentException($"Endstop '{endstop.Name}' is in endstop set {setIndex} but the kinematic system only has {actuators.Count} actuators.");

            IActuator actuator = actuators[setIndex];
            bool isMinEndstop = kinematicSystem.EndstopSets[setIndex].MinEndstopId == endstopId;

            if (!machine.Entities.Boards.TryGetValue(endstop.Pin.BoardId, out Board? board))
                return new ArgumentException($"Board {endstop.Pin.BoardId} of endstop '{endstop.Name}' doesn't exist in machine '{machine.Name}'.");

            if (actuator.BoardId != (int)endstop.Pin.BoardId)
                return new ArgumentException($"Endstop '{endstop.Name}' is on board {endstop.Pin.BoardId} but actuator '{actuator.Name}' is on board {actuator.BoardId}. Homing across boards isn't supported yet.");

            entries.Add(new HomingEntry(endstop, setIndex, isMinEndstop, actuator, board));
        }

        return entries;
    }

    /// <inheritdoc />
    public Result<Machine> Resolve(QueuedCommand queuedCommand, IReadOnlyDictionary<uint, CommandOutcome> outcomes)
    {
        Machine? initialMachine = queuedCommand.InitialMachine;
        if (initialMachine is null)
            return new ArgumentException($"{nameof(QueuedCommand.InitialMachine)} is required to resolve '{Description}'.");

        Result<List<HomingEntry>> entries = GetHomingEntries(initialMachine);
        if (entries.IsError)
            return entries.Exception;

        // Read which endstop pins triggered on each board.
        HashSet<(uint CommunicatorId, byte PinId)> triggeredPins = [];
        foreach ((uint communicatorId, CommandOutcome outcome) in outcomes)
        {
            ImmutableArray<byte> data = outcome.Data;
            if (data.Length < 1)
                continue;

            int count = data[0];
            for (int i = 0; i < count && 2 + (2 * i) <= data.Length - 1; i++)
            {
                byte pinId = data[1 + (2 * i)];
                bool triggered = data[2 + (2 * i)] != 0;
                if (triggered)
                    triggeredPins.Add((communicatorId, pinId));
            }
        }

        CompositeKinematicSystem kinematicSystem = initialMachine.MotionSystem.CompositeKinematicSystem;
        IImmutableDictionary<uint, Endstop> endstops = initialMachine.Entities.Endstops;
        IImmutableDictionary<uint, AxisLinear> axesLinear = initialMachine.Entities.AxesLinear;
        IImmutableDictionary<uint, AxisRotational> axesRotational = initialMachine.Entities.AxesRotational;

        foreach (HomingEntry entry in entries.Value)
        {
            if (!triggeredPins.Contains((entry.Board.CommunicatorId, entry.Endstop.Pin.BoardsPinId)))
                return new Exception($"Endstop '{entry.Endstop.Name}' didn't trigger.");

            endstops = endstops.SetItem(entry.Endstop.Id, entry.Endstop with { Activated = true });

            int linearCount = kinematicSystem.AxesLinear.Count;
            if (entry.EndstopSetIndex < linearCount)
            {
                AxisLinear axis = kinematicSystem.AxesLinear[entry.EndstopSetIndex];
                if (axesLinear.TryGetValue(axis.Id, out AxisLinear? entityAxis))
                    axesLinear = axesLinear.SetItem(axis.Id, entityAxis with
                    {
                        Pos = entry.IsMinEndstop ? entityAxis.PosAtMinEndstop : entityAxis.PosAtMaxEndstop
                    });
            }
            else if (entry.EndstopSetIndex - linearCount < kinematicSystem.AxesRotational.Count)
            {
                AxisRotational axis = kinematicSystem.AxesRotational[entry.EndstopSetIndex - linearCount];
                if (axesRotational.TryGetValue(axis.Id, out AxisRotational? entityAxis))
                    axesRotational = axesRotational.SetItem(axis.Id, entityAxis with
                    {
                        Pos = entry.IsMinEndstop ? entityAxis.PosAtMinEndstop : entityAxis.PosAtMaxEndstop
                    });
            }
        }

        Machine homedMachine = initialMachine.WithAxes(axesLinear, axesRotational);
        return homedMachine with { Entities = homedMachine.Entities with { Endstops = endstops } };
    }
}
