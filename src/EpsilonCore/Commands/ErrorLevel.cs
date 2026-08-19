namespace EpsilonCore.Commands;

/// <summary>
/// Indicates if the queueing or sending a command was successful, had an error, etc.
/// </summary>
public enum ErrorLevel
{
    /// <summary>
    /// Result was successful.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Result wouldn't have done anything making it unnecessary.
    /// </summary>
    Unnecessary,

    /// <summary>
    /// Result has some kind of notification that would probably be nice to know.
    /// </summary>
    Warning,

    /// <summary>
    /// Result was a failure.
    /// </summary>
    Error,

    /// <summary>
    /// Result was a failure and, for safety, needs to restart.
    /// </summary>
    EmergencyShutdown
}
