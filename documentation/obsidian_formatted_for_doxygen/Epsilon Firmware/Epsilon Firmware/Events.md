# Events {#md_epsilon_firmware_epsilon_firmware_events}

The C# event system allows users to make more interesting machines because they can have code trigger when special conditions are met. The event system is also used so the user interface can be notified when the machine changes states (For example, when a new hotend is added or the bed changes temperature). This is powerful because the machine's current state can be read inside the event handler.

## Event Types
- CommandQueued
- CommandSent
- CommandResolved
- 
