using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using System.Diagnostics;
using System.IO.Ports;

namespace EpsilonCore.Communication;

/// <summary>
/// Communicator for a microcontroller board using UART protocol.
/// </summary>
public class UartCommunicator : ICommunicator
{
    /// <summary>
    /// Creates a new instance of <see cref="UartCommunicator"/>.
    /// </summary>
    /// <param name="name"><inheritdoc cref="IEntity.Name"/></param>
    /// <param name="baudRate">Baud rate of the UART connection.</param>
    /// <param name="portNameIncludeString">Name of the COM port.</param>
    internal UartCommunicator(string name, uint baudRate, string portNameIncludeString)
    {
        Name = name;
        BaudRate = baudRate;
        this.portNameIncludeString = portNameIncludeString;
    }

    /// <inheritdoc/>
    public uint Id { get; internal set; } = 0;

    /// <inheritdoc/>
    public string Name { get; }

    private readonly SerialPort serialPort = new();
    private bool connected = false;

    /// <summary>
    /// Baud rate of the UART connection.
    /// </summary>
    public uint BaudRate { get; }
    private readonly string portNameIncludeString;

    /// <inheritdoc/>
    public bool Connect()
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
        }
        catch
        {
            return false;
        }

        connected = true;
        return true;
    }
    /// <inheritdoc/>
    public void Disconnect()
    {
        if (!connected)
            return;

        connected = false;
        //readThread?.Join();
        serialPort.Close();
    }

    /// <summary>
    /// Disconnects on destructor.
    /// </summary>
    ~UartCommunicator()
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
    public DataPacket? Read()
    {
        if (!connected)
        {
            bool success = Connect();
            if (!success)
                return null;
        }
        try
        {
            // ensure at least one byte is available to read
            int bytesThatCanBeRead = serialPort.BytesToRead;
            if (bytesThatCanBeRead <= 0)
                return null;

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
                    return dataPacket.Value;
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
                    return null;
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
                    return dataPacket.Value;
                }
                else
                {
                    // just add as many more bytes as we can
                    serialPort.Read(startedDataPacketBytes, offset, bytesThatCanBeRead);

                    remainingBytesToRead -= bytesThatCanBeRead;
                    dataPacketStarted = true;
                    return null;
                }
            }
        }
        catch (TimeoutException)
        {
            Debug.WriteLine($"Read timeout - {serialPort.PortName}");
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Send(DataPacket packet)
    {
        if (!connected)
        {
            bool success = Connect();
            if (!success)
                return false;
        }

        if (serialPort.WriteBufferSize - serialPort.BytesToWrite <= packet.AllData.Length)
            return false;

        serialPort.Write(packet.AllData.ToArray(), 0, packet.AllData.Length);
        serialPort.BaseStream.Flush();
        return true;
    }

    /// <summary>
    /// Stores the last used sequence number used by a <see cref="DataPacket"/> when it is sent.
    /// Start at <see cref="byte.MaxValue"/> that way the first time 
    /// <see cref="IncrementAndReturnNextSequenceNumber"/> is called, it will wrap to 0 before being returned.
    /// </summary>
    private byte lastUsedSequenceNumber = byte.MaxValue;

    /// <inheritdoc/>
    public byte IncrementAndReturnNextSequenceNumber()
    {
        if (lastUsedSequenceNumber == byte.MaxValue)
            lastUsedSequenceNumber = 0;
        else
            lastUsedSequenceNumber++;

        return lastUsedSequenceNumber;
    }
}