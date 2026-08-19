using System.Diagnostics.CodeAnalysis;
using UnitsNet;

namespace EpsilonCore.Motion.Axis;

public record AxisLinear(
    string Name,
    int KinematicSystemId,
    bool IsExtrusionAxis,
    Length PosMin,
    Length PosMax,
    Length? PosAtMinEndstop = null,
    Length? PosAtMaxEndstop = null,
    Length? Pos = null)
    : IAxis
{
    /// <inheritdoc cref="Machines.IEntity.Id"/>
    public uint Id { get; internal init; } = 0;
    public bool IsLinearAxis => true;

    [MemberNotNullWhen(true, nameof(Pos))]
    public bool IsHomed => Pos is not null;

    /// <summary>
    /// Returns true if a position is unreachable by this axis.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public bool PosOutOfRange(Length pos)
        => pos < PosMin || pos > PosMax;
}