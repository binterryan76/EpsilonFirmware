using EpsilonCore.Boards;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add a new <see cref="Boards.Endstop"/> to a <see cref="Machine"/>.
/// </summary>
/// <param name="Endstop"></param>
/// <param name="LineNumber"></param>
public record AddEndstopCommand(
    Endstop Endstop,
    uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add endstop '{Endstop.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => false;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        Endstop endstopToAdd = Endstop with
        {
            Id = initialMachine.Entities.Endstops.NextId()
        };


        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                Endstops = initialMachine.Entities.Endstops.Add(endstopToAdd.Id, endstopToAdd),
            }
        };

        if (initialMachine.Entities.Endstops.ContainsEntityWithName(endstopToAdd.Name))
            return QueuedCommand.Warning(this,
                $"An endstop with the same name '{endstopToAdd.Name}' has already been added to machine '{initialMachine.Name}'",
                initialMachine,
                resultantMachine);

        return QueuedCommand.Success(this, initialMachine, resultantMachine);
    }
}
