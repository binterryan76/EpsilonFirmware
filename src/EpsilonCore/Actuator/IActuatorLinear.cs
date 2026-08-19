using UnitsNet;

namespace EpsilonCore.Actuator;

public interface IActuatorLinear : IActuator
{
    /// <summary>
    /// Max speed this actuator can have.
    /// </summary>
    public Speed MaxSpeed { get; }

    /// <summary>
    /// Max speed this actuator can have during a homing move.
    /// </summary>
    public Speed HomingSpeed { get; }

    /// <summary>
    /// Max acceleration this actuator can have.
    /// </summary>
    public Acceleration MaxAcceleration { get; }

    /// <summary>
    /// Maximum allowed speed when entering/exiting a 90 degree junction between two moves in actuator space.
    /// This is not used when planning a single move, only when two consecutive moves meet.
    /// </summary>
    public Speed SquareCornerSpeed { get; }

    /// <summary>
    /// Because <see cref="IActuatorLinear"/> is just an interface, it might be implemented on 
    /// classes or records so the C# with statment isn't guaranteed to work.
    /// This just ensures that anything that implements <see cref="IActuatorLinear"/>
    /// behaves like a record and has a function that behaves like a with statment.
    /// TODO: replace this with an abstract base record that other records inherit from.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="boardId"></param>
    /// <param name="maxSpeed"></param>
    /// <param name="homingSpeed"></param>
    /// <param name="maxAcceleration"></param>
    /// <returns></returns>
    public IActuatorLinear With(
        uint? id = null,
        string? name = null,
        int? boardId = null,
        Speed? maxSpeed = null,
        Speed? homingSpeed = null,
        Acceleration? maxAcceleration = null);
}
