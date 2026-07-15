using EpsilonCore.Commands;
using EpsilonCore.Machines;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace EpsilonCore.Engine;

internal class MachineQueue(Machine initialMachine)
{
    public enum MachineQueueStatus
    {
        Running,
        Stopped,
    }

    public readonly ConcurrentQueue<ICommand> CommandsToEnqueue = [];
    public readonly Queue<QueuedCommand> Queued = [];
    public readonly Queue<QueuedCommand> Sent = [];

    public Machine CurrentMachine { get; set; } = initialMachine;
    public Machine LatestQueuedMachine { get; set; } = initialMachine;
    public IImmutableDictionary<uint, BoardQueue> BoardQueues { get; set; } = ImmutableDictionary<uint, BoardQueue>.Empty;
    public MachineQueueStatus EnqueueStatus { get; set; } = MachineQueueStatus.Running;
    public MachineQueueStatus SendStatus { get; set; } = MachineQueueStatus.Running;
}
