## EpsilonEngine
This is the main class which runs the main forever loop running each machine by receiving commands from the user interface, queueing commands to the corresponding [[EpsilonCore::Engine::MachineQueue|MachineQueue]], sending and receiving [[EpsilonCore::Communication::DataPacket|DataPacket]]s to/from microcontroller [[EpsilonCore::Boards::Board|Board]]s. [[EpsilonCore::Engine::EpsilonEngine|EpsilonEngine]] contains exactly one [[EpsilonCore::Engine::MachineQueue|MachineQueue]] object per machine. It manages the limited computation of the host, distributing it across all the machines that the host is responsible for. If a machine is idle, it requires very little computational power so the host can devote more effort to other machines.

## MachineQueue
The [[EpsilonCore::Engine::MachineQueue|MachineQueue]] class manages the lifecycle of [[EpsilonCore::Commands::ICommand|ICommand]]s:
1. Queued to a machine.
2. Ready to send to microcontrollers.
3. Sent to microcontrollers.
4. Resolved and discarded.

Because [[Epsilon Firmware/Architecture#EpsilonCore|EpsilonCore]] uses many [[Architecture#Immutable Records|immutable records]] and each [[EpsilonCore::Commands::ICommand|ICommand]] has a [[EpsilonCore::Commands::ICommand#ResultantMachine|ICommand.ResultantMachine]] the [[EpsilonCore::Engine::MachineQueue|MachineQueue]] class also manages the chain of resultant machines including the resultant machine of the last queued command and the last resolved command.

## ICommand
There are many different types of commands such as [[EpsilonCore::Commands::MotionCommands::MoveCommand|MoveCommand]] and [[EpsilonCore::Commands::ThermalCommands::SetTargetTempCommand|SetTargetTempCommand]] and they all inherit from [[EpsilonCore::Commands::ICommand|ICommand]]. 

### Command Duration
Some commands have known a duration such as a 1 second pause or a move command (since the step pulse times are calculated before sending to the microcontroller) but some have an unknown duration such as a homing command or a command to wait for a heater to reach a given temperature. Every command with an unknown duration relies on a measurement of a sensor in order to fully resolve. This is important because a machine can have any action cancelled and the recovery procedure will require the host-side firmware to figure out where any microcontrollers were in their execution of the commands they have queued.

