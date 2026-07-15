using EpsilonCore.Machines;

namespace EpsilonCore.Boards;

public interface ISwitch : IEntity
{
    public IPin Pin { get; }
    public bool Inverted { get; }
    public bool Activated { get; }
}
