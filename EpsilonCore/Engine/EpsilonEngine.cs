using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Communication;
using EpsilonCore.Display;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EpsilonCore.Engine;

/// <summary>
/// This contains the queues for all machines and runs the main loop to process commands.
/// Start this process by adding a machine with its machine machineQueue by calling <see cref="AddMachineQueue"/>.
/// </summary>
public class EpsilonEngine
{
    // Give the mutex a unique name (prefix with "Global\\" to make it OS-wide)
    private static readonly Mutex mainLoopMutex = new();

    /// <summary>
    /// Max number of commands that can be in the send machineQueue at a time.
    /// </summary>
    private const uint SEND_QUEUE_MAX = byte.MaxValue;
    private ConcurrentDictionary<uint, MachineQueue> MachineQueues { get; } = [];

    /// <summary>
    /// Indicates if the main loop is running.
    /// False until Start is called.
    /// </summary>
    public bool Running { get; private set; } = false;
    private Thread? mainThread;

    /// <summary>
    /// Creates a new machine machineQueue for the given machine.
    /// There is exactly one machine machineQueue per machine.
    /// </summary>
    /// <param name="machine"></param>
    public void AddMachineQueue(Machine machine)
    {
        //TODO: Epsilon engine should make the Machine object and commands should be used to manipulate them.
        bool success = MachineQueues.TryAdd((uint)MachineQueues.Count, new MachineQueue(machine));

        if (success)
            machine.ResultMessageLogger.Log($"New machine added: '{machine.Name}'.");
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
    /// <param name="name"></param>
    /// <param name="baudRate"></param>
    /// <param name="portNameIncludeString"></param>
    /// <returns></returns>
    public bool AddUartCommunicator(uint machineQueueId, string name, uint baudRate, string portNameIncludeString)
    {
        bool success = MachineQueues.TryGetValue(machineQueueId, out MachineQueue? machineQueue);

        if (!success || machineQueue is null)
        {
            Debug.WriteLine($"Communicator '{name}' could not be added because {nameof(MachineQueue)} {machineQueueId} doesn't exist.");
            return false;
        }

        UartCommunicator communicator = new(name, baudRate, portNameIncludeString);
        uint nextBoardQueueId = machineQueue.BoardQueues.NextId();
        communicator.Id = nextBoardQueueId;
        BoardQueue boardQueueToAdd = new(communicator);
        mainLoopMutex.WaitOne();
        machineQueue.BoardQueues = machineQueue.BoardQueues.Add(nextBoardQueueId, boardQueueToAdd);
        success = communicator.Connect();
        mainLoopMutex.ReleaseMutex();
        return success;
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
    /// This is what the mainThread runs.
    /// continually
    /// </summary>
    private void MainLoop()
    {
        while (Running)
        {
            mainLoopMutex.WaitOne();
            foreach (MachineQueue machineQueue in MachineQueues.Values)
            {
                // skip stopped queues
                if (machineQueue.SendStatus != MachineQueue.MachineQueueStatus.Running)
                    continue;

                //foreach (BoardQueue boardQueue in machineQueue.BoardQueues.Values)
                //{
                //
                //}
                EnqueueAllCommands(machineQueue);
                SendSomeQueuedCommands(machineQueue);
                ReceiveDataFromBoards(machineQueue);
            }
            mainLoopMutex.ReleaseMutex();
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

        mainLoopMutex.WaitOne();

        // call this once to queue all commands
        EnqueueAllCommands(queueToClear);

        bool finished = false;

        while (!finished)
        {
            finished = true;
            if (queueToClear.Queued.Count > 0)
            {
                if (queueToClear.SendStatus != MachineQueue.MachineQueueStatus.Running)
                {
                    mainLoopMutex.ReleaseMutex();
                    return false;
                }

                SendSomeQueuedCommands(queueToClear);
                finished = false;
            }

            if (queueToClear.Sent.Count > 0)
            {
                ReceiveDataFromBoards(queueToClear);
                finished = false;
            }
        }

        mainLoopMutex.ReleaseMutex();
        return true;
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
    /// Moves all <see cref="ICommand"/>s from <see cref="MachineQueue.CommandsToEnqueue"/> to <see cref="MachineQueue.Queued"/>.
    /// Gets the output machine for incoming commands from <see cref="MachineQueue.CommandsToEnqueue"/> 
    /// by calling <see cref="ICommand.EnqueueCommandSpecific(Machine)"/>.
    /// Adds the commands and their output machines to <see cref="MachineQueue.Queued"/>.
    /// </summary>
    /// <param name="machineQueue"></param>
    /// <exception cref="Exception">Thrown if a successfully queued command has a null <see cref="QueuedCommand.ResultantMachine"/>.</exception>
    private static void EnqueueAllCommands(MachineQueue machineQueue)
    {
        while (!machineQueue.CommandsToEnqueue.IsEmpty &&
            machineQueue.EnqueueStatus == MachineQueue.MachineQueueStatus.Running)
        {
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

                // Log error
                initialMachine.ResultMessageLogger.Log(Helper.GetFormattedDisplayMessage(queuedCommand));

                // don't send any more commands to this machineQueue until error is addressed
                machineQueue.CommandsToEnqueue.Clear();
                machineQueue.EnqueueStatus = MachineQueue.MachineQueueStatus.Stopped;
                return;
            }

            Debug.Assert(queuedCommand.ResultantMachine is not null,
                "All success results require a non-null ResultantMachine property.");

            if (queuedCommand.DataPacket is null)
            {
                // no data to send so just add it to queued commands and continue
                // Note: we don't need to send it to a BoardQueue because if there is no data to send, it isn't board specific.
                machineQueue.Queued.Enqueue(queuedCommand);
                machineQueue.LatestQueuedMachine = queuedCommand.ResultantMachine;

                continue;
            }

            success = machineQueue.BoardQueues.TryGetValue(queuedCommand.DataPacket.CommunicatorId, out BoardQueue? boardQueue);

            // Cause error if BoardQueue doesn't exist
            if (!success || boardQueue is null)
            {
                queuedCommand = queuedCommand with
                {
                    ErrorLevel = ErrorLevel.Error,
                    DisplayMessage = $"{nameof(BoardQueue)} {queuedCommand.DataPacket.CommunicatorId} is needed to send the data packet but doesn't exist"
                };

                // TODO: raise event for failed machineQueue event

                // log error
                initialMachine.ResultMessageLogger.Log(Helper.GetFormattedDisplayMessage(queuedCommand));

                // don't send any more commands to this machineQueue until error is addressed
                machineQueue.CommandsToEnqueue.Clear();
                machineQueue.EnqueueStatus = MachineQueue.MachineQueueStatus.Stopped;
                return;
            }

            // everything was successful so we can add it to the queued commands for both the BoardQueue and MachineQueue
            machineQueue.Queued.Enqueue(queuedCommand);
            machineQueue.LatestQueuedMachine = queuedCommand.ResultantMachine;
            // TODO: eventually add the ability for there to be multiple DataPackets for a single command and send each packet to the corresponding board.
            boardQueue.Queued.Enqueue(queuedCommand);
        }
    }

    /// <summary>
    /// Sends a set of <see cref="DataPacket"/>s via an <see cref="ICommunicator"/> and
    /// moves commands and their output machines from <see cref="MachineQueue.Queued"/> to <see cref="MachineQueue.Sent"/>.
    /// Sends <see cref="DataPacket"/>s until either <see cref="SEND_QUEUE_MAX"/> is reached or an error is encountered or
    /// the microcontroller's internal machineQueue doesn't have enough space for the next command.
    /// </summary>
    /// <param name="machineQueue"></param>
    private static void SendSomeQueuedCommands(MachineQueue machineQueue)
    {
        // TODO: fix this loop because sending data should probably be async?
        while (machineQueue.SendStatus == MachineQueue.MachineQueueStatus.Running &&
            machineQueue.Queued.Count > 0)
        {
            QueuedCommand nextCommand = machineQueue.Queued.Peek();

            Debug.Assert(nextCommand.ErrorLevel == ErrorLevel.Warning ||
                nextCommand.ErrorLevel == ErrorLevel.Success,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.Queued)} should only contain {nameof(QueuedCommand)}s with a status of either {ErrorLevel.Warning.ToDisplayStr()} or {ErrorLevel.Success.ToDisplayStr()}.");

            Debug.Assert(nextCommand.ResultantMachine is not null,
                $"{nameof(MachineQueue)}.{nameof(MachineQueue.Queued)} should never contain a null {nameof(QueuedCommand)}.{nameof(QueuedCommand.ResultantMachine)}.");

            // Early continue if there is no data to send
            if (nextCommand.DataPacket is null)
            {
                // Actually remove the command from the queued queue
                machineQueue.Queued.Dequeue();

                if (machineQueue.Sent.Count <= 0)
                {
                    // Just skip the sent list entirely and just apply the resultant machine if
                    // there is no data to send and no commands in front of the current one.
                    machineQueue.CurrentMachine = nextCommand.ResultantMachine;
                }
                else
                {
                    // We can't apply the resultant machine until the earlier sent commands are completed.
                    machineQueue.Sent.Enqueue(nextCommand);
                }

                continue;
            }

            uint boardQueueId = nextCommand.DataPacket.CommunicatorId;
            bool success = machineQueue.BoardQueues.TryGetValue(boardQueueId, out BoardQueue? boardQueue);
            int dataPacketSize = nextCommand.DataPacket.TotalLength;

            Debug.Assert(success && boardQueue is not null,
                $"{nameof(BoardQueue)} {boardQueueId} could not be found but should have been verified to exist when the command was queued.");

            if (boardQueue.BoardCommandBufferBytesFree < dataPacketSize)
            {
                // no more room in microcontroller's buffer to send data packet, just bail without dequeueing the command
                return;
            }

            // Actually remove the command from the queued queue
            machineQueue.Queued.Dequeue();

            ICommunicator communicatorToSendWith = boardQueue.Communicator;

            // only assign the sequence number right before sending data packet because if a
            // command is cancelled, we don't want to increment the sequence number
            QueuedCommand commandToSend = nextCommand with
            {
                DataPacket = nextCommand.DataPacket with
                {
                    AllData = nextCommand.DataPacket.AllData.SetItem(
                        DataPacket.SEQUENCE_NUM_INDEX,
                        communicatorToSendWith.IncrementAndReturnNextSequenceNumber())
                }
            };

            // actually send data packet via the ICommunicator
            bool sendSuccess = communicatorToSendWith.Send(commandToSend.DataPacket);

            IResultMessageLogger logger = nextCommand.ResultantMachine.ResultMessageLogger;
            if (sendSuccess)
            {
                // track how much space the sent command is taking up in the microcontroller's command machineQueue
                boardQueue.BoardCommandBufferBytesOccupied += dataPacketSize;

                // log success
                logger.Log(
                    Helper.GetFormattedDisplayMessage(
                        commandToSend.Command,
                        ErrorLevel.Success,
                        "Command sent"));

                // move the command to the Sent queue for both the MachineQueue and BoardQueue.
                machineQueue.Sent.Enqueue(commandToSend);
                boardQueue.Sent.Enqueue(commandToSend);
            }
            else
            {
                // TODO: Raise event for command send failure.

                // log fail
                logger.Log(
                    Helper.GetFormattedDisplayMessage(
                        commandToSend.Command,
                        ErrorLevel.Error,
                        $"Failed to send data packet using communicator '{communicatorToSendWith.Name}'"));

                // stop machineQueue
                machineQueue.SendStatus = MachineQueue.MachineQueueStatus.Stopped;
            }
        }
    }

    private static void ReceiveDataFromBoards(MachineQueue machineQueue)
    {
        // Get list of BoardQueues to iterate through
        List<BoardQueue> boardQueues = [.. machineQueue.BoardQueues.Values];
        List<BoardQueue> boardQueuesToRemove = [];
        Machine machine = machineQueue.CurrentMachine;

        while (boardQueues.Count > 0)
        {
            foreach (BoardQueue boardQueue in boardQueues)
            {
                ICommunicator communicator = boardQueue.Communicator;

                DataPacket? dataPacket = communicator.Read();

                if (dataPacket is null)
                {
                    // stop looping through boards that don't have data ready
                    boardQueuesToRemove.Add(boardQueue);
                    continue;
                }

                Result<CommandCode> commandCode = dataPacket.CommandCode.ToCommandCode();

                IResultMessageLogger logger = machine.ResultMessageLogger;

                // If unrecognized command code is received, log and continue
                if (commandCode.IsError)
                {
                    logger.Log($"Unregocnized command code received: {dataPacket.CommandCode}.");
                    logger.Log(commandCode.Exception.Message);
                    continue;
                }

                Result<Board> board = machine.Entities.Boards.Values.TryFirst(b => b.CommunicatorId == communicator.Id);
                string dataPacketSource;
                if (board.IsSuccess)
                    dataPacketSource = $"{machine.Name} (board {board.Value.Id})";
                else
                    dataPacketSource = $"{machine.Name} (communicator {communicator.Id})";

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
                                logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but command {sequenceNum} wasn't in the sent MachineQueue.");
                                continue;
                            }

                            // remove command from sent queue
                            QueuedCommand sentCommandInMachineQueue = machineQueue.Sent.Dequeue();

                            Debug.Assert(sentCommandInMachineQueue.ResultantMachine is not null,
                                $"A sent command should never have a null {nameof(QueuedCommand.ResultantMachine)}.");

                            // Apply resultant machine.
                            machineQueue.CurrentMachine = sentCommandInMachineQueue.ResultantMachine;

                            // Commands with data packets should have had data sent to a board via an
                            // ICommunicator and thus should be the next item in the BoardQueue.
                            if (sentCommandInMachineQueue.DataPacket is not null)
                            {
                                if (boardQueue.Sent.Count <= 0)
                                {
                                    logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but command {sequenceNum} wasn't in the sent BoardQueue.");
                                    return;
                                }

                                if (boardQueue.Sent.Peek() != sentCommandInMachineQueue)
                                {
                                    logger.Log($"Warning: Command {sequenceNum} completed on {dataPacketSource} but that command wasn't the next command in BoardQueue.");
                                    return;
                                }

                                // remove the command from the BoardQueue and reduce the buffer bytes occupied
                                // by the data packet length now that the microcontroller is done with that data packet.
                                boardQueue.Sent.Dequeue();
                                boardQueue.BoardCommandBufferBytesOccupied -= sentCommandInMachineQueue.DataPacket.TotalLength;
                            }

                            // log successful command completed
                            logger.Log($"Command {dataPacket.SequenceNumber} completed on {dataPacketSource}.");
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
            foreach (BoardQueue boardQueueToRemove in boardQueuesToRemove)
            {
                boardQueues.Remove(boardQueueToRemove);
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
