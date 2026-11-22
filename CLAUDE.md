# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Epsilon Firmware is host-side firmware (C#, .NET 10) for machines like 3D printers, pen plotters and CNC machines, inspired by Klipper/RepRap. Like Klipper, it is split into a host side and an MCU side (C++); **only the host side lives in this repo**. The project is early in development and large architectural changes are still happening.

## Commands

The solution file is `src/EpsilonFirmware.slnx` (new XML solution format; needs a recent .NET SDK).

```sh
dotnet build src/EpsilonFirmware.slnx
dotnet run --project src/EpsilonDesktop        # WPF app, Windows only
```

There is no test project and no linter configured.

Documentation (Doxygen + Obsidian markdown) is generated with `documentation/doxygen/GenerateDocs.bat`, run from inside `documentation/doxygen` (needs doxygen, GraphViz `dot`, and python3 on PATH). It rewrites wikilinks in `documentation/obsidian` in place, regenerates `documentation/obsidian_formatted_for_doxygen` (generated, don't hand-edit), then runs doxygen. `doxygen_record_filter.py` is a Doxygen input filter that turns C# records/primary constructors into classes because Doxygen can't parse them.

## Projects

- **EpsilonCore** (`net10.0`, library): almost all firmware logic. `GenerateDocumentationFile` is on, so public members without XML doc comments produce CS1591 warnings.
- **EpsilonDesktop** (`net10.0-windows`, WPF, CommunityToolkit.Mvvm): debugging/desktop UI. MVVM with `ViewModels/` and `Views/` (UserControls).
- **EpsilonPlotter**: ScottPlot-based plotting, mainly for debugging.
- **GenericHelpers**: `Result<T>`, `Option`, `OptionalResult` types.
- **UnitsNetHelpers**: extensions to the UnitsNet library (e.g. `ReciprocalDuration`, `Duration2`).

## Architecture

### Immutable machine state
`Machine` (and most of its contents: `Entities`, `MotionSystem`, axes, actuators, boards) are immutable records. Each command applied to a machine produces a **new** `Machine`. This makes cancelling sent-but-unexecuted commands trivial: revert to an earlier `Machine`. Measured values (e.g. actual heater temperature) are held in mutable state since they never need reverting. `ICommunicator`s are mutable, so they live on `MachineQueue` in the engine rather than in `Machine`.

Even machine setup is done with commands (`Commands/SetupCommands/Add*Command`: add a board, pin, axis, kinematics, endstop, and so on), not by mutating a `Machine`.

### Commands
Every command implements `ICommand.EnqueueCommandSpecific(Machine initialMachine)`, which validates against the initial machine and returns a `QueuedCommand` that holds the `ErrorLevel`, a message, the `ResultantMachine`, and the `DataPacket`s to send to the MCU(s). Sequence numbers are **not** set there; they are assigned just before sending. `RequiresZeroVelocity` forces the motion planner to bring the machine to a stop first. Commands that come from a program carry an optional `LineNumber`.

### Engine and command lifecycle
- `EpsilonEngine` owns one `MachineQueue` per machine (`ConcurrentDictionary<uint, MachineQueue>`) and runs a single background `MainLoop` thread, started on the first `AddMachineQueue`. The loop sleeps when every queue is idle or stopped.
- `MachineQueue` moves commands through these stages: `CommandsToEnqueue` (thread-safe inbox) → `Queued` (step times not solved yet, waits in `MoveQueue`) → `ReadyToSend` → `DataPacketsReadyToSend` → `Sent` → resolved. It tracks both `LatestQueuedMachine` and `CurrentMachine`.
- Events `CommandQueued`, `CommandSent`, `CommandResolved`, and `MachineQueueAdded` are how the UI (and user scripts) observe state changes.
- One command can produce data packets for several boards. All of them must have buffer space before any packet for that command is sent.

### Communication protocol
`DataPacket` byte layout: `[0]` data length (max 255), `[1]` sequence number, `[2]` `CommandCode`, then the payload. `CommandCode` values must fit in a byte; they must stay in sync with the MCU-side C++ firmware. `SerialCommunicator` (System.IO.Ports) implements `ICommunicator`. Incoming packets come in through the `DataPacketsReceivedHandler` event and are handled in `EpsilonEngine.ProcessDataPacket`.

### Units
Physical quantities use UnitsNet types throughout (`Length`, `Angle`, `Duration`, `RotationalSpeed`, ...), not raw doubles. `Units/` defines `DefaultUnits`, `CommunicationUnits` (units used when encoding to MCU bytes) and `DisplayUnits`.

### Error handling
Fallible operations return `GenericHelpers.Result<T>` (implicitly convertible from a value or an `Exception`) or `OptionalResult`, not exceptions. Check `IsSuccess` before reading `.Value`; the accessors throw on the wrong branch. User-facing messages go through `IResultMessageLogger`.

### Desktop scripting
EpsilonDesktop configures machines with **C# scripts** run through Roslyn (`CSharpScript.RunAsync` in `MainViewModel`), using `ScriptGlobals` as globals (exposes `Engine` and `TextboxResultLogger`). A startup script runs on launch; its path is `AppSettings.StartupFilePath`, stored in `%APPDATA%/.../settings.json` (see `EpsilonDesktop/Constants.cs`). `sample_data/StartupFile.cs` is an example startup script that builds a SCARA machine on a BTT Octopus Pro. It is not compiled with the solution.

## Documentation

Design notes live in the Obsidian vault at `documentation/obsidian/Epsilon Firmware/` (Architecture, Primary Classes, Event System, Unique Problems). In those notes, link to classes as `[[ClassName]]` and to members as `[[ClassName#MemberName]]`.

# Code Styling
- Prefer `Regex myRegex = new();` over `var myRegex = new Regex();`
- In each file order code elements as follows:
  - Enums
  - Records
  - Structs
  - Classes
  - Constants
  - Member variables
  - Properties
  - Functions (in depth first search order)
- Use XML documentation on every class, record, enum, function, and property. 
	- Include descriptions of the input and output variables.
	- List any specific exception types that could be thrown by the function.
	- Use `<see cref="member"/>` tags to reference other code members so renaming the member updates the XML documentation.