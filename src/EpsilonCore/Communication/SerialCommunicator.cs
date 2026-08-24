using EpsilonCore.Helpers; using GenericHelpers;
using EpsilonCore.Machines;
using System.Diagnostics;
using System.IO.Ports;

namespace EpsilonCore.Communication;

/// <summary>
/// Communicator for a microcontroller board using a <see cref="SerialPort"/>.
/// </summary>
public class SerialCommunicator : ICommunicator
{
    /// <summary>
    /// Creates a new instance of <see cref="SerialCommunicator"/>.
    /// </summary>
    /// <param name="name"><inheritdoc cref="IEntity.Name"/></param>
    /// <param name="baudRate">Baud rate of the UART connection.</param>
    /// <param name="portNameIncludeString">Name of the COM port.</param>
    /// <param name="boardCommandBufferSize">The size of the microcontroller's command buffer in bytes.</param>
    /// <param name="machineId"><inheritdoc cref="ICommunicator.MachineId"/></param>
    internal SerialCommunicator(
        string name,
        uint baudRate,
        string portNameIncludeString,
        uint boardCommandBufferSize,
        uint machineId)
    {
        Name = name;
        BaudRate = baudRate;
        this.portNameIncludeString = portNameIncludeString;
        BoardCommandBufferSize = boardCommandBufferSize;
        MachineId = machineId;
    }



    private readonly SerialPort serialPort = new();
    private bool connected = false;

    /// <summary>
    /// Baud rate of the UART connection.
    /// </summary>
    public uint BaudRate { get; }
    private readonly string portNameIncludeString;

    /// <inheritdoc/>
    public override event EventHandler<IEnumerable<DataPacket>>? DataPacketsReceivedHandler;

    /// <inheritdoc/>
    public override bool Connect()
    {
        if (connected)
            return true;
        string[] portNames = SerialPort.GetPortNames();
        try
        {
            SetPortName(portNameIncludeString);
            serialPort.BaudRate = (int)BaudRate;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;
            serialPort.DtrEnable = true; // Essential for many CDC devices
            serialPort.RtsEnable = true; // Often required to clear the device 
            serialPort.Open();

            // When data is received, read it and use the event handler to notify the BoardQueue of the new data packets.
            serialPort.DataReceived += (sender, e) =>
            {
                IEnumerable<DataPacket> packets = Read();
                if (packets.Any())
                    DataPacketsReceivedHandler?.Invoke(this, packets);
            };

            serialPort.ErrorReceived += (sender, e) =>
            {
                ErrorCode errorCode = e.EventType switch
                {
                    SerialError.Frame => ErrorCode.SerialFrame,
                    SerialError.Overrun => ErrorCode.SerialOverrun,
                    SerialError.RXOver => ErrorCode.SerialRXOver,
                    SerialError.RXParity => ErrorCode.SerialRXParity,
                    SerialError.TXFull => ErrorCode.SerialTXFull,
                    _ => ErrorCode.Unknown
                };
                Result<DataPacket> dataPacket = DataPacket.New(Id, 0, Commands.CommandCode.ERROR, [(byte)errorCode]);
                Debug.Assert(dataPacket.IsSuccess);
                DataPacketsReceivedHandler?.Invoke(this, [dataPacket.Value]);
            };
        }
        catch
        {
            return false;
        }

        connected = true;
        return true;
    }

    /// <inheritdoc/>
    public override void Disconnect()
    {
        if (!connected)
            return;

        connected = false;
        serialPort.Close();
    }

    /// <summary>
    /// Disconnects on destructor.
    /// </summary>
    ~SerialCommunicator()
    {
        Disconnect();
    }

    private void SetPortName(string portNameIncludeString)
    {
        string[] serialPortNames = SerialPort.GetPortNames();
        string serialPortName = string.Empty;
        foreach (string portName in serialPortNames)
        {
            if (portName.Contains(portNameIncludeString))
                serialPortName = portName;
        }

        if (string.IsNullOrEmpty(serialPortName))
            throw new Exception($"No port names contain {portNameIncludeString}.");

        serialPort.PortName = serialPortName;
    }

    private bool dataPacketStarted = false;
    private int remainingBytesToRead = 0;
    private byte[] startedDataPacketBytes = [];

    /// <inheritdoc/>
    public override IEnumerable<DataPacket> Read()
    {
        List<DataPacket> packets = [];

        if (!connected)
        {
            bool success = Connect();
            if (!success)
                return packets;
        }

        try
        {
            while (serialPort.BytesToRead > 0)
            {
                // ensure at least one byte is available to read
                int bytesThatCanBeRead = serialPort.BytesToRead;

                if (!dataPacketStarted)
                {
                    // first byte is the data length
                    int packetDataLength = serialPort.ReadByte();
                    bytesThatCanBeRead--;

                    if (packetDataLength > DataPacket.MAX_DATA_LENGTH)
                        throw new Exception($"Data length cannot exceed {DataPacket.MAX_DATA_LENGTH}.");

                    if (packetDataLength < DataPacket.MIN_DATA_LENGTH)
                        throw new Exception($"Data length must be at least {DataPacket.MIN_DATA_LENGTH}.");

                    if (bytesThatCanBeRead >= packetDataLength)
                    {
                        // yay we can read entire packet and return it
                        uint totalLength = (uint)(packetDataLength + 1);
                        byte[] allData = new byte[totalLength];

                        // set data length byte
                        allData[0] = (byte)packetDataLength;

                        // set remaining bytes
                        serialPort.Read(allData, 1, packetDataLength);

                        // initialize new packet (this sets first byte of array)
                        Result<DataPacket> dataPacket = DataPacket.New(Id, allData);

                        Debug.Assert(dataPacket.IsSuccess);

                        dataPacketStarted = false;
                        Debug.WriteLine(dataPacket.ToString());
                        packets.Add(dataPacket.Value);
                        continue;
                    }
                    else
                    {
                        // initialize packet (this sets first byte of array)
                        startedDataPacketBytes = new byte[packetDataLength + 1];
                        startedDataPacketBytes[0] = (byte)packetDataLength;

                        // read as many remaining bytes as we can
                        serialPort.Read(startedDataPacketBytes, 1, bytesThatCanBeRead);

                        remainingBytesToRead = packetDataLength - bytesThatCanBeRead;
                        dataPacketStarted = true;
                        return packets;
                    }
                }
                else // data packet has been started
                {
                    // figure out offset of where we will write new bytes
                    byte numDataBytes = startedDataPacketBytes[0];
                    int offset = numDataBytes + 1 - remainingBytesToRead;

                    if (bytesThatCanBeRead >= remainingBytesToRead)
                    {
                        // yay we can read rest of packet and return it
                        // set remaining bytes
                        serialPort.Read(startedDataPacketBytes, offset, remainingBytesToRead);

                        dataPacketStarted = false;

                        Result<DataPacket> dataPacket = DataPacket.New(Id, startedDataPacketBytes);

                        Debug.Assert(dataPacket.IsSuccess);

                        Debug.WriteLine(dataPacket.Value.ToString());
                        packets.Add(dataPacket.Value);
                        continue;
                    }
                    else
                    {
                        // just add as many more bytes as we can
                        serialPort.Read(startedDataPacketBytes, offset, bytesThatCanBeRead);

                        remainingBytesToRead -= bytesThatCanBeRead;
                        dataPacketStarted = true;
                        return packets;
                    }
                }
            }
            return packets;
        }
        catch (TimeoutException)
        {
            Debug.WriteLine($"Read timeout - {serialPort.PortName}");
            return packets;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading from serial port - {serialPort.PortName}: {ex.Message}");
            return packets;
        }
    }

    /// <inheritdoc/>
    public override bool Send(DataPacket packet)
    {
        if (!connected)
        {
            bool success = Connect();
            if (!success)
                return false;
        }

        int serialPortBytesAvailable = serialPort.WriteBufferSize - serialPort.BytesToWrite;
        if (serialPortBytesAvailable < packet.AllData.Length)
            return false;

        if (BoardCommandBufferBytesFree < packet.AllData.Length)
            return false;

        serialPort.Write(packet.AllData.ToArray(), 0, packet.AllData.Length);
        serialPort.BaseStream.Flush();
        return true;
    }

}