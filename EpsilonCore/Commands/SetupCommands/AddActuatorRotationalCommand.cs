using EpsilonCore.Actuator;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add an <see cref="IActuatorRotational"/> to a <see cref="Machine"/>.
/// </summary>
/// <param name="Actuator"></param>
/// <param name="LineNumber"></param>
public record AddActuatorRotationalCommand(IActuatorRotational Actuator, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add rotational actuator '{Actuator.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        IActuatorRotational actuatorToAdd = Actuator.With(id: initialMachine.Entities.ActuatorsRotational.NextId());

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                ActuatorsRotational = initialMachine.Entities.ActuatorsRotational.Add(actuatorToAdd.Id, actuatorToAdd)
            }
        };

        if (initialMachine.Entities.Boards.ContainsEntityWithName(actuatorToAdd.Name))
            return QueuedCommand.Warning(this,
                $"A rotational actuator with the same name'{Actuator.Name}' has already been added to machine '{initialMachine.Name}'",
                resultantMachine);

        return QueuedCommand.Success(this, resultantMachine);
    }
}

