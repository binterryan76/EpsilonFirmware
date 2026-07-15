using EpsilonCore.Machines;

namespace EpsilonCore.Actuator;

public interface IActuator : IEntity
{
    public int BoardId { get; }
}