using EpsilonCore.Boards;
using UnitsNet;

namespace EpsilonCore.Thermal;

public record Thermometer(string Name, IPin Pin, Temperature Temp);
