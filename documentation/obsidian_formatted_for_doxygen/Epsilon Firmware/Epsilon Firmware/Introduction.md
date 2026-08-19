# Introduction {#md_epsilon_firmware_epsilon_firmware_introduction}

Epsilon Firmware is a firmware intended to operate machines such as 3D printers, pen plotters, or CNC machines. It takes inspiration from RepRap Firmware and Klipper Firmware with several additional features. The motivation for making new firmware came when I tried building a large, complex 3D printer and found it difficult to set up the machine to support the complex behavior using just G-code. One of the primary goals of Epsilon Firmware is to make complex customization easier. 

Note: Epsilon Firmware is still early in development and large architectural code changes are still happening.

## Architecture {#md_epsilon_firmware_epsilon_firmware_introduction_architecture}
Similar to Klipper, Epsilon Firmware is split into two sections, the host side (written in C#) and the microcontroller / MCU side written in C++. The host can control one or more machines, each with one or more microconrollers.
