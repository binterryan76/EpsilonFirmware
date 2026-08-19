using EpsilonCore.Machines;

namespace EpsilonCore.Motion.Axis;

public interface IAxis : IEntity
{
    public int KinematicSystemId { get; }
    public bool IsExtrusionAxis { get; }
    public bool IsLinearAxis { get; }
}
