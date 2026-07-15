using EpsilonCore.Communication;
using EpsilonCore.Machines;
using System.Collections.Immutable;
using UnitsNet;

namespace EpsilonCore.Boards;

public record Board : IEntity
{
    public Board(
        string name,
        uint communicatorId,
        Frequency clockFrequency,
        BoardTimer timerLowPrecision,
        BoardTimer timerMotor,
        IntSize sizeOfInt,
        FloatSize sizeOfDouble,
        bool isLittleEndian = true)
    {
        Name = name;
        ClockFrequency = clockFrequency;
        TimerLowPrecision = timerLowPrecision;
        TimerMotor = timerMotor;
        SizeOfInt = sizeOfInt;
        SizeOfDouble = sizeOfDouble;
        IsLittleEndian = isLittleEndian;
        CommunicatorId = communicatorId;
        HeaterIds = ImmutableList<uint>.Empty;
        ThermometerIds = ImmutableList<uint>.Empty;
        SwitchIds = ImmutableList<uint>.Empty;
        ActuatorLinearIds = ImmutableList<uint>.Empty;
        ActuatorRotationalIds = ImmutableList<uint>.Empty;
    }

    public uint Id { get; init; } = 0;
    public string Name { get; init; }
    public uint CommunicatorId { get; init; }
    public IImmutableList<uint> HeaterIds { get; init; }
    public IImmutableList<uint> ThermometerIds { get; init; }
    public IImmutableList<uint> SwitchIds { get; init; }
    public IImmutableList<uint> ActuatorLinearIds { get; init; }
    public IImmutableList<uint> ActuatorRotationalIds { get; init; }
    public Frequency ClockFrequency { get; }

    /// <summary>
    /// Each board will have a schedule for low precision actions.
    /// For example, an STM32 32 bit timer that ticks every millisecond can count to 49.7 days.
    /// </summary>
    public BoardTimer TimerLowPrecision { get; init; }

    /// <summary>
    /// Eeach board will have a timer for stepper motor pulse interrupts.
    /// For example, an STM32 16 bit timer that ticks every microsecond 
    /// will send step pulses to the nearest microsecond.
    /// </summary>
    public BoardTimer TimerMotor { get; init; }
    public IntSize SizeOfInt { get; }
    public FloatSize SizeOfDouble { get; }
    public bool IsLittleEndian { get; }

    public enum IntSize
    {
        Bits8 = 8,
        Bits16 = 16,
        Bits32 = 32,
        Bits64 = 64,
        Bits128 = 128,
    }

    public enum FloatSize
    {
        Bits16 = 16,
        Bits32 = 32,
        Bits64 = 64,
    }

    public void WriteInt(int value, ref byte[] buffer, int offset)
    {
        switch (SizeOfInt)
        {
            case IntSize.Bits8:
                if (value > byte.MaxValue)
                    throw new ArgumentException($"Value ({value}) exceeds max value ({byte.MaxValue}).", nameof(value));
                byte value8 = (byte)value;
                buffer[offset] = value8;
                return;
            case IntSize.Bits16:
                if (value > Int16.MaxValue)
                    throw new ArgumentException($"Value ({value}) exceeds max value ({Int16.MaxValue}).", nameof(value));
                Int16 value16 = (Int16)value;
                byte[] bytes16 = BitConverter.GetBytes(value16);
                Array.Copy(bytes16, 0, buffer, offset, bytes16.Length);
                return;
            case IntSize.Bits32:
                Int32 value32 = (Int32)value;
                byte[] bytes32 = BitConverter.GetBytes(value32);
                Array.Copy(bytes32, 0, buffer, offset, bytes32.Length);
                return;
            case IntSize.Bits64:
                Int64 value64 = (Int64)value;
                byte[] bytes64 = BitConverter.GetBytes(value64);
                Array.Copy(bytes64, 0, buffer, offset, bytes64.Length);
                return;
            case IntSize.Bits128:
                Int128 value128 = (Int128)value;
                byte[] bytes128 = BitConverter.GetBytes(value128);
                Array.Copy(bytes128, 0, buffer, offset, bytes128.Length);
                return;
            default:
                throw new Exception($"Unsupported VarSize when converting int: {SizeOfInt}");
        }
    }

    public void WriteDouble(double value, ref byte[] buffer, int offset)
    {
        switch (SizeOfDouble)
        {
            case FloatSize.Bits16:
                if (value > (double)Half.MaxValue)
                    throw new ArgumentException($"Value ({value}) exceeds max value ({Half.MaxValue}).", nameof(value));
                if (value < (double)Half.MinValue)
                    throw new ArgumentException($"Value ({value}) is below min value ({Half.MinValue}).", nameof(value));
                Half value16 = (Half)value;
                byte[] bytes16 = BitConverter.GetBytes(value16);
                Array.Copy(bytes16, 0, buffer, offset, bytes16.Length);
                return;
            case FloatSize.Bits32:
                if (value > float.MaxValue)
                    throw new ArgumentException($"Value ({value}) exceeds max value ({float.MaxValue}).", nameof(value));
                if (value < float.MinValue)
                    throw new ArgumentException($"Value ({value}) is below min value ({float.MinValue}).", nameof(value));
                float value32 = (float)value;
                byte[] bytes32 = BitConverter.GetBytes(value32);
                Array.Copy(bytes32, 0, buffer, offset, bytes32.Length);
                return;
            case FloatSize.Bits64:
                byte[] bytes64 = BitConverter.GetBytes(value);
                Array.Copy(bytes64, 0, buffer, offset, bytes64.Length);
                return;
            default:
                throw new Exception($"Unsupported VarSize when converting double: {SizeOfInt}");
        }
    }

    /// <summary>
    /// Converts a <see cref="double"/> to a byte array to be included in a <see cref="DataPacket"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public byte[] ToByteArray(double value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts an <see cref="int"/> to a byte array to be included in a <see cref="DataPacket"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public byte[] ToByteArray(int value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts an <see cref="uint"/> to a byte array to be included in a <see cref="DataPacket"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public byte[] ToByteArray(uint value)
    {
        return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// Converts a <see cref="float"/> to a byte array to be included in a <see cref="DataPacket"/>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public byte[] ToByteArray(float value)
    {
        return BitConverter.GetBytes(value);
    }
}
