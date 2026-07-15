using EpsilonCore.Actuator;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Axis;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Motion.Kinematics;

public record CompositeKinematicSystem
{
    public uint DegreesOfFreedom { get; init; }
    public IImmutableList<IKinematics> Kinematics { get; init; }
    public IImmutableList<AxisLinear> AxesLinear { get; init; }
    public IImmutableList<AxisRotational> AxesRotational { get; init; }
    public IImmutableList<IActuatorLinear> ActuatorsLinear { get; init; }
    public IImmutableList<IActuatorRotational> ActuatorsRotational { get; init; }

    /// <summary>
    /// Sets of min and max endstops for all kinematics.
    /// Endstop switches either correspond to actuators or axes.
    /// The number of <see cref="EndstopSet"/>s must match the <see cref="DegreesOfFreedom"/>.
    /// For example, if the first <see cref="IKinematics"/> has 2 degrees of freedom and accepts homing axes then,
    /// the second <see cref="EndstopSet"/> will correspond to axis 2 of the first <see cref="IKinematics"/>.
    /// </summary>
    public IImmutableList<EndstopSet> EndstopSets { get; init; }

    // these are all just internal buffers initialized once for performance
    // reasons and so we can make Memory objects pointing to subsections of the arrays.
    private Length?[] ActuatorPositionsLinear { get; set; }
    private Angle?[] ActuatorPositionsRotational { get; set; }
    private Length?[] AxisPositionsLinear { get; set; }
    private Angle?[] AxisPositionsRotational { get; set; }
    private List<Memory<Length?>> ActuatorMemoriesLinear { get; set; }
    private List<Memory<Angle?>> ActuatorMemoriesRotational { get; set; }
    private List<Memory<Length?>> AxisMemoriesLinear { get; set; }
    private List<Memory<Angle?>> AxisMemoriesRotational { get; set; }

    internal CompositeKinematicSystem()
    {
        DegreesOfFreedom = 0;
        Kinematics = [];
        AxesLinear = [];
        AxesRotational = [];
        ActuatorsLinear = [];
        ActuatorsRotational = [];
        ActuatorPositionsLinear = [];
        ActuatorPositionsRotational = [];
        AxisPositionsLinear = [];
        AxisPositionsRotational = [];
        ActuatorMemoriesLinear = [];
        ActuatorMemoriesRotational = [];
        AxisMemoriesLinear = [];
        AxisMemoriesRotational = [];
        EndstopSets = [];
    }

    private CompositeKinematicSystem(
        uint degreesOfFreedom,
        IImmutableList<IKinematics> kinematics,
        IImmutableList<AxisLinear> axesLinear,
        IImmutableList<AxisRotational> axesRotational,
        IImmutableList<IActuatorLinear> actuatorsLinear,
        IImmutableList<IActuatorRotational> actuatorsRotational,
        IImmutableList<EndstopSet> endstopSets,
        Length?[] actuatorPositionsLinear,
        Angle?[] actuatorPositionsRotational,
        Length?[] axisPositionsLinear,
        Angle?[] axisPositionsRotational,
        List<Memory<Length?>> actuatorMemoriesLinear,
        List<Memory<Angle?>> actuatorMemoriesRotational,
        List<Memory<Length?>> axisMemoriesLinear,
        List<Memory<Angle?>> axisMemoriesRotational)
    {
        DegreesOfFreedom = degreesOfFreedom;
        Kinematics = kinematics;
        AxesLinear = axesLinear;
        AxesRotational = axesRotational;
        ActuatorsLinear = actuatorsLinear;
        ActuatorsRotational = actuatorsRotational;
        EndstopSets = endstopSets;
        ActuatorPositionsLinear = actuatorPositionsLinear;
        ActuatorPositionsRotational = actuatorPositionsRotational;
        AxisPositionsLinear = axisPositionsLinear;
        AxisPositionsRotational = axisPositionsRotational;
        ActuatorMemoriesLinear = actuatorMemoriesLinear;
        ActuatorMemoriesRotational = actuatorMemoriesRotational;
        AxisMemoriesLinear = axisMemoriesLinear;
        AxisMemoriesRotational = axisMemoriesRotational;
    }

    /// <summary>
    /// Used to convert actuator positions to axis positions.
    /// </summary>
    /// <param name="actuatorPositionsLinear"></param>
    /// <param name="actuatorPositionsRotational"></param>
    /// <returns></returns>
    public Result<(IImmutableList<Length?>, IImmutableList<Angle?>)> ForwardKinematics(
        IReadOnlyList<Length?> actuatorPositionsLinear,
        IReadOnlyList<Angle?> actuatorPositionsRotational)
    {
        if (actuatorPositionsLinear.Count != ActuatorPositionsLinear.Length)
            return new ArgumentException($"{ActuatorPositionsLinear.Length} linear actuators are required but {actuatorPositionsLinear.Count} were provided.");

        if (actuatorPositionsRotational.Count != ActuatorPositionsRotational.Length)
            return new ArgumentException($"{ActuatorPositionsRotational.Length} rotational actuators are required but {actuatorPositionsRotational.Count} were provided.");

        // enter input data into private input arrays since the memory objects are already pointing to these lists
        for (int i = 0; i < actuatorPositionsLinear.Count; i++)
            ActuatorPositionsLinear[i] = actuatorPositionsLinear[i];

        for (int i = 0; i < actuatorPositionsRotational.Count; i++)
            ActuatorPositionsRotational[i] = actuatorPositionsRotational[i];

        // run inverse kinematics on everything, any kinematics with a null input will be skipped and will output null
        for (int i = 0; i < Kinematics.Count; i++)
        {
            Exception? error = Kinematics[i].ForwardKinematics(
                ActuatorMemoriesLinear[i].Span,
                ActuatorMemoriesRotational[i].Span,
                AxisMemoriesLinear[i],
                AxisMemoriesRotational[i]);

            if (error is not null)
                return error;
        }

        return (AxisPositionsLinear.ToImmutableList(), AxisPositionsRotational.ToImmutableList());
    }

    /// <summary>
    /// Used to convert axis positions to actuator positions.
    /// </summary>
    /// <param name="axisPositionsLinear"></param>
    /// <param name="axisPositionsRotational"></param>
    /// <returns></returns>
    public Result<(IImmutableList<Length?>, IImmutableList<Angle?>)> InverseKinematics(
        IReadOnlyList<Length?> axisPositionsLinear,
        IReadOnlyList<Angle?> axisPositionsRotational)
    {
        if (axisPositionsLinear.Count != AxisPositionsLinear.Length)
            return new ArgumentException($"{AxisPositionsLinear.Length} linear axes are required but {axisPositionsLinear.Count} were provided.");

        if (axisPositionsRotational.Count != AxisPositionsRotational.Length)
            return new ArgumentException($"{AxisPositionsRotational.Length} rotational axes are required but {axisPositionsRotational.Count} were provided.");

        // enter input data into private input arrays since the memory objects are already pointing to these lists
        for (int i = 0; i < axisPositionsLinear.Count; i++)
            AxisPositionsLinear[i] = axisPositionsLinear[i];

        for (int i = 0; i < axisPositionsRotational.Count; i++)
            AxisPositionsRotational[i] = axisPositionsRotational[i];

        // run inverse kinematics on everything, any kinematics with a null input will be skipped and will output null
        for (int i = 0; i < Kinematics.Count; i++)
        {
            Exception? error = Kinematics[i].InverseKinematics(
                AxisMemoriesLinear[i].Span,
                AxisMemoriesRotational[i].Span,
                ActuatorMemoriesLinear[i],
                ActuatorMemoriesRotational[i]);

            if (error is not null)
                return error;
        }

        return (ActuatorPositionsLinear.ToImmutableList(), ActuatorPositionsRotational.ToImmutableList());
    }

    /// <summary>
    /// Returns a new CompositeKinematicSystem with an additional <see cref="IKinematics"/>.
    /// It returns a <see cref="Result{T}"/> because this can fail if the number of actuators/axes 
    /// doesn't match the degrees of freedom, it the machine hasn't been assigned these actuators/axes, 
    /// or if the machine already uses these actuators/axes in another <see cref="IKinematics"/>.
    /// </summary>
    /// <param name="initialMachine"></param>
    /// <param name="kinematicsToAdd"></param>
    /// <param name="actuatorIdsToAddLinear"></param>
    /// <param name="actuatorIdsToAddRotational"></param>
    /// <param name="axisIdsToAddLinear"></param>
    /// <param name="axisIdsToAddRotational"></param>
    /// <param name="endstopSetsToAdd"></param>
    /// <returns></returns>
    internal Result<CompositeKinematicSystem> AddKinematics(
        Machine initialMachine,
        IKinematics kinematicsToAdd,
        IReadOnlyList<uint> actuatorIdsToAddLinear,
        IReadOnlyList<uint> actuatorIdsToAddRotational,
        IReadOnlyList<uint> axisIdsToAddLinear,
        IReadOnlyList<uint> axisIdsToAddRotational,
        IReadOnlyList<EndstopSet> endstopSetsToAdd)
    {
        // First validate inputs.
        // Make sure the number of actuators and axes matches the degrees of freedom.
        int actuatorsToAddCount = actuatorIdsToAddLinear.Count + actuatorIdsToAddRotational.Count;
        int axesToAddCount = axisIdsToAddLinear.Count + axisIdsToAddRotational.Count;

        if (kinematicsToAdd.DegreesOfFreedom != actuatorsToAddCount)
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd}' because it's degrees of freedom ({kinematicsToAdd.DegreesOfFreedom}) must match the number of actuators ({actuatorsToAddCount}).");

        if (kinematicsToAdd.DegreesOfFreedom != axesToAddCount)
            return new ArgumentException(
               $"Cannot add kinematics '{kinematicsToAdd}' because it's degrees of freedom ({kinematicsToAdd.DegreesOfFreedom}) must match the number of axes ({axesToAddCount}).");

        // Ensure axes and actuators have been added to the machine.
        IEnumerable<uint> missingActuatorIdsLinear = actuatorIdsToAddLinear.Except(initialMachine.Entities.ActuatorsLinear.Keys);
        IEnumerable<uint> missingActuatorIdsRotational = actuatorIdsToAddRotational.Except(initialMachine.Entities.ActuatorsRotational.Keys);
        IEnumerable<uint> missingAxisIdsLinear = axisIdsToAddLinear.Except(initialMachine.Entities.AxesLinear.Keys);
        IEnumerable<uint> missingAxisIdsRotational = axisIdsToAddRotational.Except(initialMachine.Entities.AxesRotational.Keys);

        if (missingActuatorIdsLinear.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because linear axis(s) " +
                $"({string.Join(", ", missingActuatorIdsLinear)}) must first be assigned to machine '{initialMachine.Name}'.");

        if (missingActuatorIdsRotational.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because rotaional axis(s) " +
                $"({string.Join(", ", missingActuatorIdsRotational)}) must first be assigned to machine '{initialMachine.Name}'.");

        if (missingAxisIdsLinear.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because linear axis(s) " +
                $"({string.Join(", ", missingAxisIdsLinear)}) must first be assigned to machine '{initialMachine.Name}'.");

        if (missingAxisIdsRotational.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because rotaional axis(s) " +
                $"({string.Join(", ", missingAxisIdsRotational)}) must first be assigned to machine '{initialMachine.Name}'.");

        // Ensure endstops have been added to the machine.
        IEnumerable<uint> allEndstopIdsToAdd = endstopSetsToAdd
            .SelectMany(set => new[] { set.MinEndstopId, set.MaxEndstopId })
            .WhereNotNull();

        IEnumerable<uint> missingEndstopIds = allEndstopIdsToAdd.Except(initialMachine.Entities.Endstops.Keys);

        if (missingEndstopIds.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because endstop(s) " +
                $"({string.Join(", ", missingEndstopIds)}) must first be assigned to machine '{initialMachine.Name}'.");

        // Make sure no other kinematic systems use the same axes or actuators.
        IEnumerable<uint> actuatorIdsInUseLinear = ActuatorsLinear.Select(actuator => actuator.Id);
        IEnumerable<uint> actuatorIdsInUseRotational = ActuatorsRotational.Select(actuator => actuator.Id);
        IEnumerable<uint> axisIdsInUseLinear = AxesLinear.Select(axis => axis.Id);
        IEnumerable<uint> axisIdsInUseRotational = AxesRotational.Select(axis => axis.Id);

        IEnumerable<uint> actuatorIdsUsedTwiceLinear = actuatorIdsToAddLinear.Intersect(actuatorIdsInUseLinear);
        IEnumerable<uint> actuatorIdsUsedTwiceRotational = actuatorIdsToAddRotational.Intersect(actuatorIdsInUseRotational);
        IEnumerable<uint> axisIdsUsedTwiceLinear = axisIdsToAddLinear.Intersect(axisIdsInUseLinear);
        IEnumerable<uint> axisIdsUsedTwiceRotational = axisIdsToAddRotational.Intersect(axisIdsInUseRotational);

        if (actuatorIdsUsedTwiceLinear.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because linear axis(s) " +
                $"({string.Join(", ", actuatorIdsUsedTwiceLinear)}) are already in use by another kinematics.");

        if (actuatorIdsUsedTwiceRotational.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because rotational axis(s) " +
                $"({string.Join(", ", actuatorIdsUsedTwiceRotational)}) are already in use by another kinematics.");

        if (axisIdsUsedTwiceLinear.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because linear axis(s) " +
                $"({string.Join(", ", axisIdsUsedTwiceLinear)}) are already in use by another kinematics.");

        if (axisIdsUsedTwiceRotational.Any())
            return new ArgumentException(
                $"Cannot add kinematics '{kinematicsToAdd.Name}' because rotational axis(s) " +
                $"({string.Join(", ", axisIdsUsedTwiceRotational)}) are already in use by another kinematics.");

        // Validation complete, now work on returning modified copy with additional actuators, axes, and kinematics.
        // First create new lists of all the actuators and axes.
        IEnumerable<IActuatorLinear> actuatorsToAddLinear = initialMachine.Entities.ActuatorsLinear.Values
            .Where(actuator => actuatorIdsToAddLinear.Contains(actuator.Id));

        IEnumerable<IActuatorRotational> actuatorsToAddRotational = initialMachine.Entities.ActuatorsRotational.Values
            .Where(actuator => actuatorIdsToAddRotational.Contains(actuator.Id));

        IEnumerable<AxisLinear> axesToAddLinear = initialMachine.Entities.AxesLinear.Values
            .Where(axis => axisIdsToAddLinear.Contains(axis.Id));

        IEnumerable<AxisRotational> axesToAddRotational = initialMachine.Entities.AxesRotational.Values
            .Where(axis => axisIdsToAddRotational.Contains(axis.Id));

        IImmutableList<IKinematics> kinematics = Kinematics.Add(kinematicsToAdd);
        IImmutableList<IActuatorLinear> allActuatorsLinear = ActuatorsLinear.AddRange(actuatorsToAddLinear);
        IImmutableList<IActuatorRotational> allActuatorsRotational = ActuatorsRotational.AddRange(actuatorsToAddRotational);
        IImmutableList<AxisLinear> allAxesLinear = AxesLinear.AddRange(axesToAddLinear);
        IImmutableList<AxisRotational> allAxesRotational = AxesRotational.AddRange(axesToAddRotational);
        IImmutableList<EndstopSet> allEndstopSets = EndstopSets.AddRange(endstopSetsToAdd);

        // Next, initialize arrays to use for calculating forward and inverse kinematics.
        uint degreesOfFreedom = (uint)kinematics.Sum(k => k.DegreesOfFreedom);
        uint axisLinearCount = (uint)(AxesLinear.Count + axisIdsToAddLinear.Count);
        uint axisRotationalCount = (uint)(AxesRotational.Count + axisIdsToAddRotational.Count);
        uint actuatorLinearCount = (uint)(ActuatorsLinear.Count + actuatorIdsToAddLinear.Count);
        uint actuatorRotationalCount = (uint)(ActuatorsRotational.Count + actuatorIdsToAddRotational.Count);

        Length?[] actuatorPositionsLinear = new Length?[actuatorLinearCount];
        Angle?[] actuatorPositionsRotational = new Angle?[actuatorRotationalCount];
        Length?[] axisPositionsLinear = new Length?[axisLinearCount];
        Angle?[] axisPositionsRotational = new Angle?[axisRotationalCount];
        List<Memory<Length?>> actuatorMemoriesLinear = [];
        List<Memory<Angle?>> actuatorMemoriesRotational = [];
        List<Memory<Length?>> axisMemoriesLinear = [];
        List<Memory<Angle?>> axisMemoriesRotational = [];

        int actuatorMemoryIndexLinear = 0;
        int actuatorMemoryIndexRotational = 0;
        int axisMemoryIndexLinear = 0;
        int axisMemoryIndexRotational = 0;

        // Layout the memory for each kinematics contained in the CompositeKinematicSystem.
        foreach (IKinematics kin in kinematics)
        {
            actuatorMemoriesLinear.Add(new(
                actuatorPositionsLinear,
                actuatorMemoryIndexLinear,
                (int)kin.ActuatorLinearCount));

            actuatorMemoryIndexLinear += (int)kin.ActuatorLinearCount;

            actuatorMemoriesRotational.Add(new(
                actuatorPositionsRotational,
                actuatorMemoryIndexRotational,
                (int)kin.ActuatorRotationalCount));

            actuatorMemoryIndexLinear += (int)kin.ActuatorRotationalCount;

            axisMemoriesLinear.Add(new(
                axisPositionsLinear,
                axisMemoryIndexLinear,
                (int)kin.AxisLinearCount));

            axisMemoryIndexLinear += (int)kin.AxisLinearCount;

            axisMemoriesRotational.Add(new(
                axisPositionsRotational,
                axisMemoryIndexRotational,
                (int)kin.AxisRotationalCount));

            axisMemoryIndexRotational += (int)kin.AxisRotationalCount;
        }

        return new CompositeKinematicSystem(
            degreesOfFreedom,
            kinematics,
            allAxesLinear,
            allAxesRotational,
            allActuatorsLinear,
            allActuatorsRotational,
            allEndstopSets,
            actuatorPositionsLinear,
            actuatorPositionsRotational,
            axisPositionsLinear,
            axisPositionsRotational,
            actuatorMemoriesLinear,
            actuatorMemoriesRotational,
            axisMemoriesLinear,
            axisMemoriesRotational);
    }
}
