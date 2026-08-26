using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using System.Collections.Concurrent;

namespace EpsilonCore.Engine;

/// <summary>
/// MachineQueue manages the queue of <see cref="ICommand"/>s for a specific machine.
/// It also keeps track of the latest status update of the machine for things like temperature and position updates.
/// <see cref="ICommand"/>s are first created, then send to <see cref="CommandsToEnqueue"/> where they wait 
/// to be queued to a machine and moved to <see cref="Queued"/>, then marked ready to send and moved 
/// to <see cref="ReadyToSend"/>, then sent and moved to <see cref="Sent"/>.
/// If a <see cref="Board"/> sends a message saying that a command failed or an error occurred, every other
/// <see cref="Board"/> will be notified to shutdown, clear their movment queue, set pins to safe states, etc.
/// </summary>
/// <param name="initialMachine"></param>
internal class MachineQueue(Machine initialMachine)
{
    public enum MachineQueueStatus
    {
        Running,
        Stopped,
    }

    /// <summary>
    /// Returns true if this MachineQueue has no data to process.
    /// </summary>
    public bool IsEmpty
    {
        get =>
            CommandsToEnqueue.IsEmpty &&
            Queued.Count <= 0 &&
            ReadyToSend.Count <= 0 &&
            DataPacketsReadyToSend.Count <= 0 &&
            Sent.Count <= 0 &&
            MoveQueue.Count <= 0;
    }

    /// <summary>
    /// Contains commands that were sent from an external source and haven't been looked at yet.
    /// </summary>
    public ConcurrentQueue<ICommand> CommandsToEnqueue { get; } = [];

    /// <summary>
    /// Contains commands that have been queued to a machine but haven't had their step times solved yet so cannot be sent.
    /// Commands will wait here until either enough commands pile up to solve their step times, or until a command requires 
    /// the machine to come to a complete stop, or if the end there are no more commands to queue.
    /// </summary>
    public Queue<QueuedCommand> Queued { get; set; } = [];

    /// <summary>
    /// Contains commands that have been queued to a machine and have had their step times solved so they are ready to be sent.
    /// Commands will wait here until the microcontroller's command buffer has enough space to receive the command.
    /// </summary>
    public Queue<QueuedCommand> ReadyToSend { get; } = [];

    /// <summary>
    /// Contains the <see cref="DataPacket"/>s that still need to be sent ffor the next command in <see cref="ReadyToSend"/>.
    /// When <see cref="ReadyToSend"/> gets a new command, this queue will be filled with the <see cref="DataPacket"/>s 
    /// for that command.
    /// As each <see cref="DataPacket"/> is sent, it will be removed from this queue until all the <see cref="DataPacket"/>s 
    /// for that command have been sent.
    /// Once this is empty, the next command in <see cref="ReadyToSend"/> will be moved to <see cref="Sent"/> and the next 
    /// command's <see cref="DataPacket"/>s will be added to this queue.
    /// </summary>
    public Queue<DataPacket> DataPacketsReadyToSend { get; } = [];

    /// <summary>
    /// Adds a command to the <see cref="ReadyToSend"/> queue and fills the <see cref="DataPacketsReadyToSend"/> 
    /// queue with the data packets from the next command to send if there are any.
    /// </summary>
    /// <param name="queuedCommand"></param>
    public void AddCommandToReadyToSend(QueuedCommand queuedCommand)
    {
        ReadyToSend.Enqueue(queuedCommand);

        // Any time a command is added to the ReadyToSend queue, we need to check if there are any data packets to send for that command.
        // If there are, we need to fill the DataPacketsReadyToSend queue with those data packets.
        MaybeQueueDataPacketsToSend();
    }

    /// <summary>
    /// Fills the <see cref="DataPacketsReadyToSend"/> queue with the data packets from the next command to send if there are any.
    /// </summary>
    public void MaybeQueueDataPacketsToSend()
    {
        if (ReadyToSend.Count <= 0)
            return;

        QueuedCommand nextCommandToSend = ReadyToSend.Peek();

        if (nextCommandToSend.DataPackets.Count > 0 && DataPacketsReadyToSend.Count <= 0)
            foreach (DataPacket dataPacket in nextCommandToSend.DataPackets)
                DataPacketsReadyToSend.Enqueue(dataPacket);
    }

    /// <summary>
    /// Contains commands that have been sent to a machine but haven't received a response yet.
    /// Commands will wait here until the microcontroller sends a response indicating the command was successful or failed.
    /// </summary>
    public Queue<QueuedCommand> Sent { get; } = [];

    /// <summary>
    /// Used to solve step times.
    /// </summary>
    public MoveQueue MoveQueue { get; } = new();

    /// <summary>
    /// Machine that resulted from the last command which has received a successful response from the microcontroller.
    /// </summary>
    public Machine CurrentMachine { get; set; } = initialMachine;

    /// <summary>
    /// Machine that resulted from the last command which was queued.
    /// This is used as the input machine for the next command to be queued.
    /// </summary>
    public Machine LatestQueuedMachine { get; set; } = initialMachine;

    /// <summary>
    /// Contains the <see cref="ICommunicator"/>s for each <see cref="Machine"/> that is being managed by this <see cref="MachineQueue"/>.
    /// </summary>
    public ConcurrentDictionary<uint, ICommunicator> Communicators { get; set; } = [];
    public MachineQueueStatus EnqueueStatus { get; set; } = MachineQueueStatus.Running;
    public MachineQueueStatus SendStatus { get; set; } = MachineQueueStatus.Running;
}
