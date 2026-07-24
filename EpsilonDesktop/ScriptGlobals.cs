using EpsilonCore.Engine;
using EpsilonDesktop.Models;

namespace EpsilonDesktop;

public class ScriptGlobals
{
    public ScriptGlobals(Func<string, bool> logFunction)
    {
        TextboxResultLogger logger = new()
        {
            LogFunction = logFunction
        };
        Engine = new(logger);
        TextboxResultLogger = logger;
    }

    public EpsilonEngine Engine { get; }
    public TextboxResultLogger TextboxResultLogger { get; }
}
