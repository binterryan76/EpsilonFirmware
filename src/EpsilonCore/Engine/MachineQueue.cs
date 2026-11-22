using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Communication;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using System.Collections.Concurrent;

namespace EpsilonCore.Engine;

/// <summary>
/// State of a <see cref="MachineQueue"/>.
/// </summary>
public enum MachineQueueStatus
{
    /// <summary>
    /// Commands are being planned and sent.
    /// </summary>
    Running,

    /// <summary>
    /// Stopped by an error. Send a <see cref="Commands.ImmediateCommands.ClearFaultsCommand"/> to restart.
    /// </summary>
    Stopped,

    /// <summary>
    /// A pause was sent and the host is waiting for every board to report it has paused.
    /// </summary>
    Pausing,

    /// <summary>
    /// Paused. Send a <see cref="Commands.ImmediateCommands.ResumeCommand"/> to continue.
    /// </summary>
    Paused,

    /// <summary>
    /// Emergency stopped. Send a <see cref="Commands.ImmediateCommands.ClearFaultsCommand"/> to restart.
    /// </summary>
    EmergencyStopped,
}

/// <summary>
/// MachineQueue manages the queue of <see cref="ICommand"/>s for a specific machine.
/// It also keeps track of the latest status update of the machine for things like temperature and position updates.
/// <see cref="ICommand"/>s are first created, then sent to <see cref="CommandsToEnqueue"/> where they wait
/// to be queued to a machine and moved to <see cref="Queued"/>, then marked ready to send and moved
/// to <see cref="ReadyToSend"/>, then sent and moved to <see cref="InFlight"/> until every board involved reports
/// the command complete, then resolved.
/// <see cref="IImmediateCommand"/>s skip all of these queues.
/// If a <see cref="Board"/> reports that a command failed, the other boards are paused and the queue stops.
///
/// Everything in this class except <see cref="CommandsToEnqueue"/> must only be accessed while holding <see cref="SyncRoot"/>
/// because the main loop, the communicators' receive threads and callers sending immediate commands all use it.
/// </summary>
/// <param name="initialMachine">The machine before any command runs.</param>
internal class MachineQueue(Machine initialMachine)
{
    /// <summary>
    /// Raised when a <see cref="QueuedCommand"/> is queued and the <see cref="QueuedCommand.ResultantMachine"/>
    /// is applied to the <see cref="MachineQueue.LatestQueuedMachine"/>.
    /// The sender object will be the <see cref="QueuedCommand.InitialMachine"/> that the command was queued to.
    /// </summary>
    public EventHandler<QueuedCommand>? CommandQueued;

    /// <summary>
    /// Raised when a <see cref="QueuedCommand"/> is sent to one or more <see cref="Board"/>s.
    /// The sender object will be the <see cref="QueuedCommand.InitialMachine"/> that the command was queued to.
    /// </summary>
    public EventHandler<QueuedCommand>? CommandSent;

    /// <summary>
    /// Raised when a <see cref="QueuedCommand"/> is fully resolved and the <see cref="QueuedCommand.ResultantMachine"/>
    /// is applied to the <see cref="MachineQueue.CurrentMachine"/>.
    /// The sender object will be the <see cref="QueuedCommand.ResultantMachine"/> that resulted from the command.
    /// </summary>
    public EventHandler<QueuedCommand>? CommandResolved;

    private readonly List<Action> pendingEvents = [];

    /// <summary>
    /// Id of this machine queue in the <see cref="EpsilonEngine"/>.
    /// </summary>
    public uint Id { get; init; }

    /// <summary>
    /// Lock that protects this queue's state.
    /// </summary>
    public Lock SyncRoot { get; } = new();

    /// <summary>
    /// Returns true if this MachineQueue has no data to process.
    /// <see cref="MoveQueue"/> isn't checked because it keeps the last solved move to remember velocities.
    /// </summary>
    public bool IsEmpty
    {
        get =>
            CommandsToEnqueue.IsEmpty &&
            Queued.Count <= 0 &&
            ReadyToSend.Count <= 0 &&
            DataPacketsReadyToSend.Count <= 0 &&
            InFlight.Count <= 0;
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
    /// The front command stays here until all of its data packets have been sent.
    /// </summary>
    public Queue<QueuedCommand> ReadyToSend { get; } = [];

    /// <summary>
    /// The <see cref="DataPacket"/>s (with their timing headers) of the front command of <see cref="ReadyToSend"/>
    /// that still need to be sent. Filled when that command starts being sent.
    /// </summary>
    public Queue<DataPacket> DataPacketsReadyToSend { get; } = [];

    /// <summary>
    /// The front command of <see cref="ReadyToSend"/> once its first data packet is about to be sent, or null.
    /// </summary>
    public InFlightCommand? CommandBeingSent { get; set; }

    /// <summary>
    /// Commands that have started being sent and haven't resolved, in the order they were sent.
    /// </summary>
    public List<InFlightCommand> InFlight { get; } = [];

    /// <summary>
    /// An <see cref="IConditionalCommand"/> with an unpredictable result that has been queued but not resolved.
    /// No commands after it are planned until it resolves, because the machine they would start from is unknown.
    /// </summary>
    public QueuedCommand? PendingBarrier { get; set; }

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

    /// <summary>
    /// Host bookkeeping for each board, keyed by communicator id.
    /// </summary>
    public Dictionary<uint, BoardState> BoardStates { get; } = [];

    /// <summary>
    /// Adjustments such as babysteps that the boards apply on top of planned commands.
    /// </summary>
    public LiveAdjustments LiveAdjustments { get; } = new();

    /// <summary>
    /// Whether commands are being planned (moved out of <see cref="CommandsToEnqueue"/>).
    /// </summary>
    public MachineQueueStatus EnqueueStatus { get; set; } = MachineQueueStatus.Running;

    /// <summary>
    /// Whether commands are being sent to the boards.
    /// </summary>
    public MachineQueueStatus SendStatus { get; set; } = MachineQueueStatus.Running;

    /// <summary>
    /// Communicator ids that haven't reported <see cref="BoardRunState.Paused"/> since the last pause was sent.
    /// </summary>
    public HashSet<uint> BoardsYetToPause { get; } = [];

    /// <summary>
    /// Host time the last pause was sent.
    /// </summary>
    public double PauseRequestedAt { get; set; }

    /// <summary>
    /// The front <see cref="InFlight"/> command the watchdog is timing and when it became the front.
    /// </summary>
    public (InFlightCommand? Command, double Since) WatchdogFront { get; set; }

    /// <summary>
    /// Queues an event to be raised once <see cref="SyncRoot"/> is released, so event handlers can't deadlock
    /// with other threads waiting for the lock.
    /// </summary>
    /// <param name="raiseEvent">Raises the event.</param>
    public void DeferEvent(Action raiseEvent) => pendingEvents.Add(raiseEvent);

    /// <summary>
    /// Returns and clears the events queued with <see cref="DeferEvent"/>.
    /// </summary>
    /// <returns>The queued events, in the order they were queued.</returns>
    public List<Action> TakePendingEvents()
    {
        List<Action> events = [.. pendingEvents];
        pendingEvents.Clear();
        return events;
    }

    /// <summary>
    /// Adds a command to the <see cref="ReadyToSend"/> queue.
    /// </summary>
    /// <param name="queuedCommand">The planned command.</param>
    public void AddCommandToReadyToSend(QueuedCommand queuedCommand) => ReadyToSend.Enqueue(queuedCommand);

    /// <summary>
    /// Discards every command that hasn't resolved and forgets what the boards had queued.
    /// Used when the boards discard their command buffers (emergency stop, fault).
    /// </summary>
    /// <param name="now">Host time.</param>
    public void ClearAllUnresolvedCommands(double now)
    {
        CommandsToEnqueue.Clear();
        Queued.Clear();
        ReadyToSend.Clear();
        DataPacketsReadyToSend.Clear();
        MoveQueue.Clear();
        InFlight.Clear();
        CommandBeingSent = null;
        PendingBarrier = null;
        BoardsYetToPause.Clear();
        WatchdogFront = (null, 0);
        foreach (BoardState boardState in BoardStates.Values)
            boardState.ResetQueue(now);
        LatestQueuedMachine = CurrentMachine;
    }
}
