using EpsilonCore.Machines;
using EpsilonCore.Units;
using UnitsNet;

namespace EpsilonCore.Thermal;

public record TempController : IEntity
{
    public TempController(
        string name,
        uint boardId,
        uint heaterId,
        uint thermometerId,
        Temperature maxTargetTemp)
    {
        Name = name;
        BoardId = boardId;
        HeaterId = heaterId;
        ThermometerId = thermometerId;
        MaxTargetTemp = maxTargetTemp;
        TargetTemp = DefaultUnits.RoomTemp;
    }

    public uint Id { get; } = 0;
    public string Name { get; init; }
    public uint BoardId { get; init; }
    public uint HeaterId { get; init; }
    public uint ThermometerId { get; init; }
    public Temperature MaxTargetTemp { get; init; }
    public Temperature TargetTemp { get; init; }
}

