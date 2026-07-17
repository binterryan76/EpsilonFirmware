using EpsilonCore.Helpers;
using EpsilonCore.Motion.Kinematics;
using System.Text;
using UnitsNet;
using UnitsNetHelpers;



namespace EpsilonCore.Motion;

public class MoveQueue
{
    private readonly Queue<Move> queue = [];
    public int Count => queue.Count;
    public void Clear() => queue.Clear();
    public Move? Last => queue.Count > 0 ? queue.Last() : null;
    public void Add(Move move) => queue.Enqueue(move);
    public bool TryDequeue(out Move? move) => queue.TryDequeue(out move);
    public Move Dequeue() => queue.Dequeue();

    /// <summary>
    /// Solves the time of every <see cref="MovePoint"/>.
    /// 
    /// When this function is called, all the intermediate sample points of every move should already have 
    /// their inverse kinematics solved. 
    /// 
    /// **** Rough Algorithm Outline ****
    /// Every point of every move will have a path ratio (called s) which will be 0-1 (inclusive) 
    /// representing how far along the move that point lies. This parameterizes the position function
    /// and lets us take derivatives with respect to s. We cannot take derivatives with respect to time 
    /// because we haven't calculated that yet.
    /// 
    /// Instead of a regular velocity trapezoid we will make a ds/dt trapezoid over time. This means that percentage of the move will 
    /// change slowly at first, ramp up linearly over time until reaching a cap out in the middle, then ramp down at the end.
    /// We will then calculate the time of each point.
    /// 
    /// This algorithm is nice because if new moves are queued and we need to recalculate new junction speeds, all the 
    /// derivatives with respect to s will remain the same so we just need to recompute new ds/dt trapezoids and new times.
    /// 
    /// **** Detailed Algorithm Outline ****
    /// 1. Calculate derivatives.
    /// At each point, each actuator will have a position (Ɵ) (we will use Ɵ for both linear and rotational actuators)
    /// and path ratio (s) forming a coordinate pair.
    /// Remember that ActuatorSpeed = dƟ/dt = dƟ/ds * ds/dt.
    /// Remember that ActuatorAcceleration = d2Ɵ/dt2 = d2Ɵ/ds2 * (ds/dt)^2 + dƟ/ds * d2s/dt2 (second derivative chain rule). 
    /// 
    /// We will approximate dƟ/ds at every point based on all these coordinate pairs using the central finite difference method
    /// for middle points, forward difference for the first point, and backward difference for the last point.
    /// The central difference formula is this:
    /// dƟ/ds ≈ (Ɵnext - Ɵprevious) / (snext - sprevious).
    /// 
    /// We will approximate d2Ɵ/ds2 at every point based on all the computed dƟ/ds values using the same finite difference method.
    /// The central difference formula for second derivatives is this:
    /// d2Ɵ/ds2 ≈ (Ɵnext - 2*Ɵcurrent + Ɵprevious) / (snext - scurrent)^2.
    /// 
    /// 2. Calculate actuator max speed contraints.
    /// We then need to calculate a ds/dt trapezoid which whill obey the actuator's 
    /// max speed (dƟ/dt) and the actuator's max acceleration (d2Ɵ/dt2).
    /// 
    /// Now we can solve for ds/dt by rearanging this formula: dƟ/dt = dƟ/ds * ds/dt and setting dƟ/dt = MaxActuatorSpeed.
    /// We already calculated dƟ/ds.
    /// ds/dt = MaxActuatorSpeed / dƟ/ds.
    /// If we take the lowest ds/dt, this will correspond to the max speed we can travel at without exceeding any actuator's max speed.
    /// This is what <see cref="MovePoint.ActuatorLimitedDsDt"/> is.
    /// The ds/dt trapezoid cannot exceed this value, otherwise some actuator's max speed will be exceeded.
    /// Should I use a 5-10% safety margin on this term?
    /// 
    /// 3. Calculate junction velocity constraints.
    /// 
    /// 4. Calculate actuator max acceleration contraints.
    /// Now we need to make sure ds/dt doesnt change too fast (slope too steep) and exceed an actuators max acceleration/deceleration.
    /// We also need to make sure that the ds/dt value is low enough that the motors can accelerate to that point from the start velocity
    /// and also low enough to decelerate to the final velocity. 
    /// We will ensure this with a forward and backward pass and take the min of both passes.
    /// Because we parameterized position over time with parameter s, Ɵ(t) becomes Ɵ(s(t)) so we have to use the second derivative chain rule:
    /// d2Ɵ/dt2 = d2Ɵ/ds2 * (ds/dt)^2 + dƟ/ds * d2s/dt2
    /// We rearange to solve for d2s/dt2:
    /// d2s/dt2 = [d2Ɵ/dt2 - (d2Ɵ/ds2 * (ds/dt)^2) ] / dƟ/ds.
    /// d2Ɵ/dt2 = MaxActuatorAcceleration
    /// d2Ɵ/ds2 = Second derivative we calculated earier
    /// ds/dt = Current velocity at this point
    /// Should I use a 5-10% safety margin on this term?
    /// 
    /// 5. Calculate <see cref="MovePoint.Time"/>
    /// We will then calculate the time of each point by dividing the change in s between two points (ds) by ds/dt to get the change in time (dt).
    /// 
    /// I may want to solve junction ds/dt values between moves here?
    /// 
    /// 6. Simplify results.
    /// We can then run the douglas-peuker algorithm on those time-step values to see if we can reduce the number of data points in the table.
    /// This will minimize the data that needs to be send to the microcontroller over USB.
    /// </summary>
    public void SolveAllMoves(MotionSystemPrecisions precisions)
    {
        if (Count <= 0) return;

        // Steps 1 and 2 above.
        foreach (Move move in queue)
            move.CalculateActuatorDerivativesAndVelocityLimits();

        // Step 3 above.
        MovePoint? previousMoveEndPoint = null;
        CompositeKinematicSystem? previousMoveKinematicSystem = null;
        foreach (Move move in queue)
        {
            // First loop will just set previousMoveEndPoint and previousMoveKinematicSystem and continue.
            if (previousMoveEndPoint is null || previousMoveKinematicSystem is null)
            {
                previousMoveEndPoint = move.Points[^1];
                previousMoveKinematicSystem = move.KinematicSystem;
                continue;
            }

            MovePoint currentMoveStartPoint = move.Points[0];
            CompositeKinematicSystem currentMoveKinematicSystem = move.KinematicSystem;
            (Frequency previousMoveEndDsDtLimit, Frequency currentMoveStartDsDtLimit) =
                CalculateJunctionDsDtLimits(
                    previousMoveEndPoint,
                    currentMoveStartPoint,
                    previousMoveKinematicSystem,
                    currentMoveKinematicSystem);

            // Further limit the ActuatorLimitedDsDt based on the allowed junction speeds.
            previousMoveEndPoint.ActuatorLimitedDsDt =
                UnitsNetHelpers.Helpers.Min(previousMoveEndPoint.ActuatorLimitedDsDt, previousMoveEndDsDtLimit);

            currentMoveStartPoint.ActuatorLimitedDsDt =
                UnitsNetHelpers.Helpers.Min(currentMoveStartPoint.ActuatorLimitedDsDt, currentMoveStartDsDtLimit);

            previousMoveEndPoint = move.Points[^1];
            previousMoveKinematicSystem = move.KinematicSystem;
        }

        // Steps 4, 5, and 6 above.
        foreach (Move move in queue)
        {
            move.CalculateDsDtValuesAndPointTimes();
            move.Simplify(precisions.PrecisionLinear, precisions.PrecisionRotational);
        }
    }

    /// <summary>
    /// Returns the two ds/dt values which obey every actuator's square corner speed limits 
    /// for the corresponding <paramref name="currentMoveStartPoint"/> and <paramref name="previousMoveEndPoint"/>.
    /// These values are allowed to be different even though <paramref name="currentMoveStartPoint"/> and 
    /// <paramref name="previousMoveEndPoint"/> are coincident (no distance between them) because each move
    /// scales their s parameter differently based on their own travel distance.
    /// </summary>
    /// <param name="previousMoveEndPoint"></param>
    /// <param name="currentMoveStartPoint"></param>
    /// <param name="previousMoveKinematicSystem"></param>
    /// <param name="currentMoveKinematicSystem"></param>
    /// <returns></returns>
    private static (Frequency previousMoveEndDsDtLimit, Frequency currentMoveStartDsDtLimit) CalculateJunctionDsDtLimits(
        MovePoint previousMoveEndPoint,
        MovePoint currentMoveStartPoint,
        CompositeKinematicSystem previousMoveKinematicSystem,
        CompositeKinematicSystem currentMoveKinematicSystem)
    {
        double cosineOfTurnAngle = ComputeCosineOfTurnAngle(
            previousMoveEndPoint,
            currentMoveStartPoint,
            previousMoveKinematicSystem,
            currentMoveKinematicSystem);
        double cornerSpeedFactor = ComputeCornerSpeedFactor(cosineOfTurnAngle);

        Frequency previousMoveEndDsDtLimit = Constants.LARGEST_FREQUENCY;
        Frequency currentMoveStartDsDtLimit = Constants.LARGEST_FREQUENCY;

        for (int i = 0; i < previousMoveKinematicSystem.ActuatorsLinear.Count; i++)
        {
            Speed actuatorJunctionVelocity =
                previousMoveKinematicSystem.ActuatorsLinear[i].SquareCornerSpeed * cornerSpeedFactor;

            Length derivativeMagnitude = previousMoveEndPoint.ActuatorPositionDerivatives1Linear[i].FastAbs();

            // Dividing this actuator's own junction velocity by
            // this same actuator's own dTheta/ds (units of Length/Angle)
            // gives ds/dt (units of frequency) which can be compared between
            // linear and rotational actuators.
            if (derivativeMagnitude > Constants.VERY_SMALL_LENGTH)
            {
                Frequency candidatePathDsDtLimit = actuatorJunctionVelocity / derivativeMagnitude;
                previousMoveEndDsDtLimit =
                    UnitsNetHelpers.Helpers.Min(previousMoveEndDsDtLimit, candidatePathDsDtLimit);
            }
        }

        for (int i = 0; i < previousMoveKinematicSystem.ActuatorsRotational.Count; i++)
        {
            RotationalSpeed actuatorJunctionVelocity =
                previousMoveKinematicSystem.ActuatorsRotational[i].SquareCornerSpeed * cornerSpeedFactor;

            Angle derivativeMagnitude = previousMoveEndPoint.ActuatorPositionDerivatives1Rotational[i].FastAbs();

            if (derivativeMagnitude > Constants.VERY_SMALL_ANGLE)
            {
                Frequency candidatePathDsDtLimit = actuatorJunctionVelocity / derivativeMagnitude;
                previousMoveEndDsDtLimit =
                    UnitsNetHelpers.Helpers.Min(previousMoveEndDsDtLimit, candidatePathDsDtLimit);
            }
        }

        for (int i = 0; i < currentMoveKinematicSystem.ActuatorsLinear.Count; i++)
        {
            Speed actuatorJunctionVelocity =
                currentMoveKinematicSystem.ActuatorsLinear[i].SquareCornerSpeed * cornerSpeedFactor;

            Length derivativeMagnitude = currentMoveStartPoint.ActuatorPositionDerivatives1Linear[i].FastAbs();

            if (derivativeMagnitude > Constants.VERY_SMALL_LENGTH)
            {
                Frequency candidatePathDsDtLimit = actuatorJunctionVelocity / derivativeMagnitude;
                currentMoveStartDsDtLimit =
                    UnitsNetHelpers.Helpers.Min(currentMoveStartDsDtLimit, candidatePathDsDtLimit);
            }
        }

        for (int i = 0; i < currentMoveKinematicSystem.ActuatorsRotational.Count; i++)
        {
            RotationalSpeed actuatorJunctionVelocity =
                currentMoveKinematicSystem.ActuatorsRotational[i].SquareCornerSpeed * cornerSpeedFactor;

            Angle derivativeMagnitude = currentMoveStartPoint.ActuatorPositionDerivatives1Rotational[i].FastAbs();

            if (derivativeMagnitude > Constants.VERY_SMALL_ANGLE)
            {
                Frequency candidatePathDsDtLimit = actuatorJunctionVelocity / derivativeMagnitude;
                currentMoveStartDsDtLimit =
                    UnitsNetHelpers.Helpers.Min(currentMoveStartDsDtLimit, candidatePathDsDtLimit);
            }
        }

        return (previousMoveEndDsDtLimit, currentMoveStartDsDtLimit);
    }

    /// <summary>
    /// Returns the cosine of the angle between two consecutive move's speed vectors in actuator space.
    /// Because linear actuators and rotational actuators have different units, the distance components
    /// are normalized by dividing the distance by the actuator's square corner speed.
    /// </summary>
    /// <param name="previousMoveEndPoint"></param>
    /// <param name="currentMoveStartPoint"></param>
    /// <param name="previousMoveKinematicSystem"></param>
    /// <param name="currentMoveKinematicSystem"></param>
    /// <returns></returns>
    private static double ComputeCosineOfTurnAngle(
        MovePoint previousMoveEndPoint,
        MovePoint currentMoveStartPoint,
        CompositeKinematicSystem previousMoveKinematicSystem,
        CompositeKinematicSystem currentMoveKinematicSystem)
    {
        // normalize the dƟ/ds derivatives by dividing by square corner speed so that linear and rotational
        // axes can be compared in the same units.
        List<Duration?> normalizedDerivativesPrevious =
            previousMoveEndPoint.NormalizedActuatorDerivatives(previousMoveKinematicSystem);
        List<Duration?> normalizedDerivativesCurrent =
            currentMoveStartPoint.NormalizedActuatorDerivatives(currentMoveKinematicSystem);

        // Get the magnitude of the normalized derivatives so we can calculate the angle between them.
        Duration previousEndDerivativeMagnitue =
            UnitsNetHelpers.Helpers.Distance(normalizedDerivativesPrevious);
        Duration currentStartDerivativeMagnitue =
            UnitsNetHelpers.Helpers.Distance(normalizedDerivativesCurrent);

        // Note: The angle between two vectors (Ɵ) can be calculated with this formula:
        // Cos(Ɵ) = Dot(VectorA, VectorB) / (|VectorA| * |VectorB|)
        // The dot product is a scalar equal to the sum of products of the corresponding components of two vectors.

        if (previousEndDerivativeMagnitue < Constants.VERY_SMALL_DURATION ||
            currentStartDerivativeMagnitue < Constants.VERY_SMALL_DURATION)
        {
            // One side has a degenerate tangent (an actuator momentarily stationary
            // relative to s right at the junction). Treat this as a full reversal
            // (must reach zero velocity) so we stay on the safe side rather than
            // dividing by a near-zero magnitude.
            return -1.0;
        }

        double dotProductSecondsSquared = normalizedDerivativesPrevious
            .Zip(normalizedDerivativesCurrent,
                (a, b) => a.HasValue && b.HasValue ? a.Value.Seconds * b.Value.Seconds : 0.0)
            .Sum();
        double cosineOfTurnAngle = dotProductSecondsSquared /
            (previousEndDerivativeMagnitue.Seconds * currentStartDerivativeMagnitue.Seconds);

        return Math.Clamp(cosineOfTurnAngle, -1.0, 1.0);
    }

    /// <summary>
    /// Returns a factor which can be multiplied by an actuator's square corner velocity then multiplied by the actuator's
    /// derivative with respect to s (dƟ/ds) to get a ds/dt limit which will obey the junction speed limits.
    /// If two consecutive moves have a 90 degree angle in actuator space then the square corner velocity can be used,
    /// if they have a 180 degree angle (full turn around) then all actuators must come to a full stop (factor of zero), 
    /// and if they have a small angle, then actuators won't need to slow down at all (factor of positive infinity).
    /// This function 
    /// </summary>
    /// <param name="cosineOfTurnAngle">Cosine of the angle between two consecutive move's speed vectors in actuator space.</param>
    /// <returns></returns>
    private static double ComputeCornerSpeedFactor(double cosineOfTurnAngle)
    {
        // cos(turnAngle/2) is calculated using the half-angle identity, which lets us avoid
        // calculating an inverse cosine which is a little more costly.
        // half-angle identity: cos(x/2) = sqrt((1 + cos(x)) / 2) for x in [0, pi].
        double cosineOfHalfTurnAngle = Math.Sqrt((1.0 + cosineOfTurnAngle) / 2.0);
        double denominator = 1.0 - cosineOfHalfTurnAngle;

        // Turn angle is essentially zero (straight through): no junction limit applies.
        // Returning a very large factor ensures that the junction speed limits won't be a
        // limiting factor because this value goes through a min function.
        if (denominator < Constants.VERY_SMALL_NUMBER)
            return double.PositiveInfinity;

        double squareRootTerm = Constants.ROOT_2_MINUS_1 * cosineOfHalfTurnAngle / denominator;
        return Math.Sqrt(squareRootTerm);
    }

    /// <summary>
    /// Writes the move queue to a csv file. 
    /// Has columns for time, axis positions, actuator positions, actuator velocities, and actuator accelerations.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="kinematicSystem"></param>
    /// <param name="writeSimplifiedPoints"></param>
    public void TryWriteToCsv(
        string filePath,
        CompositeKinematicSystem kinematicSystem,
        bool writeSimplifiedPoints)
    {
        StringBuilder output = new();

        // Make column headers.
        IEnumerable<string> headers = new List<string>(["Time (s)"])
            .Concat(kinematicSystem.AxesLinear.Select(axis => axis.Name + " Pos (mm)"))
            .Concat(kinematicSystem.AxesRotational.Select(axis => axis.Name + " Pos (deg)"))
            .Concat(kinematicSystem.ActuatorsLinear.Select(actuator => actuator.Name + " Pos (mm)"))
            .Concat(kinematicSystem.ActuatorsRotational.Select(actuator => actuator.Name + " Pos (deg)"))
            .Concat(kinematicSystem.ActuatorsLinear.Select(actuator => actuator.Name + " Speed (mm/s)"))
            .Concat(kinematicSystem.ActuatorsRotational.Select(actuator => actuator.Name + " Speed (deg/s)"));

        output.AppendLine(string.Join(",", headers));

        Duration timeOfStartOfMove = Duration.Zero;

        foreach (Move move in queue)
        {
            IEnumerable<MovePoint> pointsToWrite = writeSimplifiedPoints ?
                move.SimplifiedPointIndicies.Select(i => move.Points[i]) :
                move.Points;

            foreach (MovePoint point in pointsToWrite)
            {
                IEnumerable<string> values = new List<string>([(timeOfStartOfMove + point.Time).Seconds.ToString("F6")])
                    .Concat(point.AxisPositionsLinear.Select(pos => pos.HasValue ? pos.Value.Millimeters.ToString("F2") : string.Empty))
                    .Concat(point.AxisPositionsRotational.Select(pos => pos.HasValue ? pos.Value.Degrees.ToString("F2") : string.Empty))
                    .Concat(point.ActuatorPositionsLinear.Select(pos => pos.HasValue ? pos.Value.Millimeters.ToString("F2") : string.Empty))
                    .Concat(point.ActuatorPositionsRotational.Select(pos => pos.HasValue ? pos.Value.Degrees.ToString("F2") : string.Empty))
                    .Concat(point.ActuatorPositionDerivatives1Linear.Select(derivative =>
                        derivative.HasValue ? (derivative.Value * point.ActualDsDt).MillimetersPerSecond.ToString("F2") : string.Empty))
                    .Concat(point.ActuatorPositionDerivatives1Rotational.Select(derivative =>
                        derivative.HasValue ? (derivative.Value * point.ActualDsDt).DegreesPerSecond.ToString("F2") : string.Empty));
                output.AppendLine(string.Join(",", values));
            }
            timeOfStartOfMove += move.Points[^1].Time;
        }

        try
        {
            File.WriteAllText(filePath, output.ToString());
        }
        catch
        {
            // intentionally blank
        }
    }
}