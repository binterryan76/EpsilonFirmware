# README {#md_epsilon_firmware_readme}

## Introduction {#md_epsilon_firmware_readme_introduction}
Epsilon Firmware is a firmware intended to operate machines such as 3D printers, pen plotters, or CNC machines. It takes inspiration from RepRap Firmware and Klipper Firmware with several additional features. The motivation for making new firmware came when I tried building a large, complex 3D printer and found it difficult to set up the machine to support the complex behavior using just G-code. One of the primary goals of Epsilon Firmware is to make complex customization easier. 

Note: Epsilon Firmware is still early in development and large architectural code changes are still happening.

To understand why Epsilon Firmware is being made, see [Unique Problems Epsilon Firmware Attempts To Solve](@ref md_epsilon_firmware_epsilon_firmware_unique_problems_epsilon_firmware_attempts_to_solve)

## Architecture {#md_epsilon_firmware_readme_architecture}
Similar to Klipper, Epsilon Firmware is split into two sections, the host side (written in C#) and the microcontroller / MCU side written in C++. The host can control one or more machines. Each machine can have one or more microcontrollers. The main application is the user interface which creates an instance of the EpsilonEngine class. 
