# Architecture {#md_epsilon_firmware_epsilon_firmware_architecture}

## Visual Studio Projects {#md_epsilon_firmware_epsilon_firmware_architecture_visual_studio_projects}
### EpsilonCore {#md_epsilon_firmware_epsilon_firmware_architecture_epsiloncore}
This is a Visual Studio project which contains most of the firmware code on the host side (not microcontroller code). It compiles into a .dll file so that multiple types of user interfaces can be used.

#### Immutable Records {#md_epsilon_firmware_epsilon_firmware_architecture_immutable_records}
EpsilonCore uses many immutable records and the core idea is that each machine can receive commands and each command will result in a new machine. For example, a move command will result in a new machine where the axes are at a different position and a set temperature command will result in a machine where a heater has a different target temperature. Not everything about a machine's state can be predicted solely by the commands sent to it, such as a heater's actual temperature. Consequently, this value must be continually measured by a thermometer to keep the system state updated. Measured values like this are kept in a mutable portion of memory because there is never a situation where we need to revert to a previous measurement. Conversely, if some commands are queued and sent to a microcontroller and then those commands are cancelled, we do need to be able to revert to a previous machine state since those commands will no longer be applied. Immutable objects make reverting to a previous machine state trivial.

##### Performance Analysis {#md_epsilon_firmware_epsilon_firmware_architecture_performance_analysis}
I'm worried about heap churn for these relatively short lived machine state records.

2 hr 30 min print (9000 seconds)(total of about 400440 G-code commands):
G1    231943 line move (25.7 linear moves per second)
G3    84435 arc move (9 arc moves per second)
M204  29864 set acceleration
M106  20583 set speed of fan
G2    16529 arc move (1.8 arc moves per second)
G17   11219 set XY plane of arc move
M622  1326 conditional statement begin
M1002 681 update LCD

total of 332907 moves
total of 36 moves per second

### EpsilonDesktop {#md_epsilon_firmware_epsilon_firmware_architecture_epsilondesktop}
This is a Visual Studio project which contains a desktop interface for Epsilon Firmware. It compiles into an .exe file which can run on Windows. It is a useful tool for debugging EpsilonCore because it can run with the Visual Studio debugger on the same machine used to develop code so you don't have to use a web interface for most debugging.

## UnitsNetHelpers {#md_epsilon_firmware_epsilon_firmware_architecture_unitsnethelpers}
This is a Visual Studio project which contains helper functions and classes to extend the [UnitsNet](https://github.com/angularsen/UnitsNet) library. It compiles into a .dll file.

## EpsilonPlotter {#md_epsilon_firmware_epsilon_firmware_architecture_epsilonplotter}
This is a Visual Studio project which contains code to generate plots for EpsilonCore. It was mainly created for debugging purposes and may be removed at some point but if plots are used more within Epsilon Firmware then it may survive. It compiles into a .dll file.
