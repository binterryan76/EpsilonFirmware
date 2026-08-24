using EpsilonCore.Boards;
using EpsilonCore.Commands.SetupCommands;
using EpsilonCore.Engine;
using EpsilonCore.Helpers; using GenericHelpers;
using System.Diagnostics;
using UnitsNet;

namespace EpsilonCore.Commands.Macros.BoardMacros;

/// <summary>
/// Contains macros and constants useful for machines using the BTT Octopus Pro board.
/// </summary>
public static class BttOctopusProMacros
{
    #region BTT Octopus Pro
    // -------------------------------------------------------------------------
    // DRIVER 0 (MOTOR_0)
    // -------------------------------------------------------------------------
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public const string PIN_STEPPER_0_STEP = "PF13";
    public const string PIN_STEPPER_0_DIR = "PF12";
    public const string PIN_STEPPER_0_EN = "PF14";  // active LOW
    public const string PIN_STEPPER_0_UART = "PC4";
    public const string PIN_STEPPER_0_CS = "PC4";
    public const string PIN_STEPPER_0_DIAG = "PG6";   // also ENDSTOP_0

    // -------------------------------------------------------------------------
    // DRIVER 1 (MOTOR_1)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_1_STEP = "PG0";
    public const string PIN_STEPPER_1_DIR = "PG1";
    public const string PIN_STEPPER_1_EN = "PF15";  // active LOW
    public const string PIN_STEPPER_1_UART = "PD11";
    public const string PIN_STEPPER_1_CS = "PD11";
    public const string PIN_STEPPER_1_DIAG = "PG9";   // also ENDSTOP_1

    // -------------------------------------------------------------------------
    // DRIVER 2 (MOTOR_2)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_2_STEP = "PF11";
    public const string PIN_STEPPER_2_DIR = "PG3";
    public const string PIN_STEPPER_2_EN = "PG5";   // active LOW
    public const string PIN_STEPPER_2_UART = "PC6";
    public const string PIN_STEPPER_2_CS = "PC6";
    public const string PIN_STEPPER_2_DIAG = "PG10";  // also ENDSTOP_2

    // -------------------------------------------------------------------------
    // DRIVER 3 (MOTOR_3)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_3_STEP = "PG4";
    public const string PIN_STEPPER_3_DIR = "PC1";
    public const string PIN_STEPPER_3_EN = "PA0";   // active LOW
    public const string PIN_STEPPER_3_UART = "PC7";
    public const string PIN_STEPPER_3_CS = "PC7";
    public const string PIN_STEPPER_3_DIAG = "PG11";  // also ENDSTOP_3

    // -------------------------------------------------------------------------
    // DRIVER 4 (MOTOR_4)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_4_STEP = "PF9";
    public const string PIN_STEPPER_4_DIR = "PF10";
    public const string PIN_STEPPER_4_EN = "PG2";   // active LOW
    public const string PIN_STEPPER_4_UART = "PF2";
    public const string PIN_STEPPER_4_CS = "PF2";
    public const string PIN_STEPPER_4_DIAG = "PG12";

    // -------------------------------------------------------------------------
    // DRIVER 5 (MOTOR_5)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_5_STEP = "PC13";
    public const string PIN_STEPPER_5_DIR = "PF0";
    public const string PIN_STEPPER_5_EN = "PF1";   // active LOW
    public const string PIN_STEPPER_5_UART = "PE4";
    public const string PIN_STEPPER_5_CS = "PE4";
    public const string PIN_STEPPER_5_DIAG = "PG13";

    // -------------------------------------------------------------------------
    // DRIVER 6 (MOTOR_6)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_6_STEP = "PE2";
    public const string PIN_STEPPER_6_DIR = "PE3";
    public const string PIN_STEPPER_6_EN = "PD4";   // active LOW
    public const string PIN_STEPPER_6_UART = "PE1";
    public const string PIN_STEPPER_6_CS = "PE1";
    public const string PIN_STEPPER_6_DIAG = "PG14";

    // -------------------------------------------------------------------------
    // DRIVER 7 (MOTOR_7)
    // -------------------------------------------------------------------------
    public const string PIN_STEPPER_7_STEP = "PE6";
    public const string PIN_STEPPER_7_DIR = "PA14";
    public const string PIN_STEPPER_7_EN = "PE0";   // active LOW
    public const string PIN_STEPPER_7_UART = "PD3";
    public const string PIN_STEPPER_7_CS = "PD3";
    public const string PIN_STEPPER_7_DIAG = "PG15";

    // -------------------------------------------------------------------------
    // ENDSTOPS
    // -------------------------------------------------------------------------
    public const string PIN_ENDSTOP_0 = "PG6";   // DIAG_0 / X-min
    public const string PIN_ENDSTOP_1 = "PG9";   // DIAG_1 / Y-min
    public const string PIN_ENDSTOP_2 = "PG10";  // DIAG_2 / Z-min
    public const string PIN_ENDSTOP_3 = "PG11";  // DIAG_3
    public const string PIN_ENDSTOP_4 = "PG12";  // DIAG_4
    public const string PIN_ENDSTOP_5 = "PG13";  // DIAG_5
    public const string PIN_ENDSTOP_6 = "PG14";  // DIAG_6
    public const string PIN_ENDSTOP_7 = "PG15";  // DIAG_7

    // -------------------------------------------------------------------------
    // HEATER OUTPUTS
    // -------------------------------------------------------------------------
    public const string PIN_HEATER_BED = "PA1";   // HB  – heated bed
    public const string PIN_HEATER_0 = "PA2";   // HE0 – hotend 0
    public const string PIN_HEATER_1 = "PA3";   // HE1 – hotend 1
    public const string PIN_HEATER_2 = "PB10";  // HE2 – hotend 2
    public const string PIN_HEATER_3 = "PB11";  // HE3 – hotend 3

    // -------------------------------------------------------------------------
    // THERMISTOR / TEMPERATURE SENSOR INPUTS
    // -------------------------------------------------------------------------
    public const string PIN_TEMP_BED = "PF3";   // TB
    public const string PIN_TEMP_0 = "PF4";   // T0
    public const string PIN_TEMP_1 = "PF5";   // T1
    public const string PIN_TEMP_2 = "PF6";   // T2
    public const string PIN_TEMP_3 = "PF7";   // T3

    // -------------------------------------------------------------------------
    // FANS
    // -------------------------------------------------------------------------
    public const string PIN_FAN_0 = "PA8";   // FAN0 – part cooling
    public const string PIN_FAN_1 = "PE5";   // FAN1
    public const string PIN_FAN_2 = "PD12";  // FAN2
    public const string PIN_FAN_3 = "PD13";  // FAN3
    public const string PIN_FAN_4 = "PD14";  // FAN4
    public const string PIN_FAN_5 = "PD15";  // FAN5

    // -------------------------------------------------------------------------
    // FILAMENT RUNOUT SENSORS
    // -------------------------------------------------------------------------
    public const string PIN_FILAMENT_SENSOR_0 = "PG12";
    public const string PIN_FILAMENT_SENSOR_1 = "PG13";
    public const string PIN_FILAMENT_SENSOR_2 = "PG14";
    public const string PIN_FILAMENT_SENSOR_3 = "PG15";

    // -------------------------------------------------------------------------
    // PROBE / BLTouch
    // -------------------------------------------------------------------------
    public const string PIN_PROBE_SENSOR = "PB7";   // BLTouch / Z-probe signal
    public const string PIN_PROBE_CONTROL = "PB6";   // BLTouch servo control

    // -------------------------------------------------------------------------
    // NEOPIXEL / RGB LED
    // -------------------------------------------------------------------------
    public const string PIN_NEOPIXEL = "PB0";

    // -------------------------------------------------------------------------
    // SPI BUS (shared, spi1)
    // -------------------------------------------------------------------------
    public const string PIN_SPI_MOSI = "PA7";
    public const string PIN_SPI_MISO = "PA6";
    public const string PIN_SPI_SCK = "PA5";

    // -------------------------------------------------------------------------
    // EXP1 DISPLAY HEADER
    // -------------------------------------------------------------------------
    public const string PIN_EXP1_1 = "PE8";
    public const string PIN_EXP1_2 = "PE7";
    public const string PIN_EXP1_3 = "PE9";
    public const string PIN_EXP1_4 = "PE10";
    public const string PIN_EXP1_5 = "PE12";
    public const string PIN_EXP1_6 = "PE13";
    public const string PIN_EXP1_7 = "PE14";
    public const string PIN_EXP1_8 = "PE15";
    // EXP1_9 = GND, EXP1_10 = 5V (power rails, no MCU pin)

    // -------------------------------------------------------------------------
    // EXP2 DISPLAY HEADER
    // -------------------------------------------------------------------------
    public const string PIN_EXP2_1 = "PA6";   // SPI MISO
    public const string PIN_EXP2_2 = "PA5";   // SPI SCK
    public const string PIN_EXP2_3 = "PB1";
    public const string PIN_EXP2_4 = "PA4";   // SPI CS (LCD)
    public const string PIN_EXP2_5 = "PB2";
    public const string PIN_EXP2_6 = "PA7";   // SPI MOSI
    public const string PIN_EXP2_7 = "PC15";
    public const string PIN_EXP2_8 = "PC5";
    // EXP2_8 = RST, EXP2_9 = GND (no MCU pin)

    // -------------------------------------------------------------------------
    // TMC SPI CS ALIASES (per driver, matches UART column for SPI mode)
    // -------------------------------------------------------------------------
    public const string PIN_TMC_CS_DRIVER_0 = "PC4";
    public const string PIN_TMC_CS_DRIVER_1 = "PD11";
    public const string PIN_TMC_CS_DRIVER_2 = "PC6";
    public const string PIN_TMC_CS_DRIVER_3 = "PC7";
    public const string PIN_TMC_CS_DRIVER_4 = "PF2";
    public const string PIN_TMC_CS_DRIVER_5 = "PE4";
    public const string PIN_TMC_CS_DRIVER_6 = "PE1";
    public const string PIN_TMC_CS_DRIVER_7 = "PD3";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Name of pins which are input pins on the BTT Octopus Pro.
    /// </summary>
    private static readonly HashSet<string> BttOctopusProInputPinNames =
    [
        PIN_TEMP_BED,
        PIN_TEMP_0,
        PIN_TEMP_1,
        PIN_TEMP_2,
        PIN_TEMP_3,
        PIN_FILAMENT_SENSOR_0,
        PIN_FILAMENT_SENSOR_1,
        PIN_FILAMENT_SENSOR_2,
        PIN_FILAMENT_SENSOR_3,
        PIN_ENDSTOP_0,
        PIN_ENDSTOP_1,
        PIN_ENDSTOP_2,
        PIN_ENDSTOP_3,
        PIN_ENDSTOP_4,
        PIN_ENDSTOP_5,
        PIN_ENDSTOP_6,
        PIN_ENDSTOP_7,
    ];

    /// <summary>
    /// Queues commands to add the BTT Octopus Pro board and all it's pins.
    /// </summary>
    /// <param name="engine"></param>
    /// <param name="machineIndex"></param>
    /// <param name="boardName"></param>
    /// <param name="communicatorId"></param>
    /// <param name="lineNumber"></param>
    public static void AddBttOctopusProBoardAndPinsMacro(
        EpsilonEngine engine,
        uint machineIndex,
        string boardName,
        uint communicatorId,
        uint? lineNumber = null)
    {
        Result<BoardTimer> mainBoardTimerLowPrecision = BoardTimer.New(
            "STM32 TIM 5 Low Precision Timer",
            Duration.FromMilliseconds(UInt32.MaxValue),
            Duration.FromMilliseconds(1));

        Result<BoardTimer> mainBoardTimerMotor = BoardTimer.New(
            "STM32 TIM 4/3 Stepper Motor Timer",
            Duration.FromMicroseconds(UInt16.MaxValue),
            Duration.FromMicroseconds(1));

        Debug.Assert(mainBoardTimerLowPrecision.IsSuccess);
        Debug.Assert(mainBoardTimerMotor.IsSuccess);

        Board mainBoard = new(
            boardName,
            communicatorId,
            Frequency.FromMegahertz(180),
            mainBoardTimerLowPrecision.Value,
            mainBoardTimerMotor.Value,
            Board.IntSize.Bits32,
            Board.FloatSize.Bits32,
            isLittleEndian: true);

        engine.EnqueueCommand(machineIndex, new AddBoardCommand(mainBoard, lineNumber++));

        // generate pin names PA0 - PG15
        IEnumerable<string> pinNames = Stm32.GeneratePinNames('G', 15);
        byte boardsPinId = 0;
        foreach (string pinName in pinNames)
        {
            Pin pin = new(
                boardsPinId,
                pinName,
                boardId: 0,
                isOutput: BttOctopusProInputPinNames.Contains(pinName),
                isDigital: true);

            engine.EnqueueCommand(machineIndex, new AddPinCommand(pin, lineNumber++));

            boardsPinId++;
        }
    }
    #endregion
}
