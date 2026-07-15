using EpsilonCore.Communication;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands;

/// <summary>
/// Interface used by all commands.
/// Users creating custom commands need to use extreme caution and 
/// have good knowledge of the internal workings of the firmware.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Summary of what the command does.
    /// Exclude trailing periods from this string.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Optional line number for commands that come from a program.
    /// </summary>
    public uint? LineNumber { get; }

    /// <summary>
    /// This function should contain Enqueue logic specific to a particular command.
    /// This will perform any checks specific to a particular command and ensure 
    /// the command is ready to be applied to the <paramref name="initialMachine"/>.
    /// </summary>
    /// <param name="initialMachine"></param>
    /// <returns>A <see cref="QueuedCommand"/> containing the 
    /// <see cref="QueuedCommand.ResultantMachine"/> which will result when the command is applied and 
    /// <see cref="DataPacket"/> which contains the information to 
    /// send the command to the microcontroller. The <see cref="DataPacket"/> will exclude the 
    /// <see cref="DataPacket.SequenceNumber"/> because that will be assigned right before it is sent to the microcontroller.
    /// </returns>
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine);
}