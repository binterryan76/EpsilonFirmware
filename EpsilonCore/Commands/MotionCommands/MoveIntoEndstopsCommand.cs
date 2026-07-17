using EpsilonCore.Boards;
using EpsilonCore.Machines;
using System.Collections.Immutable;

namespace EpsilonCore.Commands.MotionCommands;

/// <summary>
/// Command to move a machine until it hits the given endstops.
/// Note: If you want to home the machine, an additional command 
/// will need to be sent to set the machine's true position.
/// </summary>
/// <param name="EndstopIdsToHomeTo"></param>
/// <param name="LineNumber"></param>
public record MoveIntoEndstopsCommand(
    IImmutableList<uint> EndstopIdsToHomeTo,
    uint? LineNumber = null)
    : ICommand
{
    /// <inheritdoc />
    public string Description { get; } =
        $"Moves machine into endstops [{string.Join(", ", EndstopIdsToHomeTo)}]";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        throw new NotImplementedException();
        foreach (uint endstopId in EndstopIdsToHomeTo)
        {
            bool success = initialMachine.Entities.Endstops.TryGetValue(endstopId, out Endstop? endstop);

            if (!success || endstop is null)
                return QueuedCommand.ErrorMissingEndstop(this, endstopId);

        }

        // Moving into an endstop doesn't change the machine because once the machine stops moving
        // an additional command is required to set the machine's true position.
        // TODO: this may change the machine becuase motors will be enabled by the movement
        // TODO: this will eventually need to send data, probably indicating which motors to stop when a given endstop is hit.
        return QueuedCommand.Success(this, initialMachine);
    }
}
