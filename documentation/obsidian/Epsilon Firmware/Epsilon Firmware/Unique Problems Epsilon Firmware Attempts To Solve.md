## Problems with G-code
- G-code is difficult to read because there are so many unique commands which aren't always the same from machine to machine. You probably don't know what `M584 X0 Y1 Z2:3` does off the top of your head.
- G-code was created to be as concise as possible while still being human readable with training to save bytes on old, slow computers (some CNC machines from the 1970s are still running). Computers are much faster now so this performance advantage is negligible. 
- Using meta-G-code commands is difficult because the G-code language wasn't designed to support such complex situations. Some machines require the use of meta G-code commands which can be used like a traditional programming language to store data in variables, perform loops, conditional logic, etc.

### Solution
We address all these issues by exposing C# scripting so the machine can be controlled with actual C# scripts which supports far more than meta-G-code commands ever could. For example, you can create strongly typed functions, and custom data types. We can then create a library of functions to control machines with a more readable language. Most slicers only output G-code so handling G-Code is still required but code that is written by hand (such as macros) should use a simpler, easier-to-read language and I suggest that C# should be that language despite it adding some syntax overhead.

#### Ideal Syntax
G-code would ideally look roughly like this:
```C#
Machine.EnqueueCommands(
[
	Home(x, y),
	Move(x:10, y:20, speed:100),
	Move(x:15, y:30),
];
```

## Stop Methods
- Hardware emergency stop
- Software emergency stop
- Pause
- Cancel current job
## Other Issues
- Doing a Z baby step move during a single slow print move will not occur until the move finishes.
	- We address this by allowing some commands to be processed immediately when received.
- Once a print starts, you cannot change the infill percentage.
	- We address this by integrating a slicer into the firmware so you can splice two G-code files together during a print and switch to an updated one once the current layer finishes.
- Making a 5 axis printer is a chicken and egg problem. Firmware developers don't want to support 5 axis machines when 5 axis slicers don't exist yet and conversely, slicer developers don't want to support 5 axis machines when 5 axis firmware doesn't exist yet.
	- We address this by supporting 5 axis kinematics.
- Configuring a machine with unique kinematics is difficult.
	- We address this by supporting composite kinematic systems where commands can be used to set up a kinematic system which is a combination of other kinematic systems. Adding a new base kinematic system is as simple as creating a new C# record which implements the IKinematics interface.
- Customizing firmware source code is confusing and difficult.
	- We address this by being picky about code readability and having excellent documentation.
- Using the daemon.gcode in RepRap Firmware is slow.
	- We address this by using the C# [[Event System]].
- Making a machine that does certain things when some conditions are met is difficult.
	- We address this by using the C#  [[Event System]].