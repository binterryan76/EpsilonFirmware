using EpsilonCore.Machines;

namespace EpsilonCore.Communication;

/// <summary>
/// Used to communicate with a microcontroller board.
/// </summary>
public interface ICommunicator : IEntity
{
    /// <summary>
    /// Connects to a a microcontroller board.
    /// </summary>
    /// <returns>True if successful, false if unsuccessful.</returns>
    public bool Connect();

    /// <summary>
    /// Disconnects from a a microcontroller board.
    /// </summary>
    public void Disconnect();

    /// <summary>
    /// Sends a data packet to a microcontroller board.
    /// </summary>
    /// <param name="packet"></param>
    /// <returns>True if successful, false if unsuccessful.</returns>
    public bool Send(DataPacket packet);

    /// <summary>
    /// Reads a <see cref="DataPacket"/> from a microcontroller board if possible.
    /// </summary>
    /// <returns></returns>
    public DataPacket? Read();

    /// <summary>
    /// Returns the next usable sequence number for a command being sent with this <see cref="ICommunicator"/>.
    /// </summary>
    /// <returns></returns>
    public byte IncrementAndReturnNextSequenceNumber();


}







