namespace EpsilonCore.Boards;

public record Endstop : ISwitch
{
    public Endstop(
        string name,
        IPin pin,
        bool isMinEndstop,
        bool inverted,
        bool activated)
    {
        //KinematicSystemId = kinematicSystemId;
        Pin = pin;
        Name = name;
        IsMinEndstop = isMinEndstop;
        Inverted = inverted;
        Activated = activated;
    }

    public IPin Pin { get; init; }

    public bool Inverted { get; init; }

    public bool Activated { get; init; }

    public uint Id { get; init; }

    //public int KinematicSystemId { get; }

    public string Name { get; init; }

    public bool IsMinEndstop { get; init; }

    public string MinOrMaxStr => IsMinEndstop ? "min" : "max";

}
