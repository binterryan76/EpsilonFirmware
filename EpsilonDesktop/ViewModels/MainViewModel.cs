using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Actuator;
using EpsilonCore.Boards;
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

    private readonly ScriptGlobals globals = new();
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

    public MainViewModel()
    {
        globals.TextboxResultLogger.LogFunction = Log;
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

        //KinematicsCartesian3D kinematics = new("Cartesian Kinematics");

        //KinematicSystemCartesian3D kinematicsSystem = new(
        //    "3D Cartesian Kinematic System",
        //    kinematics,
        //    actuatorIdX: 0,
        //    actuatorIdY: 1,
        //    actuatorIdZ: 2,
        //    axisIdX: 0,
        //    axisIdY: 1,
        //    axisIdZ: 2);

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
        engine.AddMachineQueue(mainMachine);

        /*
        bool success = engine.AddUartCommunicator(
            mainMachineId,
            "STM UART Communicator",
            baudRate: 115200,
            "COM11");
        

        if (!success)
        {
            Debug.WriteLine("Could not connect.");
            return;
        }
        */

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

        engine.EnqueueCommand(mainMachineId, new AddEndstopCommand(endstopX, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new AddEndstopCommand(endstopY, lineNumber++));

        EndstopSet endstopSetX = EndstopSet.MinOnly(0);
        EndstopSet endstopSetY = EndstopSet.MinOnly(1);
        /*
        bool success = engine.CompleteAllCommands(mainMachineId);

        if (!success)
        {
            Debug.WriteLine("Commands could not complete.");
            return;
        }
        */

        //Pin ledPin = engine.GetCurrentMachine(mainMachineId)!.Entities.Pins.Values.First(pin => pin.Name == "PB7");

        //Led led = new("Yellow LED", pinId: ledPin.Id);

        //engine.EnqueueCommand(mainMachineId, new AddBoardCommand(mainBoard, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new AddLinearAxisCommand(x, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new AddLinearAxisCommand(y, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new AddLinearAxisCommand(z, lineNumber++));


        engine.EnqueueCommand(mainMachineId, new AddActuatorRotationalCommand(stepperMotorProximal, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new AddActuatorRotationalCommand(stepperMotorDistal, lineNumber++));

        engine.EnqueueCommand(mainMachineId, new AddKinematicsCommand(kinematics, [], [0, 1], [0, 1], [], [endstopSetX, endstopSetY], lineNumber++));

        engine.CompleteAllCommands(mainMachineId);

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

        //stopwatch.Restart();
        //queue.Simplify();
        //stopwatch.Stop();
        //long millis2 = stopwatch.ElapsedMilliseconds;

        //queue.TryWriteToCsv("C:\\Users\\binte\\Downloads\\MoveQueueSimplified.csv", kinematicSystem1);


        Result<MoveCommand> moveCommand = MoveCommand.New(
            ImmutableDictionary<uint, Length>.Empty
                .Add(0, Length.FromMillimeters(10))
                .Add(1, Length.FromMillimeters(59)),
            Speed.FromMillimetersPerSecond(100),
            null,
            null,
            lineNumber++);

        Debug.Assert(moveCommand.IsSuccess);

        engine.EnqueueCommand(mainMachineId, moveCommand.Value);

        //engine.EnqueueCommand(mainMachineId, new AddPinCommand(ledPin, lineNumber++));

        /*
        engine.EnqueueCommand(mainMachineId, new AddLedCommand(led, lineNumber++));
        engine.EnqueueCommand(mainMachineId, new BlinkPinCommand(
            ledPin.Id,
            BlinkCount: 10,
            BlinkTimeHigh: Duration.FromMilliseconds(200),
            BlinkTimeLow: Duration.FromMilliseconds(100),
            lineNumber++));
        */

        /*
        engine.EnqueueCommand(mainMachineId, new MoveCommand(
            [Length.FromMillimeters(10), Length.FromMillimeters(10), Length.FromMillimeters(10)],
            Speed.FromMillimetersPerSecond(100), [], null, lineNumber++));

        engine.EnqueueCommand(mainMachineId, new MoveCommand(
            [Length.FromMillimeters(100), Length.FromMillimeters(100), Length.FromMillimeters(100)],
            Speed.FromMillimetersPerSecond(20), [], null, lineNumber++));
        */

        //globals.Machines[0].EnqueueCommand(cmd1);
        //globals.Machines[0].ConnectAll();

        /*
        StepperMotor test = new("Hello", mainBoard);


        IActuatorLinear xMotor = new StepperMotorLinear()
        {
            Name = "X Stepper",
            FullStepsPerRotation = 200,
            MicrostepsPerFullStep = 16,
            MaxAcceleration = MmPerS2(10),
            MaxSpeed = MmPerS(10),
            Board = mainBoard,
        };

        StepperMotorLinear yMotor = new()
        {
            Name = "Y Stepper",
            FullStepsPerRotation = 200,
            MicrostepsPerFullStep = 16,
            MaxAcceleration = MmPerS2(10),
            MaxSpeed = MmPerS(10),
            Board = mainBoard,
        };

        MotionSystem motionSystem = new()
        {
            AxesLinear = [
                new AxisLinear() { Name = "X", PosMin = Mm(-10000.0), PosMax = Mm(10000) },
                new AxisLinear() { Name = "Y", PosMin = Mm(-10000), PosMax = Mm(10000) }],
            ActuatorsLinear = [xMotor, yMotor],
            MaxSpeedsLinear = [MmPerS(400.0), MmPerS(100.0)],
            MaxAccelerationsLinear = [MmPerS2(10.0), MmPerS2(10.0)],
            SpeedLinearCurrent = MmPerS(80.0),

            AxesRotational = [],
            ActuatorsRotational = [],
            MaxSpeedsRotational = [],
            MaxAccelerationsRotational = [],
            SpeedRotationalCurrent = RadiansPerS(10.0),

            KinematicSystems = new Cartesian(),
            Board = mainBoard,
        };


        //motionSystem.KinematicSystems = new EpsilonCore.InternalList<IKinematics>();
        Machine machine = new(
            "Binter Printer",
            motionSystem,
            DisplayUnits.DefaultMetric,
            new DebugConsoleLogger());

        machine.Boards.Add(mainBoard);

        machine.Connect();

        ICommand[] commands = [
            MoveCommand.Metric([300, 600], 100),
            MoveCommand.Metric([80, 300], 100)];

        EpsilonCore.Helpers.ApplyLineNumbers(commands);

        machine.EnqueueCommands(commands);

        List<List<double>> allTimes = [];
        List<List<double>> allPositions = [];
        List<List<double>> allVelocities = [];
        List<List<double>> allAccelerations = [];
        List<List<double>> allMotorAngles = [];
        List<List<double>> allMotorVelocities = [];
        List<List<double>> allPositions2 = [];

        Scara2D scara = new(Mm(500), Mm(500));


        for (int axis = 0; axis < motionSystem.AxesLinear.Count; axis++)
        {
            Duration startTime = Duration.Zero;
            Length startDistance = Length.Zero;

            allTimes.Add([]);
            allPositions.Add([]);
            allVelocities.Add([]);
            allAccelerations.Add([]);
            allMotorAngles.Add([]);
            allMotorVelocities.Add([]);
            allPositions2.Add([]);

            List<double> times = allTimes[axis];
            List<double> positions = allPositions[axis];
            List<double> velocities = allVelocities[axis];
            List<double> accelerations = allAccelerations[axis];

            foreach (MotionBlock block in motionSystem.MoveQueue)
            {
                foreach (MotionSegment segment in block.Segments)
                {
                    Length d = Length.Zero;

                    Speed vInitial = segment.VelocitiesStartLinear[axis];
                    Acceleration a = segment.AccelerationsLinear[axis];

                    const int POINT_COUNT = 10;
                    for (int i = 0; i <= POINT_COUNT; i++)
                    {
                        Duration timeElapsed = ((double)i / (double)POINT_COUNT) * segment.Duration;
                        Duration t = startTime + timeElapsed;
                        Speed v = vInitial + timeElapsed * a;
                        d = startDistance + (vInitial * timeElapsed) + (0.5 * a * timeElapsed * timeElapsed);
                        times.Add(t.Seconds);
                        positions.Add(d.Millimeters);
                        velocities.Add(v.MillimetersPerSecond);
                        accelerations.Add(a.CentimetersPerSecondSquared);
                    }

                    startTime += segment.Duration;
                    startDistance = d;
                }
            }
        }

        for (int i = 0; i < allTimes[0].Count; i++)
        {
            double x = allPositions[0][i];
            double y = allPositions[1][i];
            (Angle angleProximal, Angle angleDistal) = scara.InverseKinematics(Mm(x), Mm(y));
            allMotorAngles[0].Add(angleProximal.Degrees);
            allMotorAngles[1].Add(angleDistal.Degrees);
            (Length x2, Length y2) = scara.ForwardKinematics(angleProximal, angleDistal);
            allPositions2[0].Add(x2.Millimeters);
            allPositions2[1].Add(y2.Millimeters);

            double vx = allVelocities[0][i];
            double vy = allVelocities[1][i];
            (RotationalSpeed speedProximal, RotationalSpeed speedDistal) = scara.InverseSpeeds(MmPerS(vx), MmPerS(vy), angleProximal);
            allMotorVelocities[0].Add(speedProximal.DegreesPerSecond);
            allMotorVelocities[1].Add(speedDistal.DegreesPerSecond);
        }

        for (int axis = 0; axis < motionSystem.AxesLinear.Count; axis++)
        {
            List<double> Times = allTimes[axis];
            List<double> Positions = allPositions[axis];
            List<double> Velocities = allVelocities[axis];
            List<double> Accelerations = allAccelerations[axis];
            List<double> MotorAngles = allMotorAngles[axis];
            List<double> MotorVelocities = allMotorVelocities[axis];
            List<double> Positions2 = allPositions2[axis];

            string axisName = motionSystem.AxesLinear[axis].Name;

            List<EpsilonPlotter.DataSeries> series = [
                new EpsilonPlotter.DataSeries() {X = Times, Y = Positions, Legend = axisName + " Position (mm)"},
                new EpsilonPlotter.DataSeries() {X = Times, Y = Velocities, Legend = axisName + " Velocity (mm/s)"},
                new EpsilonPlotter.DataSeries() {X = Times, Y = Accelerations, Legend = axisName + " Acceleration (cm/s^2)"},
                new EpsilonPlotter.DataSeries() {X = Times, Y = MotorAngles, Legend = axisName + " Motor Angle (deg)"},
                new EpsilonPlotter.DataSeries() {X = Times, Y = MotorVelocities, Legend = axisName + " Motor Velocity (deg/s)"},];

            // every axis gets a plot
            EpsilonPlotter.EpsilonPlot plot = new()
            {
                AxisTitleHorizontal = "Time (s)",
                AxisTitleVertical = "Values",
                FullFilePath = $"C:\\Users\\binte\\Downloads\\{axisName} Axis Values.png",
                Title = "",
                Series = series,
            };

            plot.Save(1400, 800);
            AxisGraphs.Add(plot.FullFilePath);
        }



        //EpsilonPlotter.Helpers.PlotSegments(allTimes, allPositions, allVelocities, allAccelerations, allMotorAngles, allMotorVelocities, "C:\\Users\\binte\\Downloads", "Plot2", 1400, 800);

        //AxisGraphs.Add("C:\\Users\\binte\\Downloads\\Plot2-Axis0.png");
        //AxisGraphs.Add("C:\\Users\\binte\\Downloads\\Plot2-Axis1.png");
        */
    }

}