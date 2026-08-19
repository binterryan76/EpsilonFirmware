using UnitsNet;

namespace EpsilonCore.Actuator;

public interface IActuatorRotational : IActuator
{
    /// <summary>
    /// Max speed this actuator can have.
    /// </summary>
    public RotationalSpeed MaxSpeed { get; }

    /// <summary>
    /// Max speed this actuator can have during a homing move.
    /// </summary>
    public RotationalSpeed HomingSpeed { get; }

    /// <summary>
    /// Max acceleration this actuator can have.
    /// </summary>
    public RotationalAcceleration MaxAcceleration { get; }

    /// <summary>
    /// Maximum allowed speed when entering/exiting a 90 degree junction between two moves in actuator space.
    /// This is not used when planning a single move, only when two consecutive moves meet.
    /// </summary>
    public RotationalSpeed SquareCornerSpeed { get; }

    /// <summary>
    /// Because <see cref="IActuatorRotational"/> is just an interface, it might be implemented on 
    /// classes or records so the C# with statment isn't guaranteed to work.
    /// This just ensures that anything that implements <see cref="IActuatorRotational"/>
    /// behaves like a record and has a function that behaves like a with statment.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <param name="boardId"></param>
    /// <param name="maxSpeed"></param>
    /// <param name="homingSpeed"></param>
    /// <param name="maxAcceleration"></param>
    /// <returns></returns>
    public IActuatorRotational With(
        uint? id = null,
        string? name = null,
        int? boardId = null,
        RotationalSpeed? maxSpeed = null,
        RotationalSpeed? homingSpeed = null,
        RotationalAcceleration? maxAcceleration = null);
}

