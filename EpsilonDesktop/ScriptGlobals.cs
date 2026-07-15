using EpsilonCore.Engine;
using EpsilonDesktop.Models;

namespace EpsilonDesktop;

public class ScriptGlobals
{
    public EpsilonEngine Engine { get; } = new();
    public TextboxResultLogger TextboxResultLogger { get; set; } = new();
}
