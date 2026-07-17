using EpsilonCore.Actuator;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add an <see cref="IActuatorLinear"/> to a <see cref="Machine"/>.
/// </summary>
/// <param name="Actuator"></param>
/// <param name="LineNumber"></param>
public record AddActuatorLinearCommand(IActuatorLinear Actuator, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add linear actuator '{Actuator.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        IActuatorLinear actuatorToAdd = Actuator.With(id: initialMachine.Entities.ActuatorsLinear.NextId());

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                ActuatorsLinear = initialMachine.Entities.ActuatorsLinear.Add(actuatorToAdd.Id, actuatorToAdd)
            }
        };

        if (initialMachine.Entities.Boards.ContainsEntityWithName(actuatorToAdd.Name))
            return QueuedCommand.Warning(this,
                $"A linear actuator with the same name'{Actuator.Name}' has already been added to machine '{initialMachine.Name}'",
                resultantMachine);

        return QueuedCommand.Success(this, resultantMachine);
    }
}
