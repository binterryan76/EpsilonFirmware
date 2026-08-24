using GenericHelpers;
using UnitsNet;

namespace EpsilonCore.Boards;

/// <summary>
/// Represents a timer on a board used for scheduling actions.
/// </summary>
public record BoardTimer
{
    /// <summary>
    /// Name used in messages.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Max amount of time the timer can count to before rolling over.
    /// No action can be scheduled more than this far into the future.
    /// </summary>
    public Duration MaxDuration { get; init; }

    /// <summary>
    /// Time between each tick. 
    /// </summary>
    public Duration TickPeriod { get; init; }

    /// <summary>
    /// Returns a new instance of a <see cref="BoardTimer"/> with validation.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="maxDuration"></param>
    /// <param name="tickPeriod"></param>
    /// <returns></returns>
    public static Result<BoardTimer> New(string name, Duration maxDuration, Duration tickPeriod)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new ArgumentException("Name cannot be blank", nameof(name));

        if (maxDuration <= Duration.Zero)
            return new ArgumentException("Max durration must be positive", nameof(maxDuration));

        if (tickPeriod <= Duration.Zero)
            return new ArgumentException("Tick period must be positive", nameof(tickPeriod));

        return new BoardTimer(name, maxDuration, tickPeriod);
    }

    private BoardTimer(string name, Duration maxDuration, Duration tickPeriod)
    {
        Name = name;
        MaxDuration = maxDuration;
        TickPeriod = tickPeriod;
    }
}