using EpsilonCore.Helpers; using GenericHelpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Commands.MotionCommands;

/// <summary>
/// Command to move the machine to the specified position.
/// </summary>
public record MoveCommand : ICommand
{
    /// <summary>
    /// Final positions of the linear axes involved in the move.
    /// </summary>
    public IImmutableDictionary<uint, Length> PositionsLinear { get; private init; }

    /// <summary>
    /// Requested speed of the linear axes involved in the move.
    /// If both linear and rotational speeds are null, the move will be executed at the current speed of the machine.
    /// </summary>
    public Speed? RequestedSpeedLinear { get; private init; }

    /// <summary>
    /// Final positions of the rotational axes involved in the move.
    /// </summary>
    public IImmutableDictionary<uint, Angle> PositionsRotational { get; private init; }

    /// <summary>
    /// Requested speed of the rotational axes involved in the move.
    /// If both linear and rotational speeds are null, the move will be executed at the current speed of the machine.
    /// </summary>
    public RotationalSpeed? RequestedSpeedRotational { get; private init; }

    /// <inheritdoc />
    public uint? LineNumber { get; private init; } = null;

    /// <summary>
    /// Indicates whether the move is absolute or relative. 
    /// If true, the positions are the final position of the move.
    /// If false, they are relative to the current position.
    /// </summary>
    public bool AbsoluteMove { get; private init; } = true;

    /// <inheritdoc />
    public string Description { get; private init; }

    /// <inheritdoc />
    public bool RequiresZeroVelocity => false;

    /// <summary>
    /// Split linear moves into many small segments of this size.
    /// </summary>
    private static readonly Length segmentDistanceLinear = Length.FromMillimeters(0.1);

    /// <summary>
    /// Split rotational moves into many small segments of this size.
    /// </summary>
    private static readonly Angle segmentDistanceRotational = Angle.FromDegrees(0.1);

    private MoveCommand(
        IImmutableDictionary<uint, Length>? linearPositions,
        Speed? requestedSpeedLinear,
        IImmutableDictionary<uint, Angle>? rotationalPositions,
        RotationalSpeed? requestedSpeedRotational,
        bool absoluteMove,
        uint? lineNumber)
    {
        PositionsLinear = linearPositions is null ? ImmutableDictionary<uint, Length>.Empty : linearPositions;
        RequestedSpeedLinear = requestedSpeedLinear;
        PositionsRotational = rotationalPositions is null ? ImmutableDictionary<uint, Angle>.Empty : rotationalPositions;
        RequestedSpeedRotational = requestedSpeedRotational;
        Description = GetMoveDescription(linearPositions, requestedSpeedLinear, rotationalPositions, requestedSpeedRotational);
        LineNumber = lineNumber;
        AbsoluteMove = absoluteMove;
    }

    /// <summary>
    /// Returns either a new <see cref="MoveCommand"/> or an error if the inputs are invalid.
    /// </summary>
    /// <param name="linearPositions"></param>
    /// <param name="requestedSpeedLinear"></param>
    /// <param name="rotationalPositions"></param>
    /// <param name="requestedSpeedRotational"></param>
    /// <param name="absoluteMove"></param>
    /// <param name="lineNumber"></param>
    /// <returns></returns>
    public static Result<MoveCommand> New(
        IImmutableDictionary<uint, Length>? linearPositions = null,
        Speed? requestedSpeedLinear = null,
        IImmutableDictionary<uint, Angle>? rotationalPositions = null,
        RotationalSpeed? requestedSpeedRotational = null,
        bool absoluteMove = true,
        uint? lineNumber = null)
    {
        if ((linearPositions is null || linearPositions.Count <= 0) &&
           (rotationalPositions is null || rotationalPositions.Count <= 0))
            return new ArgumentException("Move contains no linear positions nor rotational positions.");

        if (requestedSpeedLinear < Speed.Zero || requestedSpeedRotational < RotationalSpeed.Zero)
            return new ArgumentException("Move cannot have a negative speed.");

        return new MoveCommand(
            linearPositions,
            requestedSpeedLinear,
            rotationalPositions,
            requestedSpeedRotational,
            absoluteMove,
            lineNumber);
    }

    private static string GetPositionsString<TKey, TValue>(IImmutableDictionary<TKey, TValue> positions)
    {
        return string.Join(", ", positions.Select(kvp => $"(initialAxis {kvp.Key}: {kvp.Value})"));
    }

    private static string GetMoveDescription(
        IImmutableDictionary<uint, Length>? linearPositions,
        Speed? linearSpeed,
        IImmutableDictionary<uint, Angle>? rotationalPositions,
        RotationalSpeed? rotationalSpeed)
    {
        string speedString;
        if (linearSpeed.HasValue)
            speedString = "at " + linearSpeed.Value.ToString();
        else if (rotationalSpeed.HasValue)
            speedString = "at " + rotationalSpeed.Value.ToString();
        else
            speedString = "at current speed.";

        bool hasLinearPositions = linearPositions is not null && linearPositions.Any();
        bool hasRotationalPositions = rotationalPositions is not null && rotationalPositions.Any();
        string linearPositionsString = $"linear positions: {(linearPositions is not null ? GetPositionsString(linearPositions) : string.Empty)}";
        string rotationalPositionsString = $"rotational positions: {(rotationalPositions is not null ? GetPositionsString(rotationalPositions) : string.Empty)}";
        if (hasLinearPositions && hasRotationalPositions)
            return $"Move to {linearPositionsString} {rotationalPositionsString} {speedString}";
        else if (hasLinearPositions)
            return $"Move to {linearPositionsString} {speedString}";
        else if (hasRotationalPositions)
            return $"Move to {rotationalPositionsString} {speedString}";
        else
            return $"Move missing positions";
    }

    /// <summary>
    /// Returns a <see cref="Move"/> result that represents the move specified by this command, 
    /// using the current positions of the machine as the starting point.
    /// </summary>
    /// <param name="initialMachine"></param>
    /// <returns></returns>
    public Result<Move> GetMove(Machine initialMachine)
    {
        CompositeKinematicSystem kinematicSystem = initialMachine.MotionSystem.CompositeKinematicSystem;

        // Get the final positions of all axes involved in the move, using the current position if no new position is specified.
        List<Length?> positionsFinalLinear = [];
        foreach (AxisLinear axis in kinematicSystem.AxesLinear)
        {
            if (PositionsLinear.TryGetValue(axis.Id, out Length pos))
                positionsFinalLinear.Add(pos);
            else
                positionsFinalLinear.Add(axis.Pos);
        }

        // Repeat with rotational axes.
        List<Angle?> positionsFinalRotational = [];
        foreach (AxisRotational axis in kinematicSystem.AxesRotational)
        {
            if (PositionsRotational.TryGetValue(axis.Id, out Angle pos))
                positionsFinalRotational.Add(pos);
            else
                positionsFinalRotational.Add(axis.Pos);
        }

        return Move.New(initialMachine.MotionSystem.Precisions,
            kinematicSystem,
            [.. kinematicSystem.AxesLinear.Select(axis => axis.Pos)],
            [.. kinematicSystem.AxesRotational.Select(axis => axis.Pos)],
            positionsFinalLinear,
            positionsFinalRotational,
            RequestedSpeedLinear,
            RequestedSpeedRotational);
    }

    /// <summary>
    /// The generic move algorithm works like this:
    ///     Figure out which axes are involved in the move by seeing which ones have a non-zero distance travelled.
    ///     Figure out which kinematic system each initialAxis belongs to.
    ///     Split move into many small segments (about 0.1 mm or 0.1 degrees).
    ///         We split it into small enough segments to assume that their won't be any crazy jumps in velocity between two adjacent segments.
    ///     For each kinematic system involved, solve the inverse kinematics at each of the segment end points to get the actuator positions.
    ///     Determine which actuator will limit the acceleration for each segment.
    ///     Set that actuator's acceleration to it's maximum value.
    ///     Determine what the other actuator's acceleration will be.
    ///         This is done by making sure the ratio of distances, velocities, and accelerations matches for each component of the move.
    ///         This is because distance travelled in a constant acceleration move is linear with both velocity and acceleration. 
    ///             ΔX = (Vi * Δt) + (0.5 * A * (Δt)^2)
    ///     Determine the time it will take to travel that segment.
    ///         Δt = (-(Vi^2) -+ (Vi^2 + (2 * A * ΔX))^0.5) / A
    ///     Determine what the final velocities will be at each segment.
    ///     If any final velocities exceed that actuator's maximum velocity, change all accelerations to zero and recompute the time using constant velocity.
    ///         Δt = ΔX / V
    ///     Now that there are times for each intermediate point, group sets of intermediate points into constant acceleration segments with a given tolerance.
    ///         Basically we will draw a parabola through the first point, the last point and best fit all the points between keeping the max error below a threshold.
    ///         We will include as many points as possible in a given segment.
    ///         Each segment will need to be sent to the microcontroller as a move segment with constant acceleration.
    /// </summary>
    /// <param name="initialMachine"></param>
    /// <returns></returns>
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        //StringBuilder output = new();
        //output.AppendLine("Duration,Angle Proximal,Angle Distal,Accel Proximal,Accel Distal,Vel Proximal,VelDistal");
        //for (int i = 0; i < moveSegments.IntermediatePosCount - 1; i++)
        //{
        //    output.AppendLine($"{moveSegments.Times[i].Seconds},{moveSegments.ActuatorInfosRotational[0].Positions[i].Degrees},{moveSegments.ActuatorInfosRotational[1].Positions[i].Degrees},{moveSegments.ActuatorInfosRotational[0].SegmentAccelerations[i].DegreesPerSecondSquared},{moveSegments.ActuatorInfosRotational[1].SegmentAccelerations[i].DegreesPerSecondSquared},{moveSegments.ActuatorInfosRotational[0].Velocities[i].DegreesPerSecond},{moveSegments.ActuatorInfosRotational[1].Velocities[i].DegreesPerSecond}");
        //}
        //File.WriteAllText("C:\\Users\\binte\\Downloads\\points.csv", output.ToString());
        //
        //stopwatch.Stop();
        //long millis = stopwatch.ElapsedMilliseconds;

        IImmutableDictionary<uint, AxisLinear> resultantLinearAxes = initialMachine.Entities.AxesLinear;
        foreach ((uint axisId, Length finalPosOrDistance) in PositionsLinear)
        {
            bool success = resultantLinearAxes.TryGetValue(axisId, out AxisLinear? initialAxis);

            if (!success || initialAxis is null)
                return QueuedCommand.Error(this, $"Axis {axisId} does not exist in machine '{initialMachine.Name}'.");

            if (initialAxis.Pos is null)
                return QueuedCommand.Error(this, $"Axis {axisId} '{initialAxis.Name}' has not been homed.");

            Length finalPos = AbsoluteMove ? finalPosOrDistance : initialAxis.Pos.Value + finalPosOrDistance;
            resultantLinearAxes.SetItem(axisId, initialAxis with { Pos = finalPos });
        }

        IImmutableDictionary<uint, AxisRotational> resultantRotationalAxes = initialMachine.Entities.AxesRotational;
        foreach ((uint axisId, Angle finalPosOrDistance) in PositionsRotational)
        {
            bool success = resultantRotationalAxes.TryGetValue(axisId, out AxisRotational? initialAxis);

            if (!success || initialAxis is null)
                return QueuedCommand.Error(this, $"Axis {axisId} does not exist in machine '{initialMachine.Name}'.");

            if (initialAxis.Pos is null)
                return QueuedCommand.Error(this, $"Axis {axisId} '{initialAxis.Name}' has not been homed.");

            Angle finalPos = AbsoluteMove ? finalPosOrDistance : initialAxis.Pos.Value + finalPosOrDistance;
            resultantRotationalAxes.SetItem(axisId, initialAxis with { Pos = finalPos });
        }

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                AxesLinear = resultantLinearAxes,
                AxesRotational = resultantRotationalAxes
            },
            MotionSystem = initialMachine.MotionSystem with
            {
                CompositeKinematicSystem = initialMachine.MotionSystem.CompositeKinematicSystem with
                {
                    AxesLinear = resultantLinearAxes.Values.ToImmutableList(),
                    AxesRotational = resultantRotationalAxes.Values.ToImmutableList()
                }
            }
        };

        return QueuedCommand.Success(this, initialMachine, resultantMachine);
    }
}
