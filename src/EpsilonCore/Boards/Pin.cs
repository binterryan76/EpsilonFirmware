namespace EpsilonCore.Boards;

public record Pin : IPin
{
    public Pin(
        byte boardsPinId,
        string name,
        uint boardId,
        bool isOutput,
        bool isDigital)
    {
        BoardsPinId = boardsPinId;
        Name = name;
        BoardId = boardId;
        IsOutput = isOutput;
        IsDigital = isDigital;
    }
    public uint BoardId { get; init; }
    public uint Id { get; init; } = 0;
    public string Name { get; init; }
    public byte BoardsPinId { get; init; }
    public bool IsOutput { get; init; }
    public bool IsDigital { get; init; }
}
