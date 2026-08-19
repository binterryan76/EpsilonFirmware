namespace EpsilonCore.Display;

/// <summary>
/// Used to log messages that result from queueing and sending commands.
/// </summary>
public interface IResultMessageLogger
{
    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message"></param>
    public void Log(string message);
}
