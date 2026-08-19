# Architecture {#md_epsilon_firmware_epsilon_core_architecture}

## EpsilonEngine {#md_epsilon_firmware_epsilon_core_architecture_epsilonengine}
This is the main class which runs main loop receiving commands, queueing commands, sending data packets, and receiving data packets for every machine. EpsilonEngine contains multiple MachineQueue objects.
