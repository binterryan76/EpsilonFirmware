using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Helpers;
using System.Collections.Immutable;

namespace EpsilonCore.Communication;

/// <summary>
/// A data packet is just a byte array and where to send it.
/// The first byte indicates the number of bytes left to read (data length) which 
/// means the max data length is 255 and the max total length is 1 + 255.
/// 
/// </summary>
public record DataPacket
{
    /// <summary>
    /// Index of the <see cref="DataLength"/> in the <see cref="AllData"/> array.
    /// </summary>
    public const int DATA_LENGTH_INDEX = 0;

    /// <summary>
    /// Index of the <see cref="SequenceNumber"/> in the <see cref="AllData"/> array.
    /// </summary>
    public const int SEQUENCE_NUM_INDEX = 1;

    /// <summary>
    /// Index of the <see cref="CommandCode"/> in the <see cref="AllData"/> array.
    /// </summary>
    public const int COMMAND_CODE_INDEX = 2;

    /// <summary>
    /// ID of the <see cref="ICommunicator"/> to send <see cref="DataPacket"/> with 
    /// or the <see cref="ICommunicator"/> that received the <see cref="DataPacket"/>.
    /// TODO: Maybe rename to BoardQueueId? BoardQueue.Id should equal ICommunicator.Id which should equal Board.Id
    /// </summary>
    public uint CommunicatorId { get; init; }

    /// <summary>
    /// First <see cref="byte"/> of the <see cref="AllData"/> array indicating how many additional <see cref="byte"/>s it will contain.
    /// </summary>
    public byte DataLength => AllData[DATA_LENGTH_INDEX];

    /// <summary>
    /// Second <see cref="byte"/> of the <see cref="AllData"/> array giving a semi-unique number for the command.
    /// </summary>
    public byte SequenceNumber => AllData[SEQUENCE_NUM_INDEX];

    /// <summary>
    /// Third <see cref="byte"/> of the <see cref="AllData"/> array indicating the <see cref="Commands.CommandCode"/>.
    /// </summary>
    public byte CommandCode => AllData[COMMAND_CODE_INDEX];

    /// <summary>
    /// <see cref="byte"/> array containing all the data to send to a <see cref="Board"/>.
    /// </summary>
    public ImmutableArray<byte> AllData { get; init; }

    /// <summary>
    /// Total number of <see cref="byte"/>s in the <see cref="AllData"/> array.
    /// </summary>
    public int TotalLength => AllData.Length;

    /// <summary>
    /// Maximum number of data <see cref="byte"/>s in the <see cref="AllData"/> array.
    /// </summary>
    public const byte MAX_DATA_LENGTH = byte.MaxValue;

    /// <summary>
    /// Maximum number of total <see cref="byte"/>s in the <see cref="AllData"/> array.
    /// </summary>
    public const int MAX_TOTAL_LENGTH = MAX_DATA_LENGTH + 1;

    /// <summary>
    /// Minimum number of data <see cref="byte"/>s in the <see cref="AllData"/> array.
    /// All data packets have a <see cref="SequenceNumber"/> data <see cref="byte"/> and a <see cref="CommandCode"/> data <see cref="byte"/>.
    /// </summary>
    public const byte MIN_DATA_LENGTH = 2;

    /// <summary>
    /// Minimum number of total <see cref="byte"/>s in the <see cref="AllData"/> array.
    /// All data packets have a <see cref="DataLength"/> <see cref="byte"/>, <see cref="SequenceNumber"/> <see cref="byte"/>, and a <see cref="CommandCode"/> <see cref="byte"/>.
    /// </summary>
    public const byte MIN_TOTAL_LENGTH = MIN_DATA_LENGTH + 1;

    private DataPacket(uint communicatorId, byte dataLength, byte sequenceNumber, CommandCode commandCode, byte[] otherData)
    {
        CommunicatorId = communicatorId;
        AllData = [dataLength, sequenceNumber, (byte)commandCode, .. otherData];
    }

    /// <summary>
    /// Instantiates a new <see cref="DataPacket"/> with validation.
    /// Note: This copies the data array into an internal array.
    /// </summary>
    /// <param name="communicatorId"></param>
    /// <param name="sequenceNumber"></param>
    /// <param name="commandCode"></param>
    /// <param name="otherData"></param>
    /// <exception cref="ArgumentException"></exception>
    public static Result<DataPacket> New(uint communicatorId, byte sequenceNumber, CommandCode commandCode, byte[] otherData)
    {
        int dataLength = otherData.Length + 2;
        if (dataLength > MAX_DATA_LENGTH)
            return new ArgumentException($"Data length cannot exceed {MAX_DATA_LENGTH}.");

        byte dataLengthByte = (byte)(dataLength);
        return new DataPacket(communicatorId, dataLengthByte, sequenceNumber, commandCode, otherData);
    }

    private DataPacket(uint communicatorId, byte[] allData)
    {
        CommunicatorId = communicatorId;
        AllData = [.. allData];
    }

    /// <summary>
    /// Instantiates a new <see cref="DataPacket"/> with validation.
    /// Note: This copies the data array into an internal array.
    /// </summary>
    /// <param name="communicatorId"></param>
    /// <param name="allData"></param>
    /// <returns></returns>
    public static Result<DataPacket> New(uint communicatorId, byte[] allData)
    {

        if (allData.Length > MAX_TOTAL_LENGTH)
            return new ArgumentException($"Total length cannot exceed {MAX_TOTAL_LENGTH}.");

        if (allData.Length < MIN_TOTAL_LENGTH)
            return new ArgumentException($"Total length must be at least {MIN_TOTAL_LENGTH}.");

        if (allData[0] != allData.Length - 1)
            return new ArgumentException($"Data length is {allData[0]} but should be {allData.Length - 1}.");

        return new DataPacket(communicatorId, allData);
    }

    private DataPacket(uint communicatorId, uint totalLength)
    {
        CommunicatorId = communicatorId;
        byte[] allData = new byte[totalLength];

        // Set data length byte
        allData[0] = (byte)(totalLength - 1);

        AllData = ImmutableArray.Create(allData);
    }

    /// <summary>
    /// Instantiates a blank <see cref="DataPacket"/> with validation.
    /// </summary>
    /// <param name="communicatorId"></param>
    /// <param name="totalLength"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Result<DataPacket> New(uint communicatorId, uint totalLength)
    {
        if (totalLength > MAX_TOTAL_LENGTH)
            return new ArgumentException($"Total length cannot exceed {MAX_TOTAL_LENGTH}.");

        if (totalLength < MIN_TOTAL_LENGTH)
            return new ArgumentException($"Total length must be at least {MIN_TOTAL_LENGTH}.");

        return new DataPacket(communicatorId, totalLength);
    }

    /// <summary>
    /// Returns a string of the format:
    /// "CommandCode: {CommandCode} | SequenceNumber: {SequenceNumber} | Data: {AllData.ToString}"
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"CommandCode: {CommandCode} | SequenceNumber: {SequenceNumber} | Data: {AllData.ToString}";
    }
}
