namespace EpsilonCore.Commands;

/// <summary>
/// A queued command that runs in order on the board and takes a known amount of time, such as a move,
/// a dwell or setting a target temperature.
/// Because the duration is known, the host can predict the <see cref="QueuedCommand.ResultantMachine"/>
/// when the command is queued and can schedule the commands after it at exact board clock times.
/// The duration is given by <see cref="QueuedCommand.ExpectedDuration"/>.
///
/// Commands that implement only <see cref="ICommand"/> (such as setup commands) are treated as scheduled commands
/// with no duration.
/// </summary>
public interface IScheduledCommand : ICommand
{
}
