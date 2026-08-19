using UnitsNet;

namespace EpsilonCore.Actuator;

public record StepperMotorLinear : IActuatorLinear
{
    public StepperMotorLinear(
        uint id,
        string name,
        int boardID,
        int fullStepsPerRotation,
        int microstepsPerFullStep,
        Speed maxSpeed,
        Speed squareCornerSpeed,
        Speed homingSpeed,
        Acceleration maxAcceleration)
    {
        Id = id;
        Name = name;
        BoardId = boardID;
        FullStepsPerRotation = fullStepsPerRotation;
        MicrostepsPerFullStep = microstepsPerFullStep;
        MaxSpeed = maxSpeed;
        SquareCornerSpeed = squareCornerSpeed;
        HomingSpeed = homingSpeed;
        MaxAcceleration = maxAcceleration;
    }

    public uint Id { get; init; }
    public string Name { get; init; }
    public int BoardId { get; init; }
    public int FullStepsPerRotation { get; init; }
    public int MicrostepsPerFullStep { get; init; }
    public Speed MaxSpeed { get; init; }
    public Speed SquareCornerSpeed { get; init; }
    public Speed HomingSpeed { get; init; }
    public Acceleration MaxAcceleration { get; init; }

    /// <inheritdoc cref="IActuatorLinear.With(uint?, string?, int?, Speed?, Speed?, Acceleration?)"/>
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
        Acceleration? maxAcceleration = null)
    {
        return this with
        {
            Id = id is null ? Id : id.Value,
            Name = name is null ? Name : name,
            BoardId = boardId is null ? BoardId : boardId.Value,
            MaxSpeed = maxSpeed is null ? MaxSpeed : maxSpeed.Value,
            HomingSpeed = homingSpeed is null ? HomingSpeed : homingSpeed.Value,
            MaxAcceleration = maxAcceleration is null ? MaxAcceleration : maxAcceleration.Value,
        };
    }
}
