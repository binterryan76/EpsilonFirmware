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
using GenericHelpers;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using UnitsNet;
using System.Linq;

MotionSystemPrecisions precisions = new(
    Length.FromMillimeters(0.0001),
    Angle.FromDegrees(0.001),
    Duration.FromMicroseconds(10));

Machine mainMachine = new(
    "Binter Printer",
    DisplayUnits.DefaultMetric,
    TextboxResultLogger,
    precisions);

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


Engine.AddMachineQueue(mainMachine);

uint lineNumber = 1;

BttOctopusProMacros.AddBttOctopusProBoardAndPinsMacro(
    Engine,
    mainMachineId,
    "BTT Octopus Pro V1.0",
    mainCommunicatorId,
    lineNumber++);

Engine.CompleteAllCommands(mainMachineId);

IPin endstopPinX = Engine.GetCurrentMachine(mainMachineId)!.Entities.Pins.Values.First();
IPin endstopPinY = Engine.GetCurrentMachine(mainMachineId)!.Entities.Pins.Values.Last();
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

Engine.EnqueueCommands(mainMachineId, commands);
Engine.CompleteAllCommands(mainMachineId);