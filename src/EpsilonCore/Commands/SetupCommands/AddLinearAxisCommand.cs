using EpsilonCore.Helpers; using GenericHelpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Axis;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add an <see cref="AxisLinear"/> to a <see cref="Machine"/>.
/// </summary>
/// <param name="Axis"></param>
/// <param name="LineNumber"></param>
public record AddLinearAxisCommand(AxisLinear Axis, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add axis '{Axis.Name}' to a machine";

    /// <inheritdoc />
    public bool RequiresZeroVelocity => true;

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        AxisLinear axisToAdd = Axis with
        {
            Id = initialMachine.Entities.AxesLinear.NextId()
        };

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                AxesLinear = initialMachine.Entities.AxesLinear.Add(axisToAdd.Id, axisToAdd)
            }
        };

        if (initialMachine.ContainsAxisName(axisToAdd.Name))
            return QueuedCommand.Warning(this,
                $"Machine '{initialMachine.Name}' already has an axis with the name '{axisToAdd.Name}'",
                initialMachine,
                resultantMachine);

        return QueuedCommand.Success(this, initialMachine, resultantMachine);
    }
}
