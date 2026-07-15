using EpsilonCore.Communication;
using EpsilonCore.Machines;

namespace EpsilonCore.Boards;

public interface IPin : IEntity
{
    /// <summary>
    /// This is the ID used by the board to identify the pin.
    /// This is what is sent over the <see cref="ICommunicator"/> to specify a pin.
    /// </summary>
    public byte BoardsPinId { get; init; }

    /// <summary>
    /// ID of the board the pin belongs to.
    /// </summary>
    public uint BoardId { get; init; }

    public bool IsOutput { get; init; }
    public bool IsDigital { get; init; }

}
