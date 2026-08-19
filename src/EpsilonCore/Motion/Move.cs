using EpsilonCore.Actuator;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using System.Collections.Immutable;
using System.Diagnostics;
using UnitsNet;
using UnitsNetHelpers;
using static System.Math;

namespace EpsilonCore.Motion;

/// <summary>
/// Represents a move for all axes of the <see cref="Machine"/>'s <see cref="CompositeKinematicSystem"/>.
/// A <see cref="Move"/> is composed of many sample points spaced evenly from the start point to the end point.
/// A <see cref="Move"/> is composed of many segments going between each adjacent pair of points.
/// These points and segments are used to calculate the inverse kinematics at each point so we can 
/// determine how far the motors need to move and what accelerations and speeds can be used.
/// </summary>
public class Move
{
    /// <summary>
    /// Linear moves will be broken into segments of this length.
    /// </summary>
    private static readonly Length moveSegmentLengthLinear = Length.FromMillimeters(0.1);

    /// <summary>
    /// Rotational moves will be broken into segments of this angle.
    /// </summary>
    private static readonly Angle moveSegmentLengthRotational = Angle.FromDegrees(0.1);

    /// <summary>
    /// This tells the move what positional tolerance is acceptable when sampling points along the path from
    /// the first point in <see cref="Points"/> to the last point.
    /// </summary>
    public MotionSystemPrecisions Precisions { get; }

    /// <summary>
    /// This stores a reference to the <see cref="Machine"/>'s <see cref="CompositeKinematicSystem"/> at the time 
    /// the move was created to be used when solving the step pulse times.
    /// </summary>
    public CompositeKinematicSystem KinematicSystem { get; }

    /// <summary>
    /// List of all the sample points.
    /// The first point is the initial position of the move.
    /// The final point is the final position of the move.
    /// </summary>
    public IList<MovePoint> Points { get; set; }

    /// <summary>
    /// List of all the segments between adjacent pairs of sample points.
    /// There will always be at least one segment.
    /// There will always be exactly one more point than the number of segments.
    /// </summary>
    //public IList<MoveSegment> Segments { get; set; }

    /// <summary>
    /// Requested top linear speed to reach when moving from the initial position to the final position.
    /// </summary>
    public Speed? RequestedSpeedLinear { get; }

    /// <summary>
    /// Requested top rotational speed to reach when moving from the initial position to the final position.
    /// </summary>
    public RotationalSpeed? RequestedSpeedRotational { get; }

    /// <summary>
    /// Total direct distance from the initial position to the final position of the linear moves.
    /// </summary>
    public Length AxisHypotenuseDistanceLinear { get; }

    /// <summary>
    /// Total direct distance from the initial position to the final position of the rotational moves.
    /// </summary>
    public Angle AxisHypotenuseDistanceRotational { get; }

    /// <summary>
    /// Distance traveled by each <see cref="AxisLinear"/>.
    /// </summary>
    public List<Length?> AxisComponentDistancesLinear { get; }

    /// <summary>
    /// Distance traveled by each <see cref="AxisRotational"/>.
    /// </summary>
    public List<Angle?> AxisComponentDistancesRotational { get; }

    /// <summary>
    /// List of indicies of <see cref="Points"/> to closely approximate the true actuator trajectories.
    /// This simplified list of points is much faster to send to the microcontroller and still keeps the
    /// tolerance of the move within an acceptable range.
    /// </summary>
    public IReadOnlyList<int> SimplifiedPointIndicies { get; set; } = [];

    private Move(
        MotionSystemPrecisions precisions,
        CompositeKinematicSystem kinematicSystem,
        Speed? requestedSpeedLinear,
        RotationalSpeed? requestedSpeedRotational,
        List<Length?> axisComponentDistancesLinear,
        List<Angle?> axisComponentDistancesRotational,
        Length axisHypotenuseDistanceLinear,
        Angle axisHypotenuseDistanceRotational,
        IList<MovePoint> points)
    {
        Precisions = precisions;
        KinematicSystem = kinematicSystem;
        RequestedSpeedLinear = requestedSpeedLinear;
        RequestedSpeedRotational = requestedSpeedRotational;
        AxisComponentDistancesLinear = axisComponentDistancesLinear;
        AxisComponentDistancesRotational = axisComponentDistancesRotational;
        AxisHypotenuseDistanceLinear = axisHypotenuseDistanceLinear;
        AxisHypotenuseDistanceRotational = axisHypotenuseDistanceRotational;
        Points = points;
    }

    /// <summary>
    /// Creates a new <see cref="Move"/> object and automatically sets the sample points and segments.
    /// It does many checks to ensure the inputs have the same number of axes as the <see cref="Machine"/>'s
    /// <see cref="CompositeKinematicSystem"/> degrees of freedom, etc.
    /// This will set every member property on initialization.
    /// It will also calculate the inverse kinematics at every point.
    /// Will assign the axis and actuator positions of every point.
    /// </summary>
    /// <param name="precisions"><inheritdoc cref="Move.Precisions"/></param>
    /// <param name="kinematicSystem"><inheritdoc cref="Move.KinematicSystem"/></param>
    /// <param name="initialAxisPositionsLinear">Initial positions of all the <see cref="Machine"/>'s <see cref="AxisLinear"/>s.</param>
    /// <param name="initialAxisPositionsRotational">Initial positions of all the <see cref="Machine"/>'s <see cref="AxisRotational"/>s.</param>
    /// <param name="finalAxisPositionsLinear">Final positions of all the <see cref="Machine"/>'s <see cref="AxisLinear"/>s.</param>
    /// <param name="finalAxisPositionsRotational">Final positions of all the <see cref="Machine"/>'s <see cref="AxisRotational"/>s.</param>
    /// <param name="requestedSpeedLinear">Requested top linear speed to reach when moving from <paramref name="initialAxisPositionsLinear"/> to <paramref name="finalAxisPositionsLinear"/>.</param>
    /// <param name="requestedSpeedRotational">Requested top rotational speed to reach when moving from <paramref name="initialAxisPositionsRotational"/> to <paramref name="finalAxisPositionsRotational"/>.</param>
    /// <returns></returns>
    public static Result<Move> New(
        MotionSystemPrecisions precisions,
        CompositeKinematicSystem kinematicSystem,
        IList<Length?> initialAxisPositionsLinear,
        IList<Angle?> initialAxisPositionsRotational,
        IList<Length?> finalAxisPositionsLinear,
        IList<Angle?> finalAxisPositionsRotational,
        Speed? requestedSpeedLinear,
        RotationalSpeed? requestedSpeedRotational)
    {
        if (initialAxisPositionsLinear.Count != kinematicSystem.AxesLinear.Count)
            return new ArgumentException(
                $"The kinematics require {kinematicSystem.AxesLinear.Count} initial linear axis positions but {initialAxisPositionsLinear.Count} were provided.");

        if (initialAxisPositionsRotational.Count != kinematicSystem.AxesRotational.Count)
            return new ArgumentException(
                $"The kinematics require {kinematicSystem.AxesRotational.Count} initial rotational axis positions but {initialAxisPositionsRotational.Count} were provided.");

        if (finalAxisPositionsLinear.Count != kinematicSystem.AxesLinear.Count)
            return new ArgumentException(
                $"The kinematics require {kinematicSystem.AxesLinear.Count} final linear axis positions but {finalAxisPositionsLinear.Count} were provided.");

        if (finalAxisPositionsRotational.Count != kinematicSystem.AxesRotational.Count)
            return new ArgumentException(
                $"The kinematics require {kinematicSystem.AxesRotational.Count} final rotational axis positions but {finalAxisPositionsRotational.Count} were provided.");

        if (requestedSpeedLinear is null && requestedSpeedRotational is null)
            return new ArgumentException(
                $"Moves require either a linear speed or rotational speed but none were provided.");

        if (kinematicSystem.AxesLinear.Count <= 0 && kinematicSystem.AxesRotational.Count <= 0)
            return new ArgumentException(
                $"Kinematics must have at least 1 axis, either linear or rotational.");

        bool canUseRequestedSpeedLinear = requestedSpeedLinear is not null && kinematicSystem.AxesLinear.Count > 0;
        bool canUseRequestedSpeedRotational = requestedSpeedRotational is not null && kinematicSystem.AxesRotational.Count > 0;

        if (!canUseRequestedSpeedLinear && !canUseRequestedSpeedRotational)
            return new ArgumentException(
                $"Kinematics must have at a linear axis and a requested linear speed or have a rotational axis and a rotational speed.");

        // Ensure destination of all axes are within their travel limits and affected axes are homed.
        for (int i = 0; i < kinematicSystem.AxesLinear.Count; i++)
        {
            Length? destination = finalAxisPositionsLinear[i];
            AxisLinear axis = kinematicSystem.AxesLinear[i];
            if (destination is not null && axis.PosOutOfRange(destination.Value))
                return new ArgumentException($"Final position ({destination}) for linear axis '{axis.Name}' is out of bounds.");

            if (!axis.IsHomed)
                return new ArgumentException($"Linear axis '{axis.Name}' must be homed before it can be moved.");
        }

        for (int i = 0; i < kinematicSystem.AxesRotational.Count; i++)
        {
            Angle? destination = finalAxisPositionsRotational[i];
            AxisRotational axis = kinematicSystem.AxesRotational[i];
            if (destination is not null && axis.PosOutOfRange(destination.Value))
                return new ArgumentException($"Final position ({destination}) for rotational axis '{axis.Name}' is out of bounds.");

            if (!axis.IsHomed)
                return new ArgumentException($"Rotational axis '{axis.Name}' must be homed before it can be moved.");
        }

        // get component distances
        // distance = final - initial
        List<Length?> axisComponentDistancesLinear = [.. finalAxisPositionsLinear.Zip(initialAxisPositionsLinear, (final, initial) => final - initial)];
        List<Angle?> axisComponentDistancesRotational = [.. finalAxisPositionsRotational.Zip(initialAxisPositionsRotational, (final, initial) => final - initial)];


        // get hypotenuse distances
        // hypotenuse distance = Sqrt(d1^2 + d2^2 + ...)
        Length axisHypotenuseDistanceLinear = UnitsNetHelpers.Helpers.Distance(axisComponentDistancesLinear);
        Angle axisHypotenuseDistanceRotational = UnitsNetHelpers.Helpers.Distance(axisComponentDistancesRotational);

        if (axisHypotenuseDistanceLinear.Equals(Length.Zero, precisions.PrecisionLinear) &&
           axisHypotenuseDistanceRotational.Equals(Angle.Zero, precisions.PrecisionRotational))
            return new ArgumentException(
                $"Move must have a distance of at least {precisions.PrecisionLinear} or {precisions.PrecisionRotational}.");

        int segmentCount = Max(
            (int)Ceiling(axisHypotenuseDistanceLinear / moveSegmentLengthLinear),
            (int)Ceiling(axisHypotenuseDistanceRotational / moveSegmentLengthRotational));

        int pointCount = segmentCount + 1;

        Debug.Assert(segmentCount > 0, "There must be at least one segment.");

        //List<MoveSegment> segments = new(segmentCount);
        List<MovePoint> points = new(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            double pathRatio = (double)i / pointCount;

            // get axis positions at each point
            IImmutableList<Length?> currentAxisPositionsLinear = [.. axisComponentDistancesLinear
                .Zip(initialAxisPositionsLinear, (distance, initialPos) => initialPos + pathRatio * distance)];

            IImmutableList<Angle?> currentAxisPositionsRotational = [.. axisComponentDistancesRotational
                .Zip(initialAxisPositionsRotational, (distance, initialPos) => initialPos + pathRatio * distance)];

            // do inverse kinematics to get actuator positions at each point
            Result<(IImmutableList<Length?>, IImmutableList<Angle?>)> inverseKinematics =
                kinematicSystem.InverseKinematics(currentAxisPositionsLinear, currentAxisPositionsRotational);

            if (inverseKinematics.IsError)
                return inverseKinematics.Exception;

            (IImmutableList<Length?> currentActuatorPositionsLinear,
             IImmutableList<Angle?> currentActuatorPositionsRotational) = inverseKinematics.Value;


            MovePoint point = new(
                pathRatio,
                currentAxisPositionsLinear,
                currentAxisPositionsRotational,
                currentActuatorPositionsLinear,
                currentActuatorPositionsRotational);

            points.Add(point);
        }

        return new Move(
            precisions,
            kinematicSystem,
            requestedSpeedLinear,
            requestedSpeedRotational,
            axisComponentDistancesLinear,
            axisComponentDistancesRotational,
            axisHypotenuseDistanceLinear,
            axisHypotenuseDistanceRotational,
            points);
    }


    public void CalculateActuatorDerivativesAndVelocityLimits()
    {
        CalculateActuatorDerivatives(Points);
        CalculateVelocityLimits(Points, KinematicSystem);
    }

    public void CalculateDsDtValuesAndPointTimes()
    {
        CalculateDsDtValues(Points, KinematicSystem);
        CalculateTimes(Points);
    }

    /// <summary>
    /// Computes <see cref="MovePoint.ActuatorPositionDerivatives1Linear"/>,
    /// <see cref="MovePoint.ActuatorPositionDerivatives1Rotational"/>,
    /// <see cref="MovePoint.ActuatorPositionDerivatives2Linear"/>, and
    /// <see cref="MovePoint.ActuatorPositionDerivatives2Rotational"/> for every point.
    /// </summary>
    /// <param name="points"></param>
    private static void CalculateActuatorDerivatives(IList<MovePoint> points)
    {
        int actuatorCountLinear = points[0].ActuatorPositionsLinear.Count;
        int actuatorCountRotational = points[0].ActuatorPositionsRotational.Count;

        for (int iPoint = 0; iPoint < points.Count; iPoint++)
        {
            MovePoint currentPoint = points[iPoint];
            if (iPoint == 0)
            {
                // Derivatives of the first point are calculated with forward difference.
                // dƟ/ds ≈ (ƟNext - ƟCurrent) / (sNext - sCurrent).
                MovePoint nextPoint = points[iPoint + 1];
                double ds = nextPoint.PathRatio - currentPoint.PathRatio;

                // TODO: figure out what Claude means by this:
                // Second derivative at the very start of the move mainly matters when
                // path velocity is already nonzero there (e.g. a blended junction).
                // At a standstill start the curvature term drops out of the acceleration
                // constraint anyway (it is multiplied by velocity squared), so copying the
                // nearest interior estimate is a safe, simple approximation.

                for (int iActuator = 0; iActuator < actuatorCountLinear; iActuator++)
                {
                    Length? currentPos = currentPoint.ActuatorPositionsLinear[iActuator];
                    Length? nextPos = nextPoint.ActuatorPositionsLinear[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Linear[iActuator] = (nextPos - currentPos) / ds;
                    currentPoint.ActuatorPositionDerivatives2Linear[iActuator] = currentPos.HasValue ? Length.Zero : null;
                }

                for (int iActuator = 0; iActuator < actuatorCountRotational; iActuator++)
                {
                    Angle? currentPos = currentPoint.ActuatorPositionsRotational[iActuator];
                    Angle? nextPos = nextPoint.ActuatorPositionsRotational[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Rotational[iActuator] = (nextPos - currentPos) / ds;
                    currentPoint.ActuatorPositionDerivatives2Rotational[iActuator] = currentPos.HasValue ? Angle.Zero : null;
                }
            }
            else if (iPoint == points.Count - 1)
            {
                // Derivatives of the last point are calculated with backward difference.
                // dƟ/ds ≈ (ƟCurrent - ƟPrevious) / (sCurrent - sPrevious).
                MovePoint previousPoint = points[iPoint - 1];
                double ds = currentPoint.PathRatio - previousPoint.PathRatio;

                for (int iActuator = 0; iActuator < actuatorCountLinear; iActuator++)
                {
                    Length? currentPos = currentPoint.ActuatorPositionsLinear[iActuator];
                    Length? previousPos = previousPoint.ActuatorPositionsLinear[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Linear[iActuator] = (currentPos - previousPos) / ds;
                    currentPoint.ActuatorPositionDerivatives2Linear[iActuator] = currentPos.HasValue ? Length.Zero : null;
                }

                for (int iActuator = 0; iActuator < actuatorCountRotational; iActuator++)
                {
                    Angle? currentPos = currentPoint.ActuatorPositionsRotational[iActuator];
                    Angle? previousPos = previousPoint.ActuatorPositionsRotational[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Rotational[iActuator] = (currentPos - previousPos) / ds;
                    currentPoint.ActuatorPositionDerivatives2Rotational[iActuator] = currentPos.HasValue ? Angle.Zero : null;
                }
            }
            else
            {
                // Derivatives of all other points are calculated with central difference.
                // The spacing between points is constant in axis space but not in actuator space so
                // we will use the formula for unequal point spacing.
                MovePoint previousPoint = points[iPoint - 1];
                MovePoint nextPoint = points[iPoint + 1];
                double dsBehind = currentPoint.PathRatio - previousPoint.PathRatio;
                double dsAhead = nextPoint.PathRatio - currentPoint.PathRatio;

                for (int iActuator = 0; iActuator < actuatorCountLinear; iActuator++)
                {
                    Length? previousPos = previousPoint.ActuatorPositionsLinear[iActuator];
                    Length? currentPos = currentPoint.ActuatorPositionsLinear[iActuator];
                    Length? nextPos = nextPoint.ActuatorPositionsLinear[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Linear[iActuator] =
                        (dsBehind * dsBehind * (nextPos - currentPos) + dsAhead * dsAhead * (currentPos - previousPos))
                            / (dsBehind * dsAhead * (dsBehind + dsAhead));
                    currentPoint.ActuatorPositionDerivatives2Linear[iActuator] =
                        2.0 * (dsBehind * (nextPos - currentPos) - dsAhead * (currentPos - previousPos))
                            / (dsBehind * dsAhead * (dsBehind + dsAhead));
                }

                for (int iActuator = 0; iActuator < actuatorCountRotational; iActuator++)
                {
                    Angle? previousPos = previousPoint.ActuatorPositionsRotational[iActuator];
                    Angle? currentPos = currentPoint.ActuatorPositionsRotational[iActuator];
                    Angle? nextPos = nextPoint.ActuatorPositionsRotational[iActuator];
                    currentPoint.ActuatorPositionDerivatives1Rotational[iActuator] =
                        (dsBehind * dsBehind * (nextPos - currentPos) + dsAhead * dsAhead * (currentPos - previousPos))
                            / (dsBehind * dsAhead * (dsBehind + dsAhead));
                    currentPoint.ActuatorPositionDerivatives2Rotational[iActuator] =
                        2.0 * (dsBehind * (nextPos - currentPos) - dsAhead * (currentPos - previousPos))
                            / (dsBehind * dsAhead * (dsBehind + dsAhead));
                }
            }
        }
    }

    /// <summary>
    /// Calculates <see cref="MovePoint.ActuatorLimitedDsDt"/> for every point.
    /// 
    /// </summary>
    /// <param name="points"></param>
    /// <param name="kinematicSystem"></param>
    private static void CalculateVelocityLimits(IList<MovePoint> points, CompositeKinematicSystem kinematicSystem)
    {
        int actuatorCountLinear = points[0].ActuatorPositionsLinear.Count;
        int actuatorCountRotational = points[0].ActuatorPositionsRotational.Count;

        Debug.Assert(kinematicSystem.ActuatorsLinear.Count == actuatorCountLinear);
        Debug.Assert(kinematicSystem.ActuatorsRotational.Count == actuatorCountRotational);

        for (int iPoint = 0; iPoint < points.Count; iPoint++)
        {
            MovePoint currentPoint = points[iPoint];
            Frequency actuatorLimitedDsDt = Frequency.FromHertz(double.MaxValue);

            for (int iActuator = 0; iActuator < actuatorCountLinear; iActuator++)
            {
                Length? firstDerivative = currentPoint.ActuatorPositionDerivatives1Linear[iActuator];

                if (!firstDerivative.HasValue)
                    continue;

                // The built-in UnitsNet Abs() function is super slow for some reason...
                Length firstDerivativeMagnitude = Length.FromMillimeters(Abs(firstDerivative.Value.Millimeters));

                if (firstDerivativeMagnitude > Constants.VERY_SMALL_LENGTH)
                {
                    IActuatorLinear actuator = kinematicSystem.ActuatorsLinear[iActuator];
                    Frequency candidateDsDt = actuator.MaxSpeed / firstDerivativeMagnitude;
                    actuatorLimitedDsDt = UnitsNetHelpers.Helpers.Min(actuatorLimitedDsDt, candidateDsDt);
                }
            }

            for (int iActuator = 0; iActuator < actuatorCountRotational; iActuator++)
            {
                Angle? firstDerivative = currentPoint.ActuatorPositionDerivatives1Rotational[iActuator];

                if (!firstDerivative.HasValue)
                    continue;

                // The built-in UnitsNet Abs() function is super slow for some reason...
                Angle firstDerivativeMagnitude = Angle.FromRadians(Abs(firstDerivative.Value.Radians));

                if (firstDerivativeMagnitude > Constants.VERY_SMALL_ANGLE)
                {
                    IActuatorRotational actuator = kinematicSystem.ActuatorsRotational[iActuator];
                    Frequency candidateDsDt = actuator.MaxSpeed / firstDerivativeMagnitude;
                    actuatorLimitedDsDt = UnitsNetHelpers.Helpers.Min(actuatorLimitedDsDt, candidateDsDt);
                }
            }

            currentPoint.ActuatorLimitedDsDt = actuatorLimitedDsDt;
        }
    }

    private static void CalculateDsDtValues(IList<MovePoint> points, CompositeKinematicSystem kinematicSystem)
    {
        // The time-optimal profile at every sample is the lower envelope of the two passes:
        // It must be slow enough that the forward pass could have reached it, and slow
        // enough that the backward pass could still decelerate to the required end condition.
        Frequency[] forwardPassDsDt = new Frequency[points.Count];
        Frequency[] backwardPassDsDt = new Frequency[points.Count];

        //TODO: make this work with non-zero start and end velocities. In other words allow junction velocities to be used.
        forwardPassDsDt[0] = Frequency.Zero;
        for (int i = 0; i < points.Count - 1; i++)
        {
            MovePoint currentPoint = points[i];
            MovePoint nextPoint = points[i + 1];
            double ds = nextPoint.PathRatio - currentPoint.PathRatio;
            Frequency2 maxPathD2sDt2 = CalculateMaxPathD2sDt2(
                currentPoint, forwardPassDsDt[i], kinematicSystem, returnMinPathAcceleration: false);
            Frequency2 candidateNextD2sDt2 = forwardPassDsDt[i] * forwardPassDsDt[i] + 2.0 * maxPathD2sDt2 * ds;
            Frequency candidateNextDsDt = candidateNextD2sDt2 > Frequency2.Zero ? candidateNextD2sDt2.Sqrt() : Frequency.Zero;

            forwardPassDsDt[i + 1] = UnitsNetHelpers.Helpers.Min(candidateNextDsDt, nextPoint.ActuatorLimitedDsDt);
        }

        backwardPassDsDt[^1] = Frequency.Zero;
        for (int i = points.Count - 1; i > 0; i--)
        {
            MovePoint previousPoint = points[i - 1];
            MovePoint currentPoint = points[i];

            double ds = currentPoint.PathRatio - previousPoint.PathRatio;
            Frequency2 minPathD2sDt2 = CalculateMaxPathD2sDt2(
                currentPoint, backwardPassDsDt[i], kinematicSystem, returnMinPathAcceleration: true);
            Frequency2 minPathD2sDt2Magnitude = minPathD2sDt2.Abs();
            Frequency2 candidatePreviousD2sDt2 = backwardPassDsDt[i] * backwardPassDsDt[i] + 2.0 * minPathD2sDt2Magnitude * ds;
            Frequency candidatePreviousDsDt = candidatePreviousD2sDt2 > Frequency2.Zero ? candidatePreviousD2sDt2.Sqrt() : Frequency.Zero;

            backwardPassDsDt[i - 1] = UnitsNetHelpers.Helpers.Min(candidatePreviousDsDt, previousPoint.ActuatorLimitedDsDt);
        }

        for (int i = 0; i < points.Count - 1; i++)
            points[i].ActualDsDt = UnitsNetHelpers.Helpers.Min(forwardPassDsDt[i], backwardPassDsDt[i]);
    }

    private static Frequency2 CalculateMaxPathD2sDt2(
        MovePoint point,
        Frequency currentDsDt,
        CompositeKinematicSystem kinematicSystem,
        bool returnMinPathAcceleration)
    {
        Frequency2 minimumPathAcceleration = Frequency2.FromHertzSquared(double.MinValue);
        Frequency2 maximumPathAcceleration = Frequency2.FromHertzSquared(double.MaxValue);

        int actuatorCountLinear = point.ActuatorPositionsLinear.Count;
        int actuatorCountRotational = point.ActuatorPositionsRotational.Count;

        Debug.Assert(kinematicSystem.ActuatorsLinear.Count == actuatorCountLinear);
        Debug.Assert(kinematicSystem.ActuatorsRotational.Count == actuatorCountRotational);

        for (int iActuator = 0; iActuator < actuatorCountLinear; iActuator++)
        {
            IActuatorLinear actuator = kinematicSystem.ActuatorsLinear[iActuator];
            Length? firstDerivativeNullable = point.ActuatorPositionDerivatives1Linear[iActuator];
            Length? secondDerivativeNullable = point.ActuatorPositionDerivatives2Linear[iActuator];

            if (firstDerivativeNullable is null || secondDerivativeNullable is null)
                continue;

            Length firstDerivative = firstDerivativeNullable.Value;
            Length secondDerivative = secondDerivativeNullable.Value;

            // Path acceleration has (almost) no effect on this actuator's acceleration here so
            // it does not constrain "a" at this sample.
            if (firstDerivative.Equals(Length.Zero, Length.FromMillimeters(1e-9)))
                continue;

            Acceleration curvatureContribution = secondDerivative * currentDsDt * currentDsDt;

            Acceleration lowerBoundOnActuatorAcceleration = -actuator.MaxAcceleration - curvatureContribution;
            Acceleration upperBoundOnActuatorAcceleration = actuator.MaxAcceleration - curvatureContribution;

            Frequency2 candidateBoundA = lowerBoundOnActuatorAcceleration / firstDerivative;
            Frequency2 candidateBoundB = upperBoundOnActuatorAcceleration / firstDerivative;

            Frequency2 actuatorMinimumPathAcceleration = UnitsNetHelpers.Helpers.Min(candidateBoundA, candidateBoundB);
            Frequency2 actuatorMaximumPathAcceleration = UnitsNetHelpers.Helpers.Max(candidateBoundA, candidateBoundB);

            minimumPathAcceleration = UnitsNetHelpers.Helpers.Max(minimumPathAcceleration, actuatorMinimumPathAcceleration);
            maximumPathAcceleration = UnitsNetHelpers.Helpers.Min(maximumPathAcceleration, actuatorMaximumPathAcceleration);
        }

        for (int iActuator = 0; iActuator < actuatorCountRotational; iActuator++)
        {
            IActuatorRotational actuator = kinematicSystem.ActuatorsRotational[iActuator];
            Angle? firstDerivativeNullable = point.ActuatorPositionDerivatives1Rotational[iActuator];
            Angle? secondDerivativeNullable = point.ActuatorPositionDerivatives2Rotational[iActuator];

            if (firstDerivativeNullable is null || secondDerivativeNullable is null)
                continue;

            Angle firstDerivative = firstDerivativeNullable.Value;
            Angle secondDerivative = secondDerivativeNullable.Value;

            // Path acceleration has (almost) no effect on this actuator's acceleration here so
            // it does not constrain "a" at this sample.
            if (firstDerivative.Equals(Angle.Zero, Angle.FromRadians(1e-9)))
                continue;

            RotationalAcceleration curvatureContribution = secondDerivative * currentDsDt * currentDsDt;

            RotationalAcceleration lowerBoundOnActuatorAcceleration = -actuator.MaxAcceleration - curvatureContribution;
            RotationalAcceleration upperBoundOnActuatorAcceleration = actuator.MaxAcceleration - curvatureContribution;

            Frequency2 candidateBoundA = lowerBoundOnActuatorAcceleration / firstDerivative;
            Frequency2 candidateBoundB = upperBoundOnActuatorAcceleration / firstDerivative;

            Frequency2 actuatorMinimumPathAcceleration = UnitsNetHelpers.Helpers.Min(candidateBoundA, candidateBoundB);
            Frequency2 actuatorMaximumPathAcceleration = UnitsNetHelpers.Helpers.Max(candidateBoundA, candidateBoundB);

            minimumPathAcceleration = UnitsNetHelpers.Helpers.Max(minimumPathAcceleration, actuatorMinimumPathAcceleration);
            maximumPathAcceleration = UnitsNetHelpers.Helpers.Min(maximumPathAcceleration, actuatorMaximumPathAcceleration);
        }

        return returnMinPathAcceleration ? minimumPathAcceleration : maximumPathAcceleration;
    }

    private static void CalculateTimes(IList<MovePoint> points)
    {
        points[0].Time = Duration.Zero;

        for (int i = 0; i < points.Count - 1; i++)
        {
            MovePoint currentPoint = points[i];
            MovePoint nextPoint = points[i + 1];
            double ds = nextPoint.PathRatio - currentPoint.PathRatio;
            Frequency averageDsDtForThisInterval = (currentPoint.ActualDsDt + nextPoint.ActualDsDt) / 2.0;
            Duration timeForThisInterval = averageDsDtForThisInterval > Constants.VERY_SMALL_FREQUENCY
                ? ds / averageDsDtForThisInterval
                : Duration.Zero;
            nextPoint.Time = currentPoint.Time + timeForThisInterval;
        }
    }

    /// <summary>
    /// Simplifies a set of <see cref="MovePoint"/>s using the Douglas-Peucker algorithm
    /// and an allowable linear and rotational tolerance. Increasing the tolerance will 
    /// reduce the number of points more but will deviate from the true path more.
    /// </summary>
    /// <param name="move"></param>
    /// <param name="actuatorEpsilonLinear">Max deviation allowed for linear actuators.</param>
    /// <param name="actuatorEpsilonRotational">Max deviation allowed for rotational actuators.</param>
    public void Simplify(
        Length actuatorEpsilonLinear,
        Angle actuatorEpsilonRotational)
    {
        HashSet<int> pointIndiciesToKeep = new(Points.Count);

        // Perform simplification on entire list of points.
        SimplifyMoveSegment(pointIndiciesToKeep, this, 0, Points.Count - 1,
            actuatorEpsilonLinear, actuatorEpsilonRotational);

        // Sort the indicies and store them.
        List<int> pointIndiciesToKeepSorted = [.. pointIndiciesToKeep];
        pointIndiciesToKeepSorted.Sort();
        SimplifiedPointIndicies = pointIndiciesToKeepSorted;
    }

    /// <summary>
    /// Runs the Douglas-Peucker algorithm on a range of <see cref="MovePoint"/>s
    /// from <paramref name="startIndex"/> to <paramref name="endIndex"/> and 
    /// appends the index of the point with the largest actuator position deviation
    /// and calls itself recursively until the entire actuator trajectory can be represented
    /// with as few points as possible.
    /// It will append just the <paramref name="startIndex"/> to <paramref name="endIndex"/>
    /// if the straight line path is within the provided tolerance at every point between the 
    /// <paramref name="startIndex"/> and <paramref name="endIndex"/>.
    /// </summary>
    /// <param name="pointIndiciesToKeep">Set of <see cref="MovePoint"/> indicies to keep in order to closely approximate all actuator trajectories.</param>
    /// <param name="move">The move being simplified</param>
    /// <param name="startIndex">The index of the first <see cref="MovePoint"/> in the range of points to simplify.</param>
    /// <param name="endIndex">The index of the last <see cref="MovePoint"/> in the range of points to simplify.</param>
    /// <param name="actuatorEpsilonLinear">The max allowable deviation from the true linear actuator position at any point.</param>
    /// <param name="actuatorEpsilonRotational">The max allowable deviation from the true rotational actuator position at any point.</param>
    private static void SimplifyMoveSegment(
        HashSet<int> pointIndiciesToKeep,
        Move move,
        int startIndex,
        int endIndex,
        Length actuatorEpsilonLinear,
        Angle actuatorEpsilonRotational)
    {
        // Endpoints will always be included in simplification.
        pointIndiciesToKeep.Add(startIndex);
        pointIndiciesToKeep.Add(endIndex);

        int numberOfPoints = endIndex - startIndex + 1;

        // Not enough points to simplify further.
        if (numberOfPoints <= 2)
            return;

        MovePoint startPoint = move.Points[startIndex];
        MovePoint endPoint = move.Points[endIndex];
        Duration timeRange = endPoint.Time - startPoint.Time;
        IReadOnlyList<Length?> actuatorDistancesLinear = [.. endPoint.ActuatorPositionsLinear
            .Zip(startPoint.ActuatorPositionsLinear, (end, start) => end - start)];
        IReadOnlyList<Angle?> actuatorDistancesRotational = [.. endPoint.ActuatorPositionsRotational
            .Zip(startPoint.ActuatorPositionsRotational, (end, start) => end - start)];


        // This keeps track of how far off the current point is relative to the provided epsilons.
        double maxEpsilonRatio = 0.0;
        int indexWithMaxEpsilonRatio = 0;

        // Loop through each point and find the point where any actuator deviates the most.
        for (int i = startIndex; i <= endIndex; i++)
        {
            MovePoint currentPoint = move.Points[i];
            double timeRatio = (currentPoint.Time - startPoint.Time) / timeRange;
            for (int iActuator = 0; iActuator < move.KinematicSystem.ActuatorsLinear.Count; iActuator++)
            {
                Length? trueActuatorPosition = currentPoint.ActuatorPositionsLinear[iActuator];
                Length? approximateActuatorPosition = startPoint.ActuatorPositionsLinear[iActuator] +
                    timeRatio * actuatorDistancesLinear[iActuator];
                Length? epsilon = approximateActuatorPosition - trueActuatorPosition;

                if (!epsilon.HasValue)
                    continue;

                double epsilonRatio = epsilon.Value.FastAbs() / actuatorEpsilonLinear;
                if (epsilonRatio > maxEpsilonRatio)
                {
                    maxEpsilonRatio = epsilonRatio;
                    indexWithMaxEpsilonRatio = i;
                }
            }
        }

        // Repeat for rotational actuators.
        for (int i = startIndex; i <= endIndex; i++)
        {
            MovePoint currentPoint = move.Points[i];
            double timeRatio = (currentPoint.Time - startPoint.Time) / timeRange;
            for (int iActuator = 0; iActuator < move.KinematicSystem.ActuatorsRotational.Count; iActuator++)
            {
                Angle? trueActuatorPosition = currentPoint.ActuatorPositionsRotational[iActuator];
                Angle? approximateActuatorPosition = startPoint.ActuatorPositionsRotational[iActuator] +
                    timeRatio * actuatorDistancesRotational[iActuator];
                Angle? epsilon = approximateActuatorPosition - trueActuatorPosition;

                if (!epsilon.HasValue)
                    continue;

                double epsilonRatio = epsilon.Value.FastAbs() / actuatorEpsilonRotational;
                if (epsilonRatio > maxEpsilonRatio)
                {
                    maxEpsilonRatio = epsilonRatio;
                    indexWithMaxEpsilonRatio = i;
                }
            }
        }

        if (maxEpsilonRatio > 1.0)
        {
            // Straight line approximation is not enough so keep the point with the highest error then
            // split into two segments at that index and run again recursively.
            SimplifyMoveSegment(
                pointIndiciesToKeep,
                move,
                startIndex,
                indexWithMaxEpsilonRatio,
                actuatorEpsilonLinear,
                actuatorEpsilonRotational);
            SimplifyMoveSegment(
                pointIndiciesToKeep,
                move,
                indexWithMaxEpsilonRatio,
                endIndex,
                actuatorEpsilonLinear,
                actuatorEpsilonRotational);
        }
    }
}
