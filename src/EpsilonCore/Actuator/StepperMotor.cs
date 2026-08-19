using UnitsNet;

namespace EpsilonCore.Actuator;

public record StepperMotor : IActuatorRotational
{
    public StepperMotor(
        string name,
        int boardId,
        int fullStepsPerRotation,
        int microstepsPerFullStep,
        RotationalSpeed maxSpeed,
        RotationalSpeed squareCornerSpeed,
        RotationalSpeed homingSpeed,
        RotationalAcceleration maxAcceleration)
    {
        BoardId = boardId;
        Name = name;
        FullStepsPerRotation = fullStepsPerRotation;
        MicrostepsPerFullStep = microstepsPerFullStep;
        MaxSpeed = maxSpeed;
        SquareCornerSpeed = squareCornerSpeed;
        HomingSpeed = homingSpeed;
        MaxAcceleration = maxAcceleration;
    }

    public uint Id { get; init; } = 0;
    public string Name { get; init; }
    public int BoardId { get; init; }
    public int FullStepsPerRotation { get; init; }
    public int MicrostepsPerFullStep { get; init; }
    public RotationalSpeed MaxSpeed { get; init; }
    public RotationalSpeed SquareCornerSpeed { get; init; }
    public RotationalSpeed HomingSpeed { get; init; }
    public RotationalAcceleration MaxAcceleration { get; init; }

    /// <inheritdoc cref="IActuatorRotational.With(uint?, string?, int?, RotationalSpeed?, RotationalSpeed?, RotationalAcceleration?)"/>
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
        RotationalAcceleration? maxAcceleration = null)
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

