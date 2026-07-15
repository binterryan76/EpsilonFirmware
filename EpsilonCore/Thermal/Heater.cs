using EpsilonCore.Boards;

namespace EpsilonCore.Thermal;

/// <summary>
/// Represents a heater attached to a digital output pin.
/// </summary>
/// <param name="Name"></param>
/// <param name="Pin">Pin to activate to turn on heater.</param>
/// <param name="Activated">True if the heater is on.</param>
public record Heater(string Name, IPin Pin, bool Activated);
