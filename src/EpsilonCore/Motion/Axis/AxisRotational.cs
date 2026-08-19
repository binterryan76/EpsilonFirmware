using System.Diagnostics.CodeAnalysis;
using UnitsNet;

namespace EpsilonCore.Motion.Axis;

public record AxisRotational(
    string Name,
    int KinematicSystemId,
    bool IsExtrusionAxis,
    Angle PosMin,
    Angle PosMax,
    Angle? PosAtMinEndstop = null,
    Angle? PosAtMaxEndstop = null,
    Angle? Pos = null)
    : IAxis
{
    /// <inheritdoc cref="Machines.IEntity.Id"/>
    public uint Id { get; internal init; } = 0;

    [MemberNotNullWhen(true, nameof(Pos))]
    public bool IsHomed => Pos is not null;

    public bool IsLinearAxis => false;

    /// <summary>
    /// Returns true if a position is unreachable by this axis.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public bool PosOutOfRange(Angle pos)
        => pos < PosMin || pos > PosMax;
}