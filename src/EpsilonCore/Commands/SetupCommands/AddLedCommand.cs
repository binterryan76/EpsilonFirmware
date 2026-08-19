using EpsilonCore.Boards;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

public record AddLedCommand(Led Led, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add LED '{Led.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => false;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        Led ledToAdd = Led with { Id = initialMachine.Entities.Leds.NextId() };

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                Leds = initialMachine.Entities.Leds.Add(ledToAdd.Id, ledToAdd)
            }
        };

        if (initialMachine.Entities.Leds.ContainsEntityWithName(ledToAdd.Name))
            return QueuedCommand.Warning(this,
                $"An LED with the same name'{Led.Name}' has already been added to machine '{initialMachine.Name}'",
                initialMachine,
                resultantMachine);

        return QueuedCommand.Success(this, initialMachine, resultantMachine);
    }
}
