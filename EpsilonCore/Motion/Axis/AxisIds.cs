using System.Collections.Immutable;

namespace EpsilonCore.Motion.Axis;

/// <summary>
/// Represents a list of <see cref="AxisLinear.Id"/>s and <see cref="AxisRotational.Id"/>s.
/// It is used for mapping which <see cref="Actuator.IActuator"/>s affect which <see cref="IAxis"/>.
/// </summary>
/// <param name="AxisIdsLinear"></param>
/// <param name="AxisIdsRotational"></param>
public record AxisIds(IImmutableList<uint> AxisIdsLinear, IImmutableList<uint> AxisIdsRotational);
