using EpsilonCore.Commands;
using EpsilonCore.Display;
using EpsilonCore.Helpers; using GenericHelpers;

namespace EpsilonDesktop.Models;

public class TextboxResultLogger() : IResultMessageLogger
{
    public Func<string, bool>? LogFunction { get; set; }

    public void Log(string message)
    {
        if (LogFunction is not null)
            LogFunction(message);
    }

    public void Log(QueuedCommand queuedCommand)
    {
        if (LogFunction is not null)
            LogFunction(Helper.GetFormattedDisplayMessage(queuedCommand));
    }
}
