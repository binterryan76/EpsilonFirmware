using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Commands.MotionCommands;
using EpsilonCore.Communication;
using EpsilonCore.Display;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using GenericHelpers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EpsilonCore.Engine;

/// <summary>
/// This contains the queues for all machines and runs the main loop.
/// The main loop receives commands, sends those commands to microcontrollers, and receives responses from the microcontrollers.
/// Start this process by adding a machine with its machine machineQueue by calling <see cref="AddMachineQueue"/>
/// which will start a new thread to run the main loop.
/// You can machineQueue commands with <see cref="EnqueueCommand"/> or <see cref="EnqueueCommands"/> and they will be processed in the main loop.
/// 
/// This main loop needs to be a forever loop since a connection will be maintained with every microcontroller
/// and pings will be sent back and forth to ensure the connection is still alive.
/// If there are no commands to process, the main loop will sleep between periodic checks for new commands to process.
/// The main loop needs to prioritize sending commands to ensure all the microconroller's command buffers are filled.
/// With extra resources, the main loop can queue commands to machineQueues.
/// Recieving data from the microcontrollers is handled with a relatively quick event handler.
/// 
/// Remember that each command can contain multiple data packets and each data packet can be sent to a different microcontroller.
/// This means that we need to ensure that each involved microcontroller has enough room in their command buffers before sending
/// any all the data packets for a command all at once. The microcontroller assumes that a command is good to run once it receives the data packets
/// although some command will contain a specific time they need to run.
/// 
/// The main loop logic works something like this:
/// See if there are any data packets that can be sent to the microcontrollers. If so, send them until
/// there is no data left to send or any command buffer on the microcontrollers doesn't have enough room to receive the data for the given command.
/// 
/// The interrupt for receiving data packets from microcontrollers works like this:
/// If a command is completed, get the sequence number for the last data packet for that command
/// TODO: we cannot determine what command completed because we dont store the sequnce number
/// Solution, We do need to store the sequence number.
/// 
/// Idea: every command should be assumed to have completed properly and if an error occurs, the microcontroller will send an error message.
/// Other than that, the microcontroller will indicate if the command buffer has more room freed up so the main loop can send more data packets.
/// This means we need a BUFFER_BYTES_AVAILABLE message.
/// 
/// 
/// 
/// </summary>
public class EpsilonEngine(IResultMessageLogger logger)
{
    // Give the mutex a unique name (prefix with "Global\\" to make it OS-wide)
    //private static readonly Mutex mainLoopMutex = new();

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

    /// <summary>
    /// Raised when <see cref="AddMachineQueue"/> is called.
    /// The event argument is the id of the <see cref="MachineQueue"/> added.
    /// The sender object will be this <see cref="EpsilonEngine"/>.
    /// </summary>
    public EventHandler<uint>? MachineQueueAdded;

    /// <summary>
    /// Creates a new machine machineQueue for the given machine.
    /// There is exactly one machine machineQueue per machine.
    /// </summary>
    /// <param name="machine"></param>
    public void AddMachineQueue(Machine machine)
    {
        //TODO: Epsilon engine should make the Machine object and commands should be used to manipulate them.
        MachineQueue machineQueue = new(machine);
        uint id = MachineQueues.NextId();
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
    /// Adds an <see cref="ICommunicator"/> to a <see cref="MachineQueue"/>.
    /// This is done at the engine level because <see cref="ICommunicator"/>s are mutable and <see cref="Machine"/>s 
    /// are immutable so it's more correct to not have a mutable object inside an immutable one.
    /// Returns true if successful or false if there was an error.
    /// </summary>
    /// <param name="machineQueueId"></param>
    /// <param name="communicator"></param>
    /// <returns></returns>
    public Exception? AddCommunicator(uint machineQueueId, ICommunicator communicator)
    {
        bool success = MachineQueues.TryGetValue(machineQueueId, out MachineQueue? machineQueue);

        if (!success || machineQueue is null)
        {
            string message = $"Communicator '{communicator.Name}' could not be added because {nameof(MachineQueue)} {machineQueueId} doesn't exist.";
            Debug.WriteLine(message);
            return new ArgumentException(message);
        }

        uint nextBoardQueueId = machineQueue.Communicators.NextId();
        communicator.Id = nextBoardQueueId;

        success = machineQueue.Communicators.TryAdd(nextBoardQueueId, communicator);
        if (!success)
        {
            string message = $"Communicator '{communicator.Name}' could not be added due to a multithreading error.";
            Debug.WriteLine(message);
            return new ArgumentException(message);
        }

        communicator.DataPacketsReceivedHandler += (sender, dataPackets) =>
        {
            Debug.Assert(sender is not null);
            foreach (DataPacket dataPacket in dataPackets)
                ProcessDataPacket((ICommunicator)sender, dataPacket);
        };

        success = communicator.Connect();
        return success ? new Exception($"Could not connect to Communicator '{communicator.Name}'.") : null;
    }

    /// <summary>
    /// Starts the main loop to process commands.
    /// </summary>
    private void Start()
    {
        mainThread = new Thread(MainLoop);
        mainThread.Start();
        Running = true;
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

        mainThread?.Join();
        mainThread = null;
    }

    /// <summary>
    /// Adds a command to the command machineQueue to be processed.
    /// </summary>
    /// <param name="machineQueueIndex"></param>
    /// <param name="command"></param>
    public void EnqueueCommand(uint machineQueueIndex, ICommand command)
    {
        bool success = MachineQueues.TryGetValue(machineQueueIndex, out MachineQueue? queue);
        if (success)
            queue!.CommandsToEnqueue.Enqueue(command);
    }

    /// <summary>
    /// Adds a list of commands to the command machineQueue to be processed.
    /// </summary>
    /// <param name="machineQueueIndex"></param>
    /// <param name="commands"></param>
    public void EnqueueCommands(uint machineQueueIndex, IEnumerable<ICommand> commands)
    {
        bool success = MachineQueues.TryGetValue(machineQueueIndex, out MachineQueue? queue);
        if (!success)
            return;

        foreach (ICommand command in commands)
            queue!.CommandsToEnqueue.Enqueue(command);
    }

    /// <summary>
    /// This is what the mainThread runs.
    /// continually
    /// </summary>
    private void MainLoop()
    {
        while (Running)
        {
            //mainLoopMutex.WaitOne();
            foreach (MachineQueue machineQueue in MachineQueues.Values)
            {
                // skip stopped queues
                if (machineQueue.SendStatus != MachineQueue.MachineQueueStatus.Running)
                    continue;

                EnqueueNextCommand(machineQueue);
                SendSomeQueuedCommands(machineQueue);
            }
            //mainLoopMutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Waits until all commands are queued, sent, and completed.
    /// Returns true if successful or false if there was an error.
    /// </summary>
    /// <param name="machineQueueId"></param>
    /// <returns></returns>
    public bool CompleteAllCommands(uint machineQueueId)
    {
        if (!Running)
            return false;

        bool success = MachineQueues.TryGetValue(machineQueueId, out MachineQueue? queueToClear);

        if (!success || queueToClear is null || queueToClear.EnqueueStatus != MachineQueue.MachineQueueStatus.Running)
            return false;

        while (true)
        {
            // Stop if there is an error.
            if (queueToClear.EnqueueStatus != MachineQueue.MachineQueueStatus.Running ||
                queueToClear.SendStatus != MachineQueue.MachineQueueStatus.Running)
                break;

            // Stop if all queues are empty.
            if (queueToClear.CommandsToEnqueue.IsEmpty &&
                queueToClear.Queued.Count <= 0 &&
                queueToClear.ReadyToSend.Count <= 0 &&
                queueToClear.Sent.Count <= 0)
                break;

            Thread.Sleep(10);
        }

        return queueToClear.EnqueueStatus == MachineQueue.MachineQueueStatus.Running &&
            queueToClear.SendStatus == MachineQueue.MachineQueueStatus.Running;
        //mainLoopMutex.WaitOne();

        // call this once to machineQueue all commands
        // EnqueueAllCommands(queueToClear);

        //bool finished = false;
        //
        //while (!finished)
        //{
        //    finished = true;
        //    if (queueToClear.Queued.Count > 0)
        //    {
        //        if (queueToClear.SendStatus != MachineQueue.MachineQueueStatus.Running)
        //        {
        //            //mainLoopMutex.ReleaseMutex();
        //            return false;
        //        }
        //
        //        SendSomeQueuedCommands(queueToClear);
        //        finished = false;
        //    }
        //
        //    if (queueToClear.Sent.Count > 0)
        //    {
        //        ReceiveDataFromBoards(queueToClear);
        //        finished = false;
        //    }
        //}
        //
        ////mainLoopMutex.ReleaseMutex();
        //return true;
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
    /// This will also solve the <see cref="MoveQueue"/> when either enough 
    /// </summary>
    /// <param name="machineQueue"></param>
    /// <exception cref="Exception">Thrown if a successfully queued command has a null <see cref="QueuedCommand.ResultantMachine"/>.</exception>
    private void EnqueueNextCommand(MachineQueue machineQueue)
    {
        if (machineQueue.EnqueueStatus != MachineQueue.MachineQueueStatus.Running)
            return;

        if (machineQueue.CommandsToEnqueue.IsEmpty)
        {
            if (machineQueue.Queued.Count <= 0)
                return;

            SolveMovesAndMarkCommandsAsReadyToSend(machineQueue);
            return;
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
            machineQueue.EnqueueStatus = MachineQueue.MachineQueueStatus.Stopped;
            return;
        }

        Debug.Assert(queuedCommand.ResultantMachine is not null,
            "All success results require a non-null ResultantMachine property.");

        machineQueue.LatestQueuedMachine = queuedCommand.ResultantMachine;

        // If a non-move command comes in and there is nothing before it then just mark it as ready to send.
        if (machineQueue.Queued.Count <= 0 && command is not MoveCommand)
        {
            machineQueue.AddCommandToReadyToSend(queuedCommand);
            return;
        }

        // Otherwise, mark it as only queued but not ready to send.
        machineQueue.Queued.Enqueue(queuedCommand);
        CommandQueued?.Invoke(initialMachine, queuedCommand);

        if (command is MoveCommand moveCommand)
        {
            Result<Move> move = moveCommand.GetMove(initialMachine);
            if (move.IsError)
            {
                // Log error.
                initialMachine.ResultMessageLogger.Log(move.Exception.Message);

                // Don't send any more commands to this machineQueue until error is addressed.
                machineQueue.CommandsToEnqueue.Clear();
                machineQueue.EnqueueStatus = MachineQueue.MachineQueueStatus.Stopped;

                // Go ahead and solve and send all the moves ahead of this command that did work.
                SolveMovesAndMarkCommandsAsReadyToSend(machineQueue);
                return;
            }
            machineQueue.MoveQueue.Add(move.Value);
        }

        const int LOOK_AHEAD_COUNT = 20;

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
    }

    /// <summary>
    /// Solves the <see cref="MoveQueue"/> and moves all commands from <see cref="MachineQueue.Queued"/>
    /// to <see cref="MachineQueue.ReadyToSend"/>.
    /// </summary>
    /// <param name="machineQueue"></param>
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
    /// <param name="machineQueue"></param>
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
    /// Sends a set of <see cref="DataPacket"/>s via an <see cref="ICommunicator"/> and
    /// moves commands and their output machines from <see cref="MachineQueue.ReadyToSend"/> to <see cref="MachineQueue.Sent"/>.
    /// Sends <see cref="DataPacket"/>s until either an error is encountered or the microcontroller's 
    /// internal machineQueue doesn't have enough space for the next command.
    /// </summary>
    /// <param name="machineQueue"></param>
    private void SendSomeQueuedCommands(MachineQueue machineQueue)
    {
        while (machineQueue.SendStatus == MachineQueue.MachineQueueStatus.Running &&
            machineQueue.ReadyToSend.Count > 0)
        {
            QueuedCommand nextCommand = machineQueue.ReadyToSend.Peek();

            Debug.Assert(nextCommand.ErrorLevel == ErrorLevel.Warning ||
                nextCommand.ErrorLevel == ErrorLevel.Success,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.ReadyToSend)} should only contain {nameof(QueuedCommand)}s with a status of either {ErrorLevel.Warning.ToDisplayStr()} or {ErrorLevel.Success.ToDisplayStr()}.");

            Debug.Assert(nextCommand.ResultantMachine is not null,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.ReadyToSend)} should never contain a null {nameof(QueuedCommand)}.{nameof(QueuedCommand.ResultantMachine)}.");

            // Early continue if there is no data to send
            if (nextCommand.DataPackets.Count <= 0)
            {
                // Actually remove the command from the ready to send machineQueue
                machineQueue.ReadyToSend.Dequeue();

                if (machineQueue.Sent.Count <= 0)
                {
                    // Just skip the sent list entirely and just apply the resultant machine if
                    // there is no data to send and no commands in front of the current one.
                    machineQueue.CurrentMachine = nextCommand.ResultantMachine;

                    // Raise event for command resolved.
                    CommandResolved?.Invoke(nextCommand.ResultantMachine, nextCommand);
                }
                else
                {
                    // We can't apply the resultant machine until the earlier sent commands are completed.
                    machineQueue.Sent.Enqueue(nextCommand);
                }

                continue;
            }

            foreach (DataPacket dataPacket in machineQueue.DataPacketsReadyToSend)
            {
                uint communicatorId = dataPacket.CommunicatorId;
                bool success = machineQueue.Communicators.TryGetValue(communicatorId, out ICommunicator? communicatorToSendWith);
                int dataPacketSize = dataPacket.TotalLength;

                Debug.Assert(success && communicatorToSendWith is not null,
                    $"{nameof(ICommunicator)} {communicatorId} could not be found but should have been verified to exist when the command was queued.");

                if (communicatorToSendWith.BoardCommandBufferBytesFree < dataPacketSize)
                {
                    // No more room in microcontroller's buffer to send data packet, just
                    // bail without dequeueing the command from machineQueue.ReadyToSend or
                    // dequeueing data packet from machineQueue.DataPacketsReadyToSend.
                    return;
                }

                // Only assign the sequence number right before sending data packet because if a
                // command is cancelled, we don't want to increment the sequence number.
                DataPacket dataPacketWithSequenceNum = dataPacket.SetSequenceNumber(
                    communicatorToSendWith.IncrementAndReturnNextSequenceNumber());

                // Actually send data packet via the ICommunicator.
                bool sendSuccess = communicatorToSendWith.Send(dataPacketWithSequenceNum);

                IResultMessageLogger logger = nextCommand.ResultantMachine.ResultMessageLogger;
                if (sendSuccess)
                {
                    // track how much space the sent command is taking up in the microcontroller's command machineQueue
                    communicatorToSendWith.BoardCommandBufferBytesOccupied += (uint)dataPacketSize;

                    // Actually remove the data packet from the ready to send list.
                    machineQueue.DataPacketsReadyToSend.Dequeue();

                    // Actually move the command from machineQueue.ReadyToSend to machineQueue.Sent if
                    // every data packet associated with it has been sent successfully.
                    // NOTE: The command that is moved will not have any of the sequence numbers assigned to the data packets
                    // because a modified copy of the data packet is sent to the microcontroller but the data packet in the command's 
                    // data packet list is not modified. I don't think we will need to reference the sequence number later so this is ok.
                    if (machineQueue.DataPacketsReadyToSend.Count <= 0)
                    {
                        machineQueue.ReadyToSend.Dequeue();
                        machineQueue.Sent.Enqueue(nextCommand);
                        CommandSent?.Invoke(nextCommand.InitialMachine, nextCommand);
                    }
                }
                else
                {
                    // TODO: Raise event for command send failure.

                    // Don't remove the command from machineQueue.ReadyToSend or
                    // the data packet from machineQueue.DataPacketsReadyToSend.

                    // log fail
                    logger.Log(
                        Helper.GetFormattedDisplayMessage(
                            nextCommand.Command,
                            ErrorLevel.Error,
                            $"Failed to send data packet using communicator '{communicatorToSendWith.Name}'"));

                    // stop machineQueue
                    machineQueue.SendStatus = MachineQueue.MachineQueueStatus.Stopped;
                }
            }
        }
    }

    private void ProcessDataPacket(ICommunicator communicator, DataPacket dataPacket)
    {
        bool success = MachineQueues.TryGetValue(communicator.MachineId, out MachineQueue? machineQueue);
        if (!success || machineQueue is null)
        {
            EngineLogger.Log($"Data packet received from communicator '{communicator.Name}' but MachineQueue {communicator.MachineId} doesn't exist.");
            return;
        }
        Machine machine = machineQueue.CurrentMachine;
        IResultMessageLogger logger = machine.ResultMessageLogger;

        success = machine.Entities.Boards.TryGetValue(communicator.BoardId, out Board? board);
        string dataPacketSource;
        if (success && board is not null)
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

        // TODO: add validation on data length for each command code type
        switch (commandCode.Value)
        {
            case CommandCode.DISPLAY_TEXT:
                {
                    string message = System.Text.Encoding.ASCII.GetString(dataPacket.AllData.ToArray()[3..]);
                    logger.Log($"Message from {dataPacketSource}:\n\t{message}.");
                    break;
                }
            case CommandCode.COMMAND_COMPLETE:
                {
                    byte sequenceNum = dataPacket.SequenceNumber;

                    if (machineQueue.Sent.Count <= 0)
                    {
                        logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but there were no commands in MachineQueue.Sent.");
                        return;
                    }

                    // remove command from sent machineQueue
                    // TODO: for now just assume that there is only one ICommunicator and Board per machine and so one commands should complete in the order they were sent. Add support for multiple ICommunicators later.
                    // TODO: IMPORTANT: only remove the command from the queue and reduce the space occupied in the buffer(s) if all data packets of the command are complete. 
                    QueuedCommand sentCommandInMachineQueue = machineQueue.Sent.Dequeue();

                    Debug.Assert(sentCommandInMachineQueue.ResultantMachine is not null,
                        $"A sent command should never have a null {nameof(QueuedCommand.ResultantMachine)}.");

                    // Apply resultant machine.
                    machineQueue.CurrentMachine = sentCommandInMachineQueue.ResultantMachine;

                    // Commands with data packets should have had data sent to a board via an
                    // ICommunicator and thus should be the next item in the BoardQueue.
                    if (sentCommandInMachineQueue.DataPackets is not null)
                    {
                        if (machineQueue.Sent.Count <= 0)
                        {
                            logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but command {sequenceNum} wasn't in the sent MachineQueue.");
                            return;
                        }

                        if (machineQueue.Sent.Peek() != sentCommandInMachineQueue)
                        {
                            logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but that command wasn't the next command in the sent MachineQueue.");
                            return;
                        }

                        // remove the command from the BoardQueue and reduce the buffer bytes occupied
                        // by the data packet length now that the microcontroller is done with that data packet.
                        foreach (DataPacket resolvedCommandDataPacket in sentCommandInMachineQueue.DataPackets)
                        {
                            success = machineQueue.Communicators.TryGetValue(resolvedCommandDataPacket.CommunicatorId, out ICommunicator? dataPacketsCommunicator);
                            if (!success || dataPacketsCommunicator is null)
                            {
                                logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but the communicator {resolvedCommandDataPacket.CommunicatorId} for that command's data packet could not be found.");
                                continue;
                            }
                            dataPacketsCommunicator.BoardCommandBufferBytesOccupied -= (uint)resolvedCommandDataPacket.TotalLength;
                        }

                        if (sentCommandInMachineQueue.DataPackets.Count > 1)
                            throw new NotImplementedException("Currently commands can only have one data packet. Support for multiple data packets per command is not yet implemented.");

                        CommandResolved?.Invoke(sentCommandInMachineQueue.ResultantMachine, sentCommandInMachineQueue);
                    }

                    break;
                }
            case CommandCode.ERROR:
                {
                    // log errors from microcontroller
                    // TODO: require board restart?
                    byte[] allData = [.. dataPacket.AllData];
                    Result<BoardErrorCode> errorCode = allData[3].ToBoardErrorCode();
                    if (errorCode.IsError)
                    {
                        logger.Log($"Unrecognized error with command {dataPacket.SequenceNumber} on {dataPacketSource}.");
                        logger.Log(errorCode.Exception.Message);
                        return;
                    }
                    UInt32 lineNumber = BitConverter.ToUInt32(allData, 4);
                    string fileName = BitConverter.ToString(allData, 4 + sizeof(UInt32));
                    logger.Log($"Error with command {dataPacket.SequenceNumber} on {dataPacketSource}:\n\t{fileName} line {lineNumber}\n\t{errorCode.Value.DisplayMessage()}");

                    break;
                }
        }
    }

    /// <summary>
    /// Returns the latest <see cref="Machine"/> based on it's ID.
    /// Returns null if it cannot be found.
    /// </summary>
    /// <param name="machineId"></param>
    /// <returns></returns>
    public Machine? GetCurrentMachine(uint machineId)
    {
        bool success = MachineQueues.TryGetValue(machineId, out MachineQueue? queue);

        if (!success || queue is null)
            return null;

        return queue.CurrentMachine;
    }
}
