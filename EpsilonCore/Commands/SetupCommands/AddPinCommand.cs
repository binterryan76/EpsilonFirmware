using EpsilonCore.Boards;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

public record AddPinCommand(Pin Pin, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Adds pin '{Pin.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => false;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        Pin pinToAdd = Pin with { Id = initialMachine.Entities.Pins.NextId() };

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                Pins = initialMachine.Entities.Pins.Add(pinToAdd.Id, pinToAdd)
            }
        };

        if (initialMachine.Entities.Pins.ContainsEntityWithName(pinToAdd.Name))
            return QueuedCommand.Warning(this,
                $"A pin with the same name '{pinToAdd.Name}' has already been added to machine '{initialMachine.Name}'",
                resultantMachine);

        return QueuedCommand.Success(this, resultantMachine);
    }
}
