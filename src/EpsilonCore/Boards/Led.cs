using EpsilonCore.Machines;

namespace EpsilonCore.Boards;

public record Led : IEntity
{
    public Led(string name, uint pinId)
    {
        Name = name;
        PinId = pinId;
    }

    public uint Id { get; init; } = 0;
    public string Name { get; init; }
    public uint PinId { get; init; }
}
