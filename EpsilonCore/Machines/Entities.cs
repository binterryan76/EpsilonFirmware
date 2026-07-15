using EpsilonCore.Actuator;
using EpsilonCore.Boards;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Thermal;
using System.Collections.Immutable;

namespace EpsilonCore.Machines;

#pragma warning disable CS1591 

/// <summary>
/// Contains the master lists of all Epsilon Firmware entities.
/// For each of the dictionaries, the key is the <see cref="IEntity.Id"/>.
/// </summary>
public record Entities
{
    /// <summary>
    /// Dictionary of microcontroller boards associated with this machine.
    /// Data is sent between the C# machine and the C++ microcontroller(s).
    /// </summary>
    public IImmutableDictionary<uint, Board> Boards { get; init; }
        = ImmutableDictionary<uint, Board>.Empty;

    public IImmutableDictionary<uint, Pin> Pins { get; init; }
        = ImmutableDictionary<uint, Pin>.Empty;

    public IImmutableDictionary<uint, TempController> TempControllers { get; init; }
        = ImmutableDictionary<uint, TempController>.Empty;

    public IImmutableDictionary<uint, ISwitch> Switches { get; init; }
        = ImmutableDictionary<uint, ISwitch>.Empty;

    public IImmutableDictionary<uint, Led> Leds { get; init; }
        = ImmutableDictionary<uint, Led>.Empty;

    public IImmutableDictionary<uint, Endstop> Endstops { get; init; }
        = ImmutableDictionary<uint, Endstop>.Empty;

    public IImmutableDictionary<uint, IActuatorLinear> ActuatorsLinear { get; init; }
        = ImmutableDictionary<uint, IActuatorLinear>.Empty;

    public IImmutableDictionary<uint, IActuatorRotational> ActuatorsRotational { get; init; }
        = ImmutableDictionary<uint, IActuatorRotational>.Empty;

    public IImmutableDictionary<uint, AxisLinear> AxesLinear { get; init; }
        = ImmutableDictionary<uint, AxisLinear>.Empty;

    public IImmutableDictionary<uint, AxisRotational> AxesRotational { get; init; }
        = ImmutableDictionary<uint, AxisRotational>.Empty;

    public IImmutableDictionary<uint, Heater> Heaters { get; init; }
        = ImmutableDictionary<uint, Heater>.Empty;

    public IImmutableDictionary<uint, Thermometer> Thermometers { get; init; }
        = ImmutableDictionary<uint, Thermometer>.Empty;
}
