using EpsilonCore.Boards;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;

namespace EpsilonCore.Commands.SetupCommands;

/// <summary>
/// Command to add a board to a machine.
/// </summary>
/// <param name="Board">New board to add.</param>
/// <param name="LineNumber"><inheritdoc cref="ICommand.LineNumber"/></param>
public record AddBoardCommand(Board Board, uint? LineNumber = null) : ICommand
{
    /// <inheritdoc />
    public string Description { get; } = $"Add board '{Board.Name}' to a machine";

    /// <inheritdoc />
    public QueuedCommand EnqueueCommandSpecific(Machine initialMachine)
    {
        Board boardToAdd = Board with { Id = initialMachine.Entities.Boards.NextId() };

        Machine resultantMachine = initialMachine with
        {
            Entities = initialMachine.Entities with
            {
                Boards = initialMachine.Entities.Boards.Add(boardToAdd.Id, boardToAdd)
            }
        };

        if (initialMachine.Entities.Boards.ContainsEntityWithName(boardToAdd.Name))
            return QueuedCommand.Warning(this,
                $"A board with the same name'{Board.Name}' has already been added to machine '{initialMachine.Name}'",
                resultantMachine);

        return QueuedCommand.Success(this, resultantMachine);
    }
}
