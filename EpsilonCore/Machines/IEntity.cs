namespace EpsilonCore.Machines;

/// <summary>
/// Used for items that are looked up by ID.
/// If an object is contained by another object and not looked up 
/// by i, then it shouldn't be an <see cref="IEntity"/>.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique id for each type of entity.
    /// Starts at 0 and counts up.
    /// This will be 0 by default but will be 
    /// assigned when the entity is added to it's collection.
    /// </summary>
    uint Id { get; }

    /// <summary>
    /// Name used in messages about the entity.
    /// </summary>
    string Name { get; }
}
