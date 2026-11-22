using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Commands.ImmediateCommands;
using EpsilonCore.Commands.MotionCommands;
using EpsilonCore.Communication;
using EpsilonCore.Display;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using GenericHelpers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace EpsilonCore.Engine;

/// <summary>
/// This contains the queues for all machines and runs the main loop.
/// The main loop plans queued commands, sends them to microcontrollers, and the communicators' receive handlers
/// process the responses. Start this process by adding a machine with <see cref="AddMachineQueue"/>,
/// which starts a background thread running the main loop.
///
/// Commands run in one of three ways:
/// <list type="bullet">
/// <item><b>Scheduled</b> (<see cref="IScheduledCommand"/>, and plain <see cref="ICommand"/>s): queued with
/// <see cref="EnqueueCommand"/>, run in order at a known time. Their resultant machine is predicted when they are queued.
/// Once the board clocks are synchronized, each one is given the exact board clock tick to start at
/// (see <see cref="CommandTiming"/>), which keeps several boards in step.</item>
/// <item><b>Conditional</b> (<see cref="IConditionalCommand"/>): queued and run in order, but finish at an unknown time
/// because they wait for a sensor. The board reports a <see cref="CommandOutcome"/> and the command's
/// <see cref="IConditionalCommand.Resolve"/> builds the real resultant machine. If the result can't be predicted,
/// planning stops at the command (a barrier) until it resolves.</item>
/// <item><b>Immediate</b> (<see cref="IImmediateCommand"/>): sent with <see cref="SendImmediate"/> straight from the
/// caller's thread, skipping every queue and the board's command buffer. Used for emergency stop, pause, resume,
/// babystep and status queries.</item>
/// </list>
///
/// Each command can contain multiple data packets and each data packet can be sent to a different microcontroller.
/// Every data packet gets a sequence number when it is sent, and the board answers with that sequence number,
/// so completions are matched to the right command even when several boards finish out of order.
/// A command only resolves once all of its packets have completed and every command before it has resolved,
/// so <see cref="MachineQueue.CurrentMachine"/> always advances in queue order.
/// Space in the boards' command buffers is tracked separately with <see cref="CommandCode.BUFFER_BYTES_FREED"/> messages.
///
/// See the "Communication Protocol" page of the Obsidian documentation for the packet formats the boards must implement.
/// </summary>
public class EpsilonEngine(IResultMessageLogger logger)
{
    /// <summary>
    /// Once this many moves are waiting, half of them are solved and sent.
    /// </summary>
    private const int LOOK_AHEAD_COUNT = 20;

    /// <summary>
    /// The earliest a scheduled command is set to start, measured from when it is sent.
    /// Leaves time for the data packet to reach the board.
    /// </summary>
    private const double SCHEDULE_LEAD_SECONDS = 0.1;

    /// <summary>
    /// How often each board's clock is sampled once it is synchronized.
    /// </summary>
    private const double CLOCK_SYNC_INTERVAL_SECONDS = 1.0;

    /// <summary>
    /// How often each board's clock is sampled until it has <see cref="CLOCK_SYNC_FAST_SAMPLE_COUNT"/> samples.
    /// </summary>
    private const double CLOCK_SYNC_FAST_INTERVAL_SECONDS = 0.2;

    private const int CLOCK_SYNC_FAST_SAMPLE_COUNT = 4;

    /// <summary>
    /// A clock sync request without a response after this long is abandoned.
    /// </summary>
    private const double CLOCK_SYNC_RESPONSE_TIMEOUT_SECONDS = 2.0;

    /// <summary>
    /// Extra time allowed after a conditional command's timeout before the host gives up on it.
    /// </summary>
    private const double CONDITIONAL_TIMEOUT_GRACE_SECONDS = 2.0;

    /// <summary>
    /// Extra time allowed after a scheduled command's expected duration before the host gives up on it.
    /// </summary>
    private const double SCHEDULED_COMPLETION_GRACE_SECONDS = 10.0;

    /// <summary>
    /// How long the host waits for every board to confirm a pause.
    /// </summary>
    private const double PAUSE_CONFIRMATION_TIMEOUT_SECONDS = 2.0;

    /// <summary>
    /// How long the main loop waits when no machine has anything to do.
    /// It wakes up early whenever a command is enqueued or a data packet is received.
    /// </summary>
    private const int IDLE_WAIT_MILLISECONDS = 500;

    /// <summary>
    /// How long the main loop waits when machines have work but can't make progress
    /// (for example waiting for buffer space or a barrier).
    /// </summary>
    private const int BLOCKED_WAIT_MILLISECONDS = 20;

    private readonly AutoResetEvent wakeMainLoop = new(false);

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
    /// For <see cref="IConditionalCommand"/>s, <see cref="QueuedCommand.ResultantMachine"/> is the machine returned by
    /// <see cref="IConditionalCommand.Resolve"/>.
    /// The sender object will be the <see cref="QueuedCommand.ResultantMachine"/> that resulted from the command.
    /// </summary>
    public EventHandler<QueuedCommand>? CommandResolved;

    /// <summary>
    /// Raised when a sent command fails (a board reported a failure, the command couldn't be resolved, or no response
    /// arrived in time). The machine queue stops and must be restarted with <see cref="ClearFaults"/>.
    /// The sender object will be the machine queue's current machine.
    /// </summary>
    public EventHandler<QueuedCommand>? CommandFailed;

    /// <summary>
    /// Raised when <see cref="AddMachineQueue"/> is called.
    /// The event argument is the id of the <see cref="MachineQueue"/> added.
    /// The sender object will be this <see cref="EpsilonEngine"/>.
    /// </summary>
    public EventHandler<uint>? MachineQueueAdded;

    /// <summary>
    /// Raised when a machine queue's status changes (for example to paused or emergency stopped).
    /// The sender object will be this <see cref="EpsilonEngine"/>.
    /// </summary>
    public EventHandler<(uint MachineId, MachineQueueStatus Status)>? MachineQueueStatusChanged;

    /// <summary>
    /// Raised when a board sends a <see cref="CommandCode.STATUS_REPORT"/>.
    /// The sender object will be this <see cref="EpsilonEngine"/>.
    /// </summary>
    public EventHandler<BoardStatus>? BoardStatusReceived;

    private ConcurrentDictionary<uint, MachineQueue> MachineQueues { get; } = [];

    /// <summary>
    /// Indicates if the main loop is running.
    /// False until Start is called.
    /// </summary>
    public bool Running { get; private set; } = false;
    private Thread? mainThread;

    /// <summary>
    /// Logger to use for errors that occur not related to a specific machine.
    /// </summary>
    public IResultMessageLogger EngineLogger { get; } = logger;

    /// <summary>
    /// Creates a new machine machineQueue for the given machine.
    /// There is exactly one machine machineQueue per machine.
    /// </summary>
    /// <param name="machine">Machine whose boards are searched.</param>
    public void AddMachineQueue(Machine machine)
    {
        //TODO: Epsilon engine should make the Machine object and commands should be used to manipulate them.
        uint id = MachineQueues.NextId();
        MachineQueue machineQueue = new(machine) { Id = id };
        bool success = MachineQueues.TryAdd(id, machineQueue);

        if (success)
        {
            machine.ResultMessageLogger.Log($"New machine added: '{machine.Name}'.");
            MachineQueueAdded?.Invoke(this, id);
        }
        else
            machine.ResultMessageLogger.Log($"New machine failed to be added: '{machine.Name}'.");

        if (!Running)
            Start();
    }

    /// <summary>
    /// Starts the main loop to process commands.
    /// </summary>
    private void Start()
    {
        Running = true;
        mainThread = new Thread(MainLoop)
        {
            // Don't keep the process alive when the application closes.
            IsBackground = true,
            Name = $"{nameof(EpsilonEngine)} main loop",
        };
        mainThread.Start();
    }

    /// <summary>
    /// Adds an <see cref="ICommunicator"/> to a <see cref="MachineQueue"/>.
    /// This is done at the engine level because <see cref="ICommunicator"/>s are mutable and <see cref="Machine"/>s
    /// are immutable so it's more correct to not have a mutable object inside an immutable one.
    /// Returns null if successful or an exception if there was an error.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <param name="communicator">The communicator the data packet came from.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? AddCommunicator(uint machineQueueId, ICommunicator communicator)
    {
        bool success = MachineQueues.TryGetValue(machineQueueId, out MachineQueue? machineQueue);

        if (!success || machineQueue is null)
        {
            string message = $"Communicator '{communicator.Name}' could not be added because {nameof(MachineQueue)} {machineQueueId} doesn't exist.";
            Debug.WriteLine(message);
            return new ArgumentException(message);
        }

        lock (machineQueue.SyncRoot)
        {
            uint nextBoardQueueId = machineQueue.Communicators.NextId();
            communicator.Id = nextBoardQueueId;

            success = machineQueue.Communicators.TryAdd(nextBoardQueueId, communicator);
            if (!success)
            {
                string message = $"Communicator '{communicator.Name}' could not be added due to a multithreading error.";
                Debug.WriteLine(message);
                return new ArgumentException(message);
            }

            machineQueue.BoardStates[nextBoardQueueId] = new BoardState(communicator)
            {
                ScheduledEndHostSeconds = HostClock.NowSeconds
            };
        }

        communicator.DataPacketsReceivedHandler += (sender, dataPackets) =>
        {
            Debug.Assert(sender is not null);
            foreach (DataPacket dataPacket in dataPackets)
                ProcessDataPacket((ICommunicator)sender, dataPacket);
        };

        success = communicator.Connect();
        return success ? null : new Exception($"Could not connect to Communicator '{communicator.Name}'.");
    }

    /// <summary>
    /// Handles a data packet received from a board. Called on the communicator's receive thread.
    /// </summary>
    /// <param name="communicator">The communicator the data packet came from.</param>
    /// <param name="dataPacket">The received data packet.</param>
    private void ProcessDataPacket(ICommunicator communicator, DataPacket dataPacket)
    {
        bool success = MachineQueues.TryGetValue(communicator.MachineId, out MachineQueue? machineQueue);
        if (!success || machineQueue is null)
        {
            EngineLogger.Log($"Data packet received from communicator '{communicator.Name}' but MachineQueue {communicator.MachineId} doesn't exist.");
            return;
        }

        RunLocked(machineQueue, () => ProcessDataPacketLocked(machineQueue, communicator, dataPacket));
        wakeMainLoop.Set();
    }

    /// <summary>
    /// Runs <paramref name="action"/> while holding the machine queue's lock, then raises any events it queued.
    /// Events are raised after the lock is released so handlers can't deadlock with other threads.
    /// </summary>
    /// <param name="machineQueue">The machine queue whose lock is taken.</param>
    /// <param name="action">Work to do while holding the lock.</param>
    private static void RunLocked(MachineQueue machineQueue, Action action)
    {
        List<Action> events;
        lock (machineQueue.SyncRoot)
        {
            action();
            events = machineQueue.TakePendingEvents();
        }

        foreach (Action raiseEvent in events)
            raiseEvent();
    }

    /// <summary>
    /// Handles a data packet received from a board while holding the machine queue's lock.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="communicator">The communicator the data packet came from.</param>
    /// <param name="dataPacket">The received data packet.</param>
    private void ProcessDataPacketLocked(MachineQueue machineQueue, ICommunicator communicator, DataPacket dataPacket)
    {
        Machine machine = machineQueue.CurrentMachine;
        IResultMessageLogger logger = machine.ResultMessageLogger;

        bool boardFound = machine.Entities.Boards.TryGetValue(communicator.BoardId, out Board? board);
        string dataPacketSource;
        if (boardFound && board is not null)
            dataPacketSource = $"{machine.Name} (board {board.Id})";
        else
            dataPacketSource = $"{machine.Name} (communicator {communicator.Id})";

        Result<CommandCode> commandCode = dataPacket.CommandCode.ToCommandCode();

        // If unrecognized command code is received, log and continue
        if (commandCode.IsError)
        {
            logger.Log($"Unregocnized command code received from {dataPacketSource}: {dataPacket.CommandCode}.");
            logger.Log(commandCode.Exception.Message);
            return;
        }

        machineQueue.BoardStates.TryGetValue(communicator.Id, out BoardState? boardState);

        // TODO: add validation on data length for each command code type
        switch (commandCode.Value)
        {
            case CommandCode.DISPLAY_TEXT:
                {
                    string message = System.Text.Encoding.ASCII.GetString(dataPacket.Payload);
                    logger.Log($"Message from {dataPacketSource}:\n\t{message}.");
                    break;
                }
            case CommandCode.COMMAND_COMPLETE:
                {
                    CompletePacket(machineQueue, boardState, CommandOutcome.FromCommandComplete(dataPacket), dataPacketSource);
                    break;
                }
            case CommandCode.COMMAND_RESULT:
                {
                    Result<CommandOutcome> outcome = CommandOutcome.FromCommandResult(dataPacket);
                    if (outcome.IsError)
                    {
                        logger.Log($"Invalid command result for command {dataPacket.SequenceNumber} from {dataPacketSource}: {outcome.Exception.Message}");
                        return;
                    }
                    CompletePacket(machineQueue, boardState, outcome.Value, dataPacketSource);
                    break;
                }
            case CommandCode.BUFFER_BYTES_FREED:
                {
                    if (dataPacket.Payload.Length < sizeof(uint))
                    {
                        logger.Log($"Invalid {nameof(CommandCode.BUFFER_BYTES_FREED)} from {dataPacketSource}: payload must be {sizeof(uint)} bytes.");
                        return;
                    }
                    communicator.FreeBufferBytes(BitConverter.ToUInt32(dataPacket.Payload[..sizeof(uint)]));
                    break;
                }
            case CommandCode.CLOCK_SYNC_RESPONSE:
                {
                    double receivedAt = HostClock.NowSeconds;
                    if (boardState?.ClockSync.RequestSentAt is not double sentAt)
                        return;

                    boardState.ClockSync.RequestSentAt = null;
                    if (dataPacket.Payload.Length < sizeof(ulong))
                    {
                        logger.Log($"Invalid {nameof(CommandCode.CLOCK_SYNC_RESPONSE)} from {dataPacketSource}: payload must be {sizeof(ulong)} bytes.");
                        return;
                    }
                    boardState.ClockSync.AddSample(sentAt, receivedAt, BitConverter.ToUInt64(dataPacket.Payload[..sizeof(ulong)]));
                    break;
                }
            case CommandCode.STATUS_REPORT:
                {
                    Result<BoardStatus> status = BoardStatus.FromDataPacket(machineQueue.Id, dataPacket);
                    if (status.IsError)
                    {
                        logger.Log($"Invalid status report from {dataPacketSource}: {status.Exception.Message}");
                        return;
                    }
                    ProcessBoardStatus(machineQueue, boardState, status.Value, dataPacketSource);
                    break;
                }
            case CommandCode.ERROR:
                {
                    // log errors from microcontroller
                    // TODO: require board restart?
                    byte[] allData = [.. dataPacket.AllData];
                    if (allData.Length <= DataPacket.PAYLOAD_INDEX)
                    {
                        logger.Log($"Error with command {dataPacket.SequenceNumber} on {dataPacketSource}.");
                        return;
                    }
                    Result<BoardErrorCode> errorCode = allData[3].ToBoardErrorCode();
                    if (errorCode.IsError)
                    {
                        logger.Log($"Unrecognized error with command {dataPacket.SequenceNumber} on {dataPacketSource}.");
                        logger.Log(errorCode.Exception.Message);
                        return;
                    }
                    if (allData.Length < 4 + sizeof(UInt32))
                    {
                        logger.Log($"Error with command {dataPacket.SequenceNumber} on {dataPacketSource}:\n\t{errorCode.Value.DisplayMessage()}");
                        return;
                    }
                    UInt32 lineNumber = BitConverter.ToUInt32(allData, 4);
                    string fileName = BitConverter.ToString(allData, 4 + sizeof(UInt32));
                    logger.Log($"Error with command {dataPacket.SequenceNumber} on {dataPacketSource}:\n\t{fileName} line {lineNumber}\n\t{errorCode.Value.DisplayMessage()}");

                    break;
                }
            default:
                {
                    logger.Log($"Unexpected {commandCode.Value} message received from {dataPacketSource}.");
                    break;
                }
        }
    }

    /// <summary>
    /// Records that a board finished one of a command's data packets and resolves any commands that are now complete.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="boardState">Bookkeeping for the board the message came from, or null if it isn't known.</param>
    /// <param name="outcome">The reported result.</param>
    /// <param name="dataPacketSource">Description of the sender, for the log.</param>
    private void CompletePacket(MachineQueue machineQueue, BoardState? boardState, CommandOutcome outcome, string dataPacketSource)
    {
        if (boardState is null ||
            !boardState.InFlightBySequenceNumber.Remove(outcome.SequenceNumber, out InFlightCommand? inFlightCommand))
        {
            // After a stop or pause, completions for discarded commands can still arrive.
            if (machineQueue.SendStatus is MachineQueueStatus.Running or MachineQueueStatus.Pausing)
                machineQueue.CurrentMachine.ResultMessageLogger.Log(
                    $"Warning: Command {outcome.SequenceNumber} completed on {dataPacketSource} but no command with that sequence number was in flight.");
            return;
        }

        inFlightCommand.Complete(outcome);
        ResolveCompletedCommands(machineQueue);
    }

    /// <summary>
    /// Resolves completed commands from the front of <see cref="MachineQueue.InFlight"/>, applying their resultant
    /// machines in order. Stops at the first command that isn't complete.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private void ResolveCompletedCommands(MachineQueue machineQueue)
    {
        while (machineQueue.InFlight.Count > 0 && machineQueue.InFlight[0].IsComplete)
        {
            InFlightCommand inFlightCommand = machineQueue.InFlight[0];
            QueuedCommand queuedCommand = inFlightCommand.QueuedCommand;
            Debug.Assert(queuedCommand.ResultantMachine is not null);

            CommandOutcome? failedOutcome = inFlightCommand.Outcomes.Values
                .FirstOrDefault(outcome => outcome.Status != CommandOutcomeStatus.Success);
            if (failedOutcome is not null)
            {
                string boardName = machineQueue.Communicators.TryGetValue(failedOutcome.CommunicatorId, out ICommunicator? communicator)
                    ? $"'{communicator.Name}'"
                    : $"communicator {failedOutcome.CommunicatorId}";
                Fault(machineQueue, queuedCommand, $"{boardName} reported {failedOutcome.Status}");
                return;
            }

            Machine resolvedMachine = queuedCommand.ResultantMachine;
            if (queuedCommand.Command is IConditionalCommand conditionalCommand)
            {
                Result<Machine> result = conditionalCommand.Resolve(queuedCommand, inFlightCommand.Outcomes);
                if (result.IsError)
                {
                    Fault(machineQueue, queuedCommand, result.Exception.Message);
                    return;
                }

                resolvedMachine = result.Value;
                UpdateScheduleAfterConditional(machineQueue, inFlightCommand);

                // Planning can continue now that the machine after the barrier is known.
                if (ReferenceEquals(machineQueue.PendingBarrier, queuedCommand))
                {
                    machineQueue.PendingBarrier = null;
                    machineQueue.LatestQueuedMachine = resolvedMachine;
                }
            }

            machineQueue.InFlight.RemoveAt(0);
            machineQueue.CurrentMachine = resolvedMachine;
            QueuedCommand resolvedCommand = queuedCommand with { ResultantMachine = resolvedMachine };
            machineQueue.DeferEvent(() => CommandResolved?.Invoke(resolvedMachine, resolvedCommand));
        }
    }

    /// <summary>
    /// Stops the machine queue after a sent command failed: the other boards are paused (which discards their buffers),
    /// every unresolved command is discarded and the queue waits for <see cref="ClearFaults"/>.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="failedCommand">The command that failed, or null if the fault isn't caused by a specific command.</param>
    /// <param name="message">Why the machine queue faulted, for the log.</param>
    private void Fault(MachineQueue machineQueue, QueuedCommand? failedCommand, string message)
    {
        IResultMessageLogger logger = machineQueue.CurrentMachine.ResultMessageLogger;
        if (failedCommand is not null)
        {
            logger.Log(Helper.GetFormattedDisplayMessage(failedCommand.Command, ErrorLevel.Error, message));
            Machine machine = machineQueue.CurrentMachine;
            machineQueue.DeferEvent(() => CommandFailed?.Invoke(machine, failedCommand));
        }
        else
            logger.Log($"Error: {message}.");

        logger.Log("Machine stopped and all queued commands were discarded. Clear faults to continue.");

        SendToAllBoards(machineQueue, CommandCode.PAUSE);
        machineQueue.ClearAllUnresolvedCommands(HostClock.NowSeconds);
        SetStatus(machineQueue, MachineQueueStatus.Stopped, MachineQueueStatus.Stopped);
    }

    /// <summary>
    /// Sends an immediate command code with no payload to every board, ignoring failures.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="commandCode">Immediate command code to send.</param>
    private static void SendToAllBoards(MachineQueue machineQueue, CommandCode commandCode)
    {
        foreach (ICommunicator communicator in machineQueue.Communicators.Values)
        {
            Result<DataPacket> dataPacket = DataPacket.New(communicator.Id, 0, commandCode, []);
            if (dataPacket.IsSuccess)
                communicator.Send(dataPacket.Value);
        }
    }

    /// <summary>
    /// Sets the machine queue's planning and sending statuses and raises <see cref="MachineQueueStatusChanged"/> if the shown status changed.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="enqueueStatus">New planning status.</param>
    /// <param name="sendStatus">New sending status.</param>
    private void SetStatus(MachineQueue machineQueue, MachineQueueStatus enqueueStatus, MachineQueueStatus sendStatus)
    {
        MachineQueueStatus before = GetStatus(machineQueue);
        machineQueue.EnqueueStatus = enqueueStatus;
        machineQueue.SendStatus = sendStatus;
        MachineQueueStatus after = GetStatus(machineQueue);

        if (before != after)
        {
            uint id = machineQueue.Id;
            machineQueue.DeferEvent(() => MachineQueueStatusChanged?.Invoke(this, (id, after)));
        }
    }

    /// <summary>
    /// The single status shown for a machine queue: the send status unless sending is running.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <returns>The send status unless sending is running, otherwise the enqueue status.</returns>
    private static MachineQueueStatus GetStatus(MachineQueue machineQueue) =>
        machineQueue.SendStatus != MachineQueueStatus.Running ? machineQueue.SendStatus : machineQueue.EnqueueStatus;

    /// <summary>
    /// Once a conditional command finishes, its boards' end times are known again: the command's completion time
    /// plus whatever was sent after it.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="inFlightCommand">The conditional command that resolved.</param>
    private static void UpdateScheduleAfterConditional(MachineQueue machineQueue, InFlightCommand inFlightCommand)
    {
        double now = HostClock.NowSeconds;
        foreach ((uint communicatorId, CommandOutcome outcome) in inFlightCommand.Outcomes)
        {
            if (!machineQueue.BoardStates.TryGetValue(communicatorId, out BoardState? state))
                continue;

            state.UnresolvedConditionalCount = Math.Max(0, state.UnresolvedConditionalCount - 1);
            if (state.UnresolvedConditionalCount > 0)
                continue;

            double completedAt = outcome.CompletionTick > 0 && state.ClockSync.IsSynced
                ? Math.Min(now, state.ClockSync.BoardTicksToHost(outcome.CompletionTick))
                : now;
            state.ScheduledEndHostSeconds = completedAt + state.SecondsQueuedAfterConditional;
            state.SecondsQueuedAfterConditional = 0;
        }
    }

    /// <summary>
    /// Handles a <see cref="CommandCode.STATUS_REPORT"/>: finishes a pause once every board has paused,
    /// and stops the machine queue if a board reports it stopped on its own.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="boardState">Bookkeeping for the board the message came from, or null if it isn't known.</param>
    /// <param name="status">The reported status.</param>
    /// <param name="dataPacketSource">Description of the sender, for the log.</param>
    private void ProcessBoardStatus(MachineQueue machineQueue, BoardState? boardState, BoardStatus status, string dataPacketSource)
    {
        if (boardState is not null)
            boardState.LastStatus = status;

        machineQueue.DeferEvent(() => BoardStatusReceived?.Invoke(this, status));

        switch (status.State)
        {
            case BoardRunState.Paused when machineQueue.SendStatus == MachineQueueStatus.Pausing:
                machineQueue.BoardsYetToPause.Remove(status.CommunicatorId);
                if (machineQueue.BoardsYetToPause.Count <= 0)
                    FinishPause(machineQueue);
                break;

            // The board stopped on its own, for example from a hardware emergency stop button.
            case BoardRunState.EmergencyStopped when machineQueue.SendStatus != MachineQueueStatus.EmergencyStopped:
                SendToAllBoards(machineQueue, CommandCode.EMERGENCY_STOP);
                ApplyEmergencyStop(machineQueue, $"{dataPacketSource} reported an emergency stop");
                break;

            case BoardRunState.Faulted when machineQueue.SendStatus is MachineQueueStatus.Running or MachineQueueStatus.Pausing:
                Fault(machineQueue, null, $"{dataPacketSource} reported a fault");
                break;
        }
    }

    /// <summary>
    /// Called once every board has confirmed it paused. The boards discarded every command they hadn't started,
    /// so those commands (and everything not sent yet) go back in front of the commands waiting to be queued.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private void FinishPause(MachineQueue machineQueue)
    {
        List<ICommand> commandsToRunAgain = [];
        bool commandSplitAcrossBoards = false;

        foreach (InFlightCommand inFlightCommand in machineQueue.InFlight)
        {
            // A command some boards finished but that didn't resolve means boards stopped at different points.
            if (inFlightCommand.CompletedPacketCount > 0)
                commandSplitAcrossBoards = true;
            commandsToRunAgain.Add(inFlightCommand.QueuedCommand.Command);
        }

        foreach (QueuedCommand queuedCommand in machineQueue.ReadyToSend)
            if (!machineQueue.InFlight.Any(inFlight => ReferenceEquals(inFlight.QueuedCommand, queuedCommand)))
                commandsToRunAgain.Add(queuedCommand.Command);

        commandsToRunAgain.AddRange(machineQueue.Queued.Select(queuedCommand => queuedCommand.Command));
        commandsToRunAgain.AddRange(machineQueue.CommandsToEnqueue);

        IResultMessageLogger logger = machineQueue.CurrentMachine.ResultMessageLogger;
        machineQueue.ClearAllUnresolvedCommands(HostClock.NowSeconds);

        if (commandSplitAcrossBoards)
        {
            machineQueue.CurrentMachine = machineQueue.CurrentMachine.WithAllAxesUnhomed();
            machineQueue.LatestQueuedMachine = machineQueue.CurrentMachine;
            SetStatus(machineQueue, MachineQueueStatus.Stopped, MachineQueueStatus.Stopped);
            logger.Log("Error: the pause stopped boards partway through a command that spans several boards. " +
                "Queued commands were discarded and all axes must be homed again. Clear faults to continue.");
            return;
        }

        foreach (ICommand command in commandsToRunAgain)
            machineQueue.CommandsToEnqueue.Enqueue(command);

        SetStatus(machineQueue, MachineQueueStatus.Paused, MachineQueueStatus.Paused);
        logger.Log($"Paused. {commandsToRunAgain.Count} command(s) will run when resumed.");
    }

    /// <summary>
    /// Host side of an emergency stop: everything unresolved is discarded and, since the boards stopped mid-command,
    /// no axis position can be trusted.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="reason">Why the emergency stop happened, for the log.</param>
    private void ApplyEmergencyStop(MachineQueue machineQueue, string reason)
    {
        machineQueue.ClearAllUnresolvedCommands(HostClock.NowSeconds);
        machineQueue.CurrentMachine = machineQueue.CurrentMachine.WithAllAxesUnhomed();
        machineQueue.LatestQueuedMachine = machineQueue.CurrentMachine;
        machineQueue.LiveAdjustments.Clear();
        SetStatus(machineQueue, MachineQueueStatus.EmergencyStopped, MachineQueueStatus.EmergencyStopped);
        machineQueue.CurrentMachine.ResultMessageLogger.Log(
            $"{reason}. All queued commands were discarded and all axes must be homed again. Clear faults to continue.");
    }

    /// <summary>
    /// Stops the main loop on cleanup.
    /// </summary>
    ~EpsilonEngine()
    {
        Stop();
    }

    /// <summary>
    /// Stops the main loop to stop process commands.
    /// </summary>
    private void Stop()
    {
        if (!Running)
            return;

        Running = false;
        wakeMainLoop.Set();

        mainThread?.Join();
        mainThread = null;
    }

    /// <summary>
    /// Adds a command to the command machineQueue to be processed.
    /// </summary>
    /// <param name="machineQueueIndex">Id of the machine queue.</param>
    /// <param name="command">The command to queue.</param>
    public void EnqueueCommand(uint machineQueueIndex, ICommand command) =>
        EnqueueCommands(machineQueueIndex, [command]);

    /// <summary>
    /// Adds a list of commands to the command machineQueue to be processed.
    /// </summary>
    /// <param name="machineQueueIndex">Id of the machine queue.</param>
    /// <param name="commands">The commands to queue, in order.</param>
    public void EnqueueCommands(uint machineQueueIndex, IEnumerable<ICommand> commands)
    {
        bool success = MachineQueues.TryGetValue(machineQueueIndex, out MachineQueue? queue);
        if (!success || queue is null)
            return;

        // Lock so a pause can't put interrupted commands back in front of the queue while these are being added.
        lock (queue.SyncRoot)
        {
            foreach (ICommand command in commands)
                queue.CommandsToEnqueue.Enqueue(command);
        }

        wakeMainLoop.Set();
    }

    /// <summary>
    /// This is what the mainThread runs.
    /// </summary>
    private void MainLoop()
    {
        while (Running)
        {
            bool anyActive = false;
            bool progressed = false;
            foreach (MachineQueue machineQueue in MachineQueues.Values)
            {
                RunLocked(machineQueue, () =>
                {
                    MaintainClockSync(machineQueue);
                    CheckWatchdogs(machineQueue);

                    // Skip stopped queues and empty MachineQueues.
                    if (machineQueue.IsEmpty || machineQueue.SendStatus != MachineQueueStatus.Running)
                        return;

                    anyActive = true;
                    progressed |= EnqueueNextCommand(machineQueue);
                    progressed |= SendSomeQueuedCommands(machineQueue);
                });
            }

            // Prevents 100% CPU usage when nothing is happening on every MachineQueue.
            if (!progressed)
                wakeMainLoop.WaitOne(anyActive ? BLOCKED_WAIT_MILLISECONDS : IDLE_WAIT_MILLISECONDS);
        }
    }

    /// <summary>
    /// Periodically sends <see cref="CommandCode.CLOCK_SYNC_REQUEST"/> to every board whose <see cref="Board"/>
    /// has been added to the machine.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private static void MaintainClockSync(MachineQueue machineQueue)
    {
        double now = HostClock.NowSeconds;
        foreach (BoardState boardState in machineQueue.BoardStates.Values)
        {
            ClockSync clockSync = boardState.ClockSync;

            if (clockSync.RequestSentAt is double sentAt)
            {
                if (now - sentAt < CLOCK_SYNC_RESPONSE_TIMEOUT_SECONDS)
                    continue;
                clockSync.RequestSentAt = null;
            }

            double interval = clockSync.SampleCount < CLOCK_SYNC_FAST_SAMPLE_COUNT
                ? CLOCK_SYNC_FAST_INTERVAL_SECONDS
                : CLOCK_SYNC_INTERVAL_SECONDS;
            if (now - clockSync.LastRequestAt < interval)
                continue;

            Board? board = FindBoard(machineQueue.CurrentMachine, boardState.Communicator.Id);
            if (board is null)
                continue;

            clockSync.NominalFrequencyHz = board.ClockFrequency.Hertz;

            Result<DataPacket> request = DataPacket.New(boardState.Communicator.Id, 0, CommandCode.CLOCK_SYNC_REQUEST, []);
            Debug.Assert(request.IsSuccess);

            clockSync.LastRequestAt = now;
            double sendTime = HostClock.NowSeconds;
            if (boardState.Communicator.Send(request.Value))
                clockSync.RequestSentAt = sendTime;
        }
    }

    /// <summary>
    /// Returns the board that uses the given communicator, or null if no such board has been added to the machine.
    /// </summary>
    /// <param name="machine">Machine whose boards are searched.</param>
    /// <param name="communicatorId">Id of the communicator to find the board of.</param>
    /// <returns>The board, or null if none uses the communicator.</returns>
    private static Board? FindBoard(Machine machine, uint communicatorId) =>
        machine.Entities.Boards.Values.FirstOrDefault(board => board.CommunicatorId == communicatorId);

    /// <summary>
    /// Faults the machine queue if a board stops responding: if the front in-flight command takes much longer than
    /// expected, or if a pause isn't confirmed.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private void CheckWatchdogs(MachineQueue machineQueue)
    {
        double now = HostClock.NowSeconds;

        if (machineQueue.SendStatus == MachineQueueStatus.Pausing &&
            now - machineQueue.PauseRequestedAt > PAUSE_CONFIRMATION_TIMEOUT_SECONDS)
        {
            IEnumerable<string> boardNames = machineQueue.BoardsYetToPause
                .Select(id => machineQueue.Communicators.TryGetValue(id, out ICommunicator? communicator) ? $"'{communicator.Name}'" : $"communicator {id}");
            Fault(machineQueue, null, $"{string.Join(", ", boardNames)} didn't confirm the pause within {PAUSE_CONFIRMATION_TIMEOUT_SECONDS} s, so it's unknown which commands ran");
            return;
        }

        if (machineQueue.SendStatus != MachineQueueStatus.Running || machineQueue.InFlight.Count <= 0)
        {
            machineQueue.WatchdogFront = (null, now);
            return;
        }

        InFlightCommand front = machineQueue.InFlight[0];

        // Start timing once the command is at the front and fully sent, which is when the board can start it.
        if (!ReferenceEquals(machineQueue.WatchdogFront.Command, front) || !front.AllPacketsSent)
        {
            machineQueue.WatchdogFront = (front, now);
            return;
        }

        double allowedSeconds = front.QueuedCommand.Command is IConditionalCommand conditionalCommand
            ? conditionalCommand.Timeout.Seconds + CONDITIONAL_TIMEOUT_GRACE_SECONDS
            : front.QueuedCommand.ExpectedDuration.Seconds + SCHEDULED_COMPLETION_GRACE_SECONDS;

        if (now - machineQueue.WatchdogFront.Since > allowedSeconds)
            Fault(machineQueue, front.QueuedCommand, $"no response from the board within {allowedSeconds:0.#} s");
    }

    /*
     * TODO: I need to figure out how to handle a situation like setting the temp of a hotend then moving the extruder motor.
     * Possible Solution:
     * SetTargetTempCommand
     * RequireTemperatureRangeCommand
     * MoveCommand
     * When queueing commands, I will just validate that the target temp is set correctly for an extrusion.
     * The actual temperature will need to be measured at the time the command executes.
     * This is best done at the microcontroller level.
     */

    /// <summary>
    /// Moves the next <see cref="ICommand"/>s from <see cref="MachineQueue.CommandsToEnqueue"/> to <see cref="MachineQueue.Queued"/>.
    /// Will then try to move the command from <see cref="MachineQueue.Queued"/> to <see cref="MachineQueue.ReadyToSend"/>.
    /// This process involved getting the output machine for a command by calling <see cref="ICommand.EnqueueCommandSpecific(Machine)"/>.
    /// This will also solve the <see cref="MoveQueue"/> when either enough moves are queued or a command requires zero velocity.
    /// Nothing is planned while an unpredictable <see cref="IConditionalCommand"/> (a barrier) is unresolved.
    /// Returns true if anything changed.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <returns>True if anything changed.</returns>
    private bool EnqueueNextCommand(MachineQueue machineQueue)
    {
        if (machineQueue.EnqueueStatus != MachineQueueStatus.Running)
            return false;

        // The machine the next command would start from isn't known until the barrier resolves.
        if (machineQueue.PendingBarrier is not null)
            return false;

        if (machineQueue.CommandsToEnqueue.IsEmpty)
        {
            if (machineQueue.Queued.Count <= 0)
                return false;

            SolveMovesAndMarkCommandsAsReadyToSend(machineQueue);
            return true;
        }

        bool success = machineQueue.CommandsToEnqueue.TryDequeue(out ICommand? command);

        Debug.Assert(success,
            $"Dequeueing {nameof(MachineQueue.CommandsToEnqueue)} should never fail.");

        Debug.Assert(command is not null,
            $"{nameof(MachineQueue.CommandsToEnqueue)} should never contain a null command.");

        Machine initialMachine = machineQueue.LatestQueuedMachine;
        QueuedCommand queuedCommand = command.EnqueueCommandSpecific(initialMachine);

        // check for explicit enqueue command failures
        if (queuedCommand.ErrorLevel != ErrorLevel.Success)
        {
            // TODO: raise event for failed machineQueue event

            // Log error.
            initialMachine.ResultMessageLogger.Log(Helper.GetFormattedDisplayMessage(queuedCommand));

            // Don't send any more commands to this machineQueue until error is addressed.
            machineQueue.CommandsToEnqueue.Clear();
            SetStatus(machineQueue, MachineQueueStatus.Stopped, machineQueue.SendStatus);
            return true;
        }

        Debug.Assert(queuedCommand.ResultantMachine is not null,
            "All success results require a non-null ResultantMachine property.");

        machineQueue.LatestQueuedMachine = queuedCommand.ResultantMachine;
        machineQueue.DeferEvent(() => CommandQueued?.Invoke(initialMachine, queuedCommand));

        // Commands after a conditional command with an unpredictable result can't be planned until it resolves.
        if (command is IConditionalCommand { IsResultPredictable: false })
            machineQueue.PendingBarrier = queuedCommand;

        // If a non-move command comes in and there is nothing before it then just mark it as ready to send.
        if (machineQueue.Queued.Count <= 0 && command is not MoveCommand)
        {
            machineQueue.AddCommandToReadyToSend(queuedCommand);
            return true;
        }

        // Otherwise, mark it as only queued but not ready to send.
        machineQueue.Queued.Enqueue(queuedCommand);

        if (command is MoveCommand moveCommand)
        {
            Result<Move> move = moveCommand.GetMove(initialMachine);
            if (move.IsError)
            {
                // Log error.
                initialMachine.ResultMessageLogger.Log(move.Exception.Message);

                // Don't send any more commands to this machineQueue until error is addressed.
                machineQueue.CommandsToEnqueue.Clear();
                SetStatus(machineQueue, MachineQueueStatus.Stopped, machineQueue.SendStatus);

                // Go ahead and solve and send all the moves ahead of this command that did work.
                SolveMovesAndMarkCommandsAsReadyToSend(machineQueue);
                return true;
            }
            machineQueue.MoveQueue.Add(move.Value);
        }

        if (command.RequiresZeroVelocity || machineQueue.CommandsToEnqueue.IsEmpty)
        {
            // All moves can be solved and sent if the command requires zero
            // velocity because solving moves will end with zero velocity.
            // Also if there are no more commands to send then we can solve the moves and send them.
            SolveMovesAndMarkCommandsAsReadyToSend(machineQueue);
        }
        else if (machineQueue.MoveQueue.Count > LOOK_AHEAD_COUNT)
        {
            // Enough commands have been queued to the move queue to solve the moves
            // and add them to the command machineQueue.
            // We will only send half of them though because if more moves are coming up, we may
            // want to maintain a higher speed and solvig moves always ends with zero velocity.
            // Roughly the first half will be accelerating and the second half will
            // be decelerating but we don't know if we want to decelerate yet.
            SolveMovesAndMarkHalfOfMoveCommandsAsReadyToSend(machineQueue);
        }

        return true;
    }

    /// <summary>
    /// Solves the <see cref="MoveQueue"/> and moves all commands from <see cref="MachineQueue.Queued"/>
    /// to <see cref="MachineQueue.ReadyToSend"/>.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private static void SolveMovesAndMarkCommandsAsReadyToSend(MachineQueue machineQueue)
    {
        MoveQueue moveQueue = machineQueue.MoveQueue;
        moveQueue.SolveAllMoves(machineQueue.LatestQueuedMachine.MotionSystem.Precisions);

        foreach (QueuedCommand command in machineQueue.Queued)
            machineQueue.AddCommandToReadyToSend(command);

        // Clear the move machineQueue and move commands list but keep the last move to remember velocities for next batch of moves.
        Move? lastMove = moveQueue.Last;
        moveQueue.Clear();
        if (lastMove is not null)
            moveQueue.Add(lastMove);
        machineQueue.Queued.Clear();
    }

    /// <summary>
    /// Solves the <see cref="MoveQueue"/> and moves commands from <see cref="MachineQueue.Queued"/>
    /// to <see cref="MachineQueue.ReadyToSend"/> until half the move commands have been moved and
    /// continues to move commands until another <see cref="MoveCommand"/> is encountered.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    private static void SolveMovesAndMarkHalfOfMoveCommandsAsReadyToSend(MachineQueue machineQueue)
    {
        MoveQueue moveQueue = machineQueue.MoveQueue;
        moveQueue.SolveAllMoves(machineQueue.LatestQueuedMachine.MotionSystem.Precisions);

        int movesToSend = (moveQueue.Count / 2) - 1;
        int movesMarkedReadyToSend = 0;
        int commandsMarkedAsReadyToSend = 0;
        bool markedEnoughMoves = false;
        foreach (QueuedCommand queuedCommand in machineQueue.Queued)
        {
            bool isMoveCommand = queuedCommand.Command is MoveCommand;
            if (markedEnoughMoves && isMoveCommand)
                break;

            machineQueue.AddCommandToReadyToSend(queuedCommand);
            commandsMarkedAsReadyToSend++;

            if (isMoveCommand)
            {
                movesMarkedReadyToSend++;
                moveQueue.Dequeue();
            }

            if (movesMarkedReadyToSend >= movesToSend)
                markedEnoughMoves = true;
        }

        // Clear all commands that have been sent.
        machineQueue.Queued = new Queue<QueuedCommand>(machineQueue.Queued.Skip(commandsMarkedAsReadyToSend));
    }

    /// <summary>
    /// Sends <see cref="DataPacket"/>s of the commands in <see cref="MachineQueue.ReadyToSend"/> via their
    /// <see cref="ICommunicator"/>s and tracks them in <see cref="MachineQueue.InFlight"/>.
    /// Sends until either an error is encountered, a board's command buffer doesn't have enough space for the next
    /// data packet, or a board has 256 packets in flight (all sequence numbers in use).
    /// Returns true if anything changed.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <returns>True if anything changed.</returns>
    private bool SendSomeQueuedCommands(MachineQueue machineQueue)
    {
        bool progressed = false;

        while (machineQueue.SendStatus == MachineQueueStatus.Running &&
            machineQueue.ReadyToSend.Count > 0)
        {
            QueuedCommand nextCommand = machineQueue.ReadyToSend.Peek();

            Debug.Assert(nextCommand.ErrorLevel == ErrorLevel.Warning ||
                nextCommand.ErrorLevel == ErrorLevel.Success,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.ReadyToSend)} should only contain {nameof(QueuedCommand)}s with a status of either {ErrorLevel.Warning.ToDisplayStr()} or {ErrorLevel.Success.ToDisplayStr()}.");

            Debug.Assert(nextCommand.ResultantMachine is not null,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.ReadyToSend)} should never contain a null {nameof(QueuedCommand)}.{nameof(QueuedCommand.ResultantMachine)}.");

            // Commands with no data to send don't need a board.
            if (nextCommand.DataPackets.Count <= 0)
            {
                machineQueue.ReadyToSend.Dequeue();
                progressed = true;

                if (machineQueue.InFlight.Count <= 0)
                {
                    // Just apply the resultant machine if there are no commands in front of the current one.
                    machineQueue.CurrentMachine = nextCommand.ResultantMachine;
                    machineQueue.DeferEvent(() => CommandResolved?.Invoke(nextCommand.ResultantMachine, nextCommand));
                }
                else
                {
                    // We can't apply the resultant machine until the earlier sent commands are completed.
                    machineQueue.InFlight.Add(new InFlightCommand(nextCommand) { AllPacketsSent = true });
                }

                continue;
            }

            if (machineQueue.CommandBeingSent is null && !StartSendingCommand(machineQueue, nextCommand))
                return true;

            InFlightCommand commandBeingSent = machineQueue.CommandBeingSent!;

            while (machineQueue.DataPacketsReadyToSend.Count > 0)
            {
                DataPacket dataPacket = machineQueue.DataPacketsReadyToSend.Peek();
                BoardState boardState = machineQueue.BoardStates[dataPacket.CommunicatorId];
                ICommunicator communicator = boardState.Communicator;
                int dataPacketSize = dataPacket.TotalLength;

                // No more room in the microcontroller's buffer, try again once it frees some.
                if (communicator.BoardCommandBufferBytesFree < dataPacketSize)
                    return progressed;

                // Every sequence number is in use on this board, wait for a completion.
                byte sequenceNumber = communicator.PeekNextSequenceNumber();
                if (boardState.InFlightBySequenceNumber.ContainsKey(sequenceNumber))
                    return progressed;

                // Only assign the sequence number right before sending data packet because if a
                // command is cancelled, we don't want to increment the sequence number.
                sequenceNumber = communicator.IncrementAndReturnNextSequenceNumber();
                DataPacket dataPacketWithSequenceNum = dataPacket.SetSequenceNumber(sequenceNumber);

                // Track the packet before sending in case the board answers before Send returns.
                boardState.InFlightBySequenceNumber[sequenceNumber] = commandBeingSent;
                commandBeingSent.PendingPackets.Add((communicator.Id, sequenceNumber));
                communicator.BoardCommandBufferBytesOccupied += (uint)dataPacketSize;

                if (!communicator.Send(dataPacketWithSequenceNum))
                {
                    boardState.InFlightBySequenceNumber.Remove(sequenceNumber);
                    commandBeingSent.PendingPackets.Remove((communicator.Id, sequenceNumber));
                    communicator.FreeBufferBytes((uint)dataPacketSize);

                    // TODO: Raise event for command send failure.
                    nextCommand.ResultantMachine.ResultMessageLogger.Log(
                        Helper.GetFormattedDisplayMessage(
                            nextCommand.Command,
                            ErrorLevel.Error,
                            $"Failed to send data packet using communicator '{communicator.Name}'"));

                    SetStatus(machineQueue, machineQueue.EnqueueStatus, MachineQueueStatus.Stopped);
                    return true;
                }

                machineQueue.DataPacketsReadyToSend.Dequeue();
                progressed = true;
            }

            // Every data packet of the command has been sent.
            commandBeingSent.AllPacketsSent = true;
            machineQueue.ReadyToSend.Dequeue();
            machineQueue.CommandBeingSent = null;
            machineQueue.DeferEvent(() => CommandSent?.Invoke(nextCommand.InitialMachine, nextCommand));

            // Boards may have answered while the packets were being sent.
            ResolveCompletedCommands(machineQueue);
        }

        return progressed;
    }

    /// <summary>
    /// Starts sending the front command of <see cref="MachineQueue.ReadyToSend"/>: decides when each board should start
    /// it, adds the timing headers and fills <see cref="MachineQueue.DataPacketsReadyToSend"/>.
    /// Returns false (and stops the queue) if the command can't be sent.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="queuedCommand">The front command of <see cref="MachineQueue.ReadyToSend"/>.</param>
    /// <returns>True if the command can be sent, false if the queue was stopped.</returns>
    private bool StartSendingCommand(MachineQueue machineQueue, QueuedCommand queuedCommand)
    {
        IResultMessageLogger logger = machineQueue.CurrentMachine.ResultMessageLogger;

        uint? missingCommunicatorId = queuedCommand.DataPackets
            .Select(dataPacket => (uint?)dataPacket.CommunicatorId)
            .FirstOrDefault(id => !machineQueue.BoardStates.ContainsKey(id!.Value));
        if (missingCommunicatorId is not null)
        {
            logger.Log(Helper.GetFormattedDisplayMessage(queuedCommand.Command, ErrorLevel.Error,
                $"Communicator {missingCommunicatorId} doesn't exist"));
            SetStatus(machineQueue, machineQueue.EnqueueStatus, MachineQueueStatus.Stopped);
            return false;
        }

        IReadOnlyDictionary<uint, CommandTiming> timings = ScheduleCommand(machineQueue, queuedCommand);

        List<DataPacket> dataPacketsWithTiming = [];
        foreach (DataPacket dataPacket in queuedCommand.DataPackets)
        {
            Result<DataPacket> withTiming = dataPacket.WithTiming(timings[dataPacket.CommunicatorId]);
            if (withTiming.IsError)
            {
                logger.Log(Helper.GetFormattedDisplayMessage(queuedCommand.Command, ErrorLevel.Error, withTiming.Exception.Message));
                SetStatus(machineQueue, machineQueue.EnqueueStatus, MachineQueueStatus.Stopped);
                return false;
            }
            dataPacketsWithTiming.Add(withTiming.Value);
        }

        foreach (DataPacket dataPacket in dataPacketsWithTiming)
            machineQueue.DataPacketsReadyToSend.Enqueue(dataPacket);

        InFlightCommand inFlightCommand = new(queuedCommand);
        machineQueue.InFlight.Add(inFlightCommand);
        machineQueue.CommandBeingSent = inFlightCommand;
        return true;
    }

    /// <summary>
    /// Decides when each board should start a command.
    /// If every board involved has a synchronized clock and no unresolved conditional command, all of them get the
    /// same start time (as each board's own clock ticks), right after the latest of their previously scheduled commands.
    /// Otherwise each board is told to start right after its previous command.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="queuedCommand">The command being scheduled.</param>
    /// <returns>The timing header for each communicator the command is sent to, keyed by communicator id.</returns>
    private static IReadOnlyDictionary<uint, CommandTiming> ScheduleCommand(MachineQueue machineQueue, QueuedCommand queuedCommand)
    {
        double now = HostClock.NowSeconds;
        double durationSeconds = Math.Max(0, queuedCommand.ExpectedDuration.Seconds);
        List<BoardState> boardStates = [.. queuedCommand.DataPackets
            .Select(dataPacket => dataPacket.CommunicatorId)
            .Distinct()
            .Select(id => machineQueue.BoardStates[id])];

        Dictionary<uint, CommandTiming> timings = [];
        bool canUseClock = boardStates.All(state => state.ClockSync.IsSynced && state.UnresolvedConditionalCount <= 0);

        if (canUseClock)
        {
            double start = Math.Max(now + SCHEDULE_LEAD_SECONDS, boardStates.Max(state => state.ScheduledEndHostSeconds));
            foreach (BoardState state in boardStates)
            {
                timings[state.Communicator.Id] = CommandTiming.AtClock(state.ClockSync.HostToBoardTicks(start));
                state.ScheduledEndHostSeconds = start + durationSeconds;
            }
        }
        else
        {
            foreach (BoardState state in boardStates)
            {
                timings[state.Communicator.Id] = CommandTiming.AfterPrevious();
                if (state.UnresolvedConditionalCount > 0)
                    state.SecondsQueuedAfterConditional += durationSeconds;
                else
                    state.ScheduledEndHostSeconds = Math.Max(now + SCHEDULE_LEAD_SECONDS, state.ScheduledEndHostSeconds) + durationSeconds;
            }
        }

        // The board's end time is unknown until a conditional command finishes.
        if (queuedCommand.Command is IConditionalCommand)
            foreach (BoardState state in boardStates)
                state.UnresolvedConditionalCount++;

        return timings;
    }

    /// <summary>
    /// Waits until all commands are queued, sent, and completed.
    /// Returns true if successful or false if there was an error or the machine queue was paused or stopped.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <returns>True once every command has resolved, false if the machine queue doesn't exist, isn't running or stops.</returns>
    public bool CompleteAllCommands(uint machineQueueId)
    {
        if (!Running)
            return false;

        bool success = MachineQueues.TryGetValue(machineQueueId, out MachineQueue? queueToClear);

        if (!success || queueToClear is null)
            return false;

        while (true)
        {
            lock (queueToClear.SyncRoot)
            {
                // Stop if there is an error.
                if (queueToClear.EnqueueStatus != MachineQueueStatus.Running ||
                    queueToClear.SendStatus != MachineQueueStatus.Running)
                    return false;

                // Stop if all queues are empty.
                if (queueToClear.IsEmpty)
                    return true;
            }

            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Sends an <see cref="EmergencyStopCommand"/>.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? EmergencyStop(uint machineQueueId) => SendImmediate(machineQueueId, new EmergencyStopCommand());

    /// <summary>
    /// Sends an <see cref="IImmediateCommand"/> right away from the calling thread, skipping every queue.
    /// Returns null if successful or an exception if the command couldn't be sent.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <param name="command">The immediate command to send.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? SendImmediate(uint machineQueueId, IImmediateCommand command)
    {
        if (!MachineQueues.TryGetValue(machineQueueId, out MachineQueue? machineQueue))
            return new ArgumentException($"{nameof(MachineQueue)} {machineQueueId} doesn't exist.");

        // Reading the current machine and communicators doesn't need the lock: both are safe to read from any thread.
        ImmediateCommandContext context = new(
            machineQueue.CurrentMachine,
            [.. machineQueue.Communicators.Keys.Order()]);

        Result<IImmutableList<DataPacket>> dataPackets = command.CreateDataPackets(context);
        if (dataPackets.IsError)
            return dataPackets.Exception;

        DataPacket? notImmediate = dataPackets.Value.FirstOrDefault(packet => !((CommandCode)packet.CommandCode).IsImmediate());
        if (notImmediate is not null)
            return new ArgumentException($"'{command.Description}' created a data packet with {(CommandCode)notImmediate.CommandCode}, which isn't an immediate command code.");

        Exception? result = null;

        if (command.HostAction == ImmediateHostAction.EmergencyStop)
        {
            // Send before taking the lock so the stop reaches the boards as fast as possible,
            // even if the main loop is busy solving moves.
            List<string> failures = SendImmediateDataPackets(machineQueue, dataPackets.Value);
            RunLocked(machineQueue, () =>
            {
                ApplyEmergencyStop(machineQueue, "Emergency stop sent");
                if (failures.Count > 0)
                    machineQueue.CurrentMachine.ResultMessageLogger.Log($"Error: emergency stop could not be sent to: {string.Join(", ", failures)}.");
            });

            if (failures.Count > 0)
                result = new Exception($"Emergency stop could not be sent to: {string.Join(", ", failures)}.");
        }
        else
        {
            RunLocked(machineQueue, () =>
            {
                result = ValidateHostAction(machineQueue, command.HostAction);
                if (result is not null)
                    return;

                List<string> failures = SendImmediateDataPackets(machineQueue, dataPackets.Value);
                if (failures.Count > 0)
                {
                    result = new Exception($"'{command.Description}' could not be sent to: {string.Join(", ", failures)}.");

                    // A pause still applies on the host so nothing more is sent. The boards that didn't receive it
                    // never confirm, so the pause times out and stops the queue.
                    if (command.HostAction != ImmediateHostAction.Pause)
                        return;
                }

                ApplyHostAction(machineQueue, command);
                machineQueue.CurrentMachine.ResultMessageLogger.Log($"Immediate command sent: {command.Description}.");
            });
        }

        wakeMainLoop.Set();
        return result;
    }

    /// <summary>
    /// Sends immediate data packets and returns the names of the communicators they couldn't be sent with.
    /// </summary>
    /// <param name="machineQueue">The machine queue whose communicators send the data packets.</param>
    /// <param name="dataPackets">Data packets to send.</param>
    /// <returns>Names of the communicators the data packets couldn't be sent with. Empty if every packet was sent.</returns>
    private static List<string> SendImmediateDataPackets(MachineQueue machineQueue, IEnumerable<DataPacket> dataPackets)
    {
        List<string> failures = [];
        foreach (DataPacket dataPacket in dataPackets)
        {
            if (!machineQueue.Communicators.TryGetValue(dataPacket.CommunicatorId, out ICommunicator? communicator))
                failures.Add($"communicator {dataPacket.CommunicatorId} (doesn't exist)");
            else if (!communicator.Send(dataPacket))
                failures.Add($"'{communicator.Name}'");
        }
        return failures;
    }

    /// <summary>
    /// Checks that an immediate command's host action makes sense in the machine queue's current status,
    /// for example that only a paused machine is resumed.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="hostAction">The host action to check.</param>
    /// <returns>Null if the action is allowed, otherwise the reason it isn't.</returns>
    private static Exception? ValidateHostAction(MachineQueue machineQueue, ImmediateHostAction hostAction)
    {
        MachineQueueStatus status = GetStatus(machineQueue);
        return hostAction switch
        {
            ImmediateHostAction.Pause when status != MachineQueueStatus.Running =>
                new InvalidOperationException($"Only a running machine can be paused but the machine is {status}."),
            ImmediateHostAction.Resume when status != MachineQueueStatus.Paused =>
                new InvalidOperationException($"Only a paused machine can be resumed but the machine is {status}."),
            ImmediateHostAction.ClearFaults when status is MachineQueueStatus.Running or MachineQueueStatus.Pausing or MachineQueueStatus.Paused =>
                new InvalidOperationException($"There are no faults to clear because the machine is {status}."),
            _ => null,
        };
    }

    /// <summary>
    /// Performs an immediate command's <see cref="IImmediateCommand.HostAction"/> on the host after its data packets were sent.
    /// </summary>
    /// <param name="machineQueue">The machine queue. The caller must hold its <see cref="MachineQueue.SyncRoot"/>.</param>
    /// <param name="command">The immediate command that was sent.</param>
    private void ApplyHostAction(MachineQueue machineQueue, IImmediateCommand command)
    {
        double now = HostClock.NowSeconds;
        switch (command.HostAction)
        {
            case ImmediateHostAction.Pause:
                SetStatus(machineQueue, MachineQueueStatus.Pausing, MachineQueueStatus.Pausing);
                machineQueue.PauseRequestedAt = now;
                machineQueue.BoardsYetToPause.Clear();
                machineQueue.BoardsYetToPause.UnionWith(machineQueue.BoardStates.Keys);
                if (machineQueue.BoardsYetToPause.Count <= 0)
                    FinishPause(machineQueue);
                break;

            case ImmediateHostAction.Resume:
                SetStatus(machineQueue, MachineQueueStatus.Running, MachineQueueStatus.Running);
                break;

            case ImmediateHostAction.ClearFaults:
                // The boards clear their command buffers when they clear faults.
                foreach (BoardState boardState in machineQueue.BoardStates.Values)
                    boardState.ResetQueue(now);
                SetStatus(machineQueue, MachineQueueStatus.Running, MachineQueueStatus.Running);
                break;

            case ImmediateHostAction.None:
                if (command is ILiveAdjustmentCommand liveAdjustmentCommand)
                    liveAdjustmentCommand.ApplyTo(machineQueue.LiveAdjustments);
                break;
        }
    }

    /// <summary>
    /// Sends a <see cref="PauseCommand"/>.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? Pause(uint machineQueueId) => SendImmediate(machineQueueId, new PauseCommand());

    /// <summary>
    /// Sends a <see cref="ResumeCommand"/>.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? Resume(uint machineQueueId) => SendImmediate(machineQueueId, new ResumeCommand());

    /// <summary>
    /// Sends a <see cref="ClearFaultsCommand"/>.
    /// </summary>
    /// <param name="machineQueueId">Id of the machine queue.</param>
    /// <returns>Null if successful, otherwise the error.</returns>
    public Exception? ClearFaults(uint machineQueueId) => SendImmediate(machineQueueId, new ClearFaultsCommand());

    /// <summary>
    /// Returns the status of a machine queue, or null if it doesn't exist.
    /// </summary>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <returns>The status, or null if the machine queue doesn't exist.</returns>
    public MachineQueueStatus? GetMachineQueueStatus(uint machineId)
    {
        if (!MachineQueues.TryGetValue(machineId, out MachineQueue? queue))
            return null;

        lock (queue.SyncRoot)
            return GetStatus(queue);
    }

    /// <summary>
    /// Returns the latest <see cref="Machine"/> based on it's ID.
    /// Returns null if it cannot be found.
    /// </summary>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <returns>The machine after the last resolved command, or null if the machine queue doesn't exist.</returns>
    public Machine? GetCurrentMachine(uint machineId)
    {
        bool success = MachineQueues.TryGetValue(machineId, out MachineQueue? queue);

        if (!success || queue is null)
            return null;

        return queue.CurrentMachine;
    }

    /// <summary>
    /// Returns the machine that will result once every queued command has run, or null if the machine doesn't exist.
    /// </summary>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <returns>The machine after the last queued command, or null if the machine queue doesn't exist.</returns>
    public Machine? GetLatestQueuedMachine(uint machineId)
    {
        if (!MachineQueues.TryGetValue(machineId, out MachineQueue? queue))
            return null;

        return queue.LatestQueuedMachine;
    }

    /// <summary>
    /// Returns the live adjustments (such as babystep offsets) of a machine, or null if the machine doesn't exist.
    /// </summary>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <returns>The live adjustments, or null if the machine queue doesn't exist.</returns>
    public LiveAdjustments? GetLiveAdjustments(uint machineId)
    {
        if (!MachineQueues.TryGetValue(machineId, out MachineQueue? queue))
            return null;

        return queue.LiveAdjustments;
    }
}
