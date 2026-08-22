# Primary Classes {#md_epsilon_firmware_epsilon_core_primary_classes}

## EpsilonEngine {#md_epsilon_firmware_epsilon_core_primary_classes_epsilonengine}
This is the main class which runs the main forever loop running each machine by receiving commands from the user interface, queueing commands to the corresponding [MachineQueue](@ref EpsilonCore::Engine::MachineQueue), sending and receiving [DataPacket](@ref EpsilonCore::Communication::DataPacket)s to/from microcontroller [Board](@ref EpsilonCore::Boards::Board)s. [EpsilonEngine](@ref EpsilonCore::Engine::EpsilonEngine) contains exactly one [MachineQueue](@ref EpsilonCore::Engine::MachineQueue) object per machine. It manages the limited computation of the host, distributing it across all the machines that the host is responsible for. If a machine is idle, it requires very little computational power so the host can devote more effort to other machines.

## MachineQueue {#md_epsilon_firmware_epsilon_core_primary_classes_machinequeue}
The [MachineQueue](@ref EpsilonCore::Engine::MachineQueue) class manages the lifecycle of [ICommand](@ref EpsilonCore::Commands::ICommand)s:
1. Queued to a machine.
2. Ready to send to microcontrollers.
3. Sent to microcontrollers.
4. Resolved and discarded.

Because [EpsilonCore](@ref md_epsilon_firmware_epsilon_firmware_architecture_epsiloncore) uses many [immutable records](@ref md_epsilon_firmware_epsilon_firmware_architecture_immutable_records) and each [ICommand](@ref EpsilonCore::Commands::ICommand) has a [ICommand.ResultantMachine](@ref EpsilonCore::Commands::ICommand::ResultantMachine) the [MachineQueue](@ref EpsilonCore::Engine::MachineQueue) class also manages the chain of resultant machines including the resultant machine of the last queued command and the last resolved command.

## ICommand {#md_epsilon_firmware_epsilon_core_primary_classes_icommand}
There are many different types of commands such as [MoveCommand](@ref EpsilonCore::Commands::MotionCommands::MoveCommand) and [SetTargetTempCommand](@ref EpsilonCore::Commands::ThermalCommands::SetTargetTempCommand) and they all inherit from [ICommand](@ref EpsilonCore::Commands::ICommand). 

### Command Duration {#md_epsilon_firmware_epsilon_core_primary_classes_command_duration}
Some commands have known a duration such as a 1 second pause or a move command (since the step pulse times are calculated before sending to the microcontroller) but some have an unknown duration such as a homing command or a command to wait for a heater to reach a given temperature. Every command with an unknown duration relies on a measurement of a sensor in order to fully resolve. This is important because a machine can have any action cancelled and the recovery procedure will require the host-side firmware to figure out where any microcontrollers were in their execution of the commands they have queued.

