namespace EpsilonCore.Communication;

public enum ErrorCode
{
    UartOverrun = 1,
    UartBaudRate,
    UartParity,
    CommandBufferFull,
    SerialFrame,
    SerialOverrun,
    SerialRXOver,
    SerialRXParity,
    SerialTXFull,
    Unknown,
}
