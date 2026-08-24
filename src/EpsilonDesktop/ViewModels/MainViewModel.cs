using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Actuator;
using EpsilonCore.Boards;
using EpsilonCore.Commands;
using EpsilonCore.Commands.Macros.BoardMacros;
using EpsilonCore.Commands.MotionCommands;
using EpsilonCore.Commands.SetupCommands;
using EpsilonCore.Engine;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using EpsilonCore.Motion;
using EpsilonCore.Motion.Axis;
using EpsilonCore.Motion.Kinematics;
using EpsilonCore.Units;
using EpsilonDesktop.Models;
using GenericHelpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using UnitsNet;

namespace EpsilonDesktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ResponseLog { get; set; } = "";

    [ObservableProperty]
    public partial string CodeToSend { get; set; } = "";

    public ObservableCollection<string> AxisGraphs { get; set; } = [];

    private readonly ScriptGlobals globals;
    private readonly ScriptOptions options = ScriptOptions.Default
            .AddReferences(typeof(ScriptGlobals).Assembly)
            .AddImports(
                "System",
                "System.Collections.Generic",
                "EpsilonCore",
                "UnitsNet",
                "UnitsNetHelpers",
                "EpsilonDesktop.Models",
                "EpsilonCore.Commands",
                "EpsilonCore.Commands.TestCommands");



    [ObservableProperty]
    public partial FilePickerViewModel FilePickerViewModel { get; set; } = new();

    public ObservableCollection<MachineViewModel> MachineViewModels { get; set; } = [];

    [ObservableProperty]
    public partial AppSettings AppSettings { get; set; }

    public MainViewModel()
    {
        globals = new(Log);
        AppSettings = LoadAppSettings();

        AppSettings LoadAppSettings()
        {
            Result<AppSettings> appSettings = AppSettings.Load();

            if (appSettings.IsSuccess)
                return appSettings.Value;
            else
            {
                Log($"Failed to load app settings: {appSettings.Exception.Message}");
                return new();
            }
        }
    }

    public async Task OnLoad()
    {
        if (AppSettings.StartupFilePath is null || !File.Exists(AppSettings.StartupFilePath))
            return;

        try
        {
            string startupFileContents = File.ReadAllText(AppSettings.StartupFilePath);
            await CSharpScript.RunAsync(startupFileContents, options, globals);
        }
        catch (Exception ex)
        {
            Log($"Compile error:\n\t{ex.Message}");
        }
    }

    private bool Log(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            ResponseLog += message + "\n";
        return true;
    }

    [RelayCommand]
    public async Task SendCode()
    {
        try
        {
            await CSharpScript.RunAsync(CodeToSend, options, globals);
        }
        catch (Exception ex)
        {
            Log($"Compile error:\n\t{ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SendCodeFile()
    {
        try
        {
            string code = File.ReadAllText(FilePickerViewModel.FilePath);
            await CSharpScript.RunAsync(code, options, globals);
        }
        catch (Exception ex)
        {
            Log(ex.Message);
        }
    }

    [RelayCommand]
    public void TestCode()
    {
        //UartCommunicator mainBoardCommunicator = new(
        //    "Arduino UART Communicator",
        //    baudRate: 9600,
        //    "COM7");

        //UartCommunicator mainBoardCommunicator = new(
        //    "STM UART Communicator",
        //    baudRate: 115200,
        //    "COM3");

        MotionSystemPrecisions precisions = new(
            Length.FromMillimeters(0.0001),
            Angle.FromDegrees(0.001),
            Duration.FromMicroseconds(10));

        Machine mainMachine = new(
            "Binter Printer",
            DisplayUnits.DefaultMetric,
            globals.TextboxResultLogger,
            precisions);

        //MotionSystem motionSystem = MotionSystem.New(mainBoard);
        KinematicsScara2D kinematics = new(
            "Kinematics Scara 2D",
            Length.FromMillimeters(30),
            Length.FromMillimeters(30));

        StepperMotor stepperMotorProximal = new(
            "Proximal Motor",
            boardId: 0,
            fullStepsPerRotation: 200,
            microstepsPerFullStep: 16,
            maxSpeed: RotationalSpeed.FromDegreesPerSecond(360),
            squareCornerSpeed: RotationalSpeed.FromRevolutionsPerMinute(10),
            homingSpeed: RotationalSpeed.FromDegreesPerSecond(100),
            maxAcceleration: RotationalAcceleration.FromDegreesPerSecondSquared(3000));

        StepperMotor stepperMotorDistal = new(
            "Distal Motor",
            boardId: 0,
            fullStepsPerRotation: 200,
            microstepsPerFullStep: 16,
            maxSpeed: RotationalSpeed.FromDegreesPerSecond(200),
            squareCornerSpeed: RotationalSpeed.FromRevolutionsPerMinute(15),
            homingSpeed: RotationalSpeed.FromDegreesPerSecond(100),
            maxAcceleration: RotationalAcceleration.FromDegreesPerSecondSquared(2000));

        AxisLinear x = new(
            "X",
            KinematicSystemId: 0,
            IsExtrusionAxis: false,
            PosMin: Length.FromMillimeters(0),
            PosMax: Length.FromMillimeters(400),
            PosAtMinEndstop: Length.FromMillimeters(0),
            PosAtMaxEndstop: Length.FromMillimeters(400),
            Pos: Length.FromMillimeters(5));

        AxisLinear y = new(
            "Y",
            KinematicSystemId: 0,
            IsExtrusionAxis: false,
            PosMin: Length.FromMillimeters(0),
            PosMax: Length.FromMillimeters(300),
            PosAtMinEndstop: Length.FromMillimeters(0),
            PosAtMaxEndstop: Length.FromMillimeters(300),
            Pos: Length.FromMillimeters(5));

        AxisLinear z = new(
            "Z",
            KinematicSystemId: 0,
            IsExtrusionAxis: false,
            PosMin: Length.FromMillimeters(0),
            PosMax: Length.FromMillimeters(200),
            PosAtMinEndstop: Length.FromMillimeters(0),
            PosAtMaxEndstop: Length.FromMillimeters(200));


        const uint mainMachineId = 0;
        const uint mainCommunicatorId = 0;

        EpsilonEngine engine = globals.Engine;
        engine.CommandQueued += (object? sender, QueuedCommand command) =>
        {
            Debug.Assert(command.InitialMachine is not null);
            command.InitialMachine.ResultMessageLogger.Log(
                Helper.GetFormattedDisplayMessage(
                    command.Command,
                    ErrorLevel.Success,
                    "Command queued"));
        };
        engine.CommandSent += (object? sender, QueuedCommand command) =>
        {
            Debug.Assert(command.InitialMachine is not null);
            command.InitialMachine.ResultMessageLogger.Log(
                Helper.GetFormattedDisplayMessage(
                    command.Command,
                    ErrorLevel.Success,
                    "Command sent"));
        };
        engine.CommandResolved += (object? sender, QueuedCommand command) =>
        {
            Debug.Assert(command.ResultantMachine is not null);
            command.ResultantMachine.ResultMessageLogger.Log(
                Helper.GetFormattedDisplayMessage(
                    command.Command,
                    ErrorLevel.Success,
                    "Command resolved"));
        };
        engine.MachineQueueAdded += (object? engineMachineQueueAddedTo, uint machineQueueId) =>
        {
            Result<MachineViewModel> viewModel = MachineViewModel.New(engine, machineQueueId, engine.EngineLogger);
            Debug.Assert(viewModel.IsSuccess);
            MachineViewModels.Add(viewModel.Value);
        };
        engine.AddMachineQueue(mainMachine);

        uint lineNumber = 1;

        BttOctopusProMacros.AddBttOctopusProBoardAndPinsMacro(
            engine,
            mainMachineId,
            "BTT Octopus Pro V1.0",
            mainCommunicatorId,
            lineNumber++);

        engine.CompleteAllCommands(mainMachineId);

        IPin endstopPinX = engine.GetCurrentMachine(mainMachineId)!.Entities.Pins.Values.First();
        IPin endstopPinY = engine.GetCurrentMachine(mainMachineId)!.Entities.Pins.Values.Last();
        Endstop endstopX = new("X Endstop", endstopPinX, isMinEndstop: true, inverted: false, activated: false);
        Endstop endstopY = new("Y Endstop", endstopPinY, isMinEndstop: true, inverted: false, activated: false);
        EndstopSet endstopSetX = EndstopSet.MinOnly(0);
        EndstopSet endstopSetY = EndstopSet.MinOnly(1);

        List<ICommand> commands =
        [
            new AddEndstopCommand(endstopX, lineNumber++),
            new AddEndstopCommand(endstopY, lineNumber++),
            new AddLinearAxisCommand(x, lineNumber++),
            new AddLinearAxisCommand(y, lineNumber++),
            new AddLinearAxisCommand(z, lineNumber++),
            new AddActuatorRotationalCommand(stepperMotorProximal, lineNumber++),
            new AddActuatorRotationalCommand(stepperMotorDistal, lineNumber++),
            new AddKinematicsCommand(kinematics, [], [0, 1], [0, 1], [], [endstopSetX, endstopSetY], lineNumber++),
        ];

        engine.EnqueueCommands(mainMachineId, commands);
        engine.CompleteAllCommands(mainMachineId);

        Result<MoveCommand> moveCommand1 = MoveCommand.New(
            ImmutableDictionary<uint, Length>.Empty
                .Add(0, Length.FromMillimeters(10))
                .Add(1, Length.FromMillimeters(59)),
            Speed.FromMillimetersPerSecond(10),
            null,
            null,
            absoluteMove: true,
            lineNumber++);
        Debug.Assert(moveCommand1.IsSuccess);

        Result<MoveCommand> moveCommand2 = MoveCommand.New(
            ImmutableDictionary<uint, Length>.Empty
                .Add(0, Length.FromMillimeters(5))
                .Add(1, Length.FromMillimeters(5)),
            Speed.FromMillimetersPerSecond(100),
            null,
            null,
            absoluteMove: true,
            lineNumber++);
        Debug.Assert(moveCommand2.IsSuccess);

        List<ICommand> moveCommands = [moveCommand1.Value, moveCommand2.Value];
        engine.EnqueueCommands(mainMachineId, moveCommands);

    }

    [RelayCommand]
    public void TestMoveQueue()
    {
        const uint mainMachineId = 0;

        MotionSystemPrecisions precisions = new(
            Length.FromMillimeters(0.0001),
            Angle.FromDegrees(0.001),
            Duration.FromMicroseconds(10));

        Machine mainMachine = new(
            "Binter Printer",
            DisplayUnits.DefaultMetric,
            globals.TextboxResultLogger,
            precisions);

        EpsilonEngine engine = globals.Engine;
        engine.AddMachineQueue(mainMachine);

        CompositeKinematicSystem kinematicSystem1 = engine.GetCurrentMachine(mainMachineId)!.MotionSystem.CompositeKinematicSystem;
        MoveQueue queue = new();
        Result<Move> move1 = Move.New(
            precisions,
            kinematicSystem1,
            [Length.FromMillimeters(5), Length.FromMillimeters(5)],
            [],
            [Length.FromMillimeters(10), Length.FromMillimeters(59)],
            [],
            Speed.FromMillimetersPerSecond(10),
            null);
        Debug.Assert(move1.IsSuccess);

        Result<Move> move2 = Move.New(
            precisions,
            kinematicSystem1,
            [Length.FromMillimeters(10), Length.FromMillimeters(59)],
            [],
            [Length.FromMillimeters(5), Length.FromMillimeters(5)],
            [],
            Speed.FromMillimetersPerSecond(10),
            null);
        Debug.Assert(move2.IsSuccess);

        queue.Add(move1.Value);
        queue.Add(move2.Value);

        Stopwatch stopwatch = Stopwatch.StartNew();
        queue.SolveAllMoves(
            new MotionSystemPrecisions(
                Length.FromMillimeters(0.01),
                Angle.FromDegrees(0.05),
                Duration.FromSeconds(0.0001)));
        stopwatch.Stop();

        long millis1 = stopwatch.ElapsedMilliseconds;

        queue.TryWriteToCsv(
            "C:\\Users\\binte\\Downloads\\MoveQueueWithJunctionLimitsSimplified.csv",
            kinematicSystem1,
            writeSimplifiedPoints: true);
    }

}