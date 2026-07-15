using System.Diagnostics;

namespace EpsilonCore.Display;

/// <summary>
/// Used to log messages that result from queueing and sending commands using <see cref="Debug.WriteLine(string)"/>.
/// </summary>
public class DebugConsoleLogger : IResultMessageLogger
{
    /// <summary>
    /// Logs a message using <see cref="Debug.WriteLine(string)"/>.
    /// </summary>
    /// <param name="message"></param>
    public void Log(string message)
    {
        Debug.WriteLine(message);
    }
}
