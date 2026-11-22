This page describes the messages sent between the host ([[EpsilonEngine]]) and the microcontroller boards. The board firmware (C++, not in this repository) must implement it exactly. The command codes are defined in [[CommandCode]].

## Packet Layout
Every message is a [[DataPacket]]:

| Byte | Meaning |
|---|---|
| 0 | Data length: number of bytes after this one (2 to 255) |
| 1 | Sequence number |
| 2 | Command code |
| 3+ | Payload |

All multi-byte numbers are little endian. Unless stated otherwise, doubles are 8 byte IEEE doubles in communication units (mm, mm/s, radians, kelvin, seconds; see `CommunicationUnits`).

## Three Kinds of Commands
Commands from the host fall into three groups, decided by the command code range.

| Range | Kind | Examples |
|---|---|---|
| 1 – 127 | Queued commands | `MOVE`, `MOVE_INTO_ENDSTOPS`, `SET_TARGET_TEMP`, `WAIT_FOR_TEMP`, `BLINK_PIN` |
| 128 – 191 | Immediate commands | `EMERGENCY_STOP`, `PAUSE`, `RESUME`, `CLEAR_FAULTS`, `BABYSTEP`, `STATUS_QUERY`, `CLOCK_SYNC_REQUEST` |
| 240 – 255 | Messages from the board | `COMMAND_COMPLETE`, `COMMAND_RESULT`, `BUFFER_BYTES_FREED`, `STATUS_REPORT`, `CLOCK_SYNC_RESPONSE`, `DISPLAY_TEXT`, `ERROR` |

On the host these correspond to [[IScheduledCommand]] (known duration), [[IConditionalCommand]] (unknown duration) and [[IImmediateCommand]].

### Queued Commands
- Stored in the board's command buffer and run in the order received.
- Every queued packet has a unique sequence number (per board, wrapping at 255). The host never has more than 256 packets in flight per board.
- The host inserts a 9 byte **timing header** at the start of the payload ([[CommandTiming]]), so the command's own payload starts at byte 12:

| Byte | Meaning |
|---|---|
| 3 | Timing mode: 0 = `AfterPrevious`, 1 = `AtClock` |
| 4 – 11 | uint64 value in board clock ticks |

  - `AfterPrevious`: start this many ticks after the previous queued command finishes. Used before the clocks are synchronized and while a conditional command is running.
  - `AtClock`: start when the board's clock reaches this tick. If that tick has already passed when the command reaches the front of the queue, start immediately.
- When a command finishes, the board sends either `COMMAND_COMPLETE` (scheduled commands) or `COMMAND_RESULT` (conditional commands) with the same sequence number.
- Buffer space is reported separately with `BUFFER_BYTES_FREED`. The host only sends a queued packet if it believes the whole packet (including the timing header) fits in the buffer.

### Immediate Commands
- Acted on as soon as they are received, even if the command buffer is full. They never enter the buffer.
- No timing header. The sequence number is 0 and is ignored.
- Never answered with `COMMAND_COMPLETE`.

| Code | Name | Payload | Board behaviour |
|---|---|---|---|
| 128 | `EMERGENCY_STOP` | none | Stop all motors, turn heaters off, set pins to safe states, discard the command buffer, report `EmergencyStopped` and ignore queued commands until `CLEAR_FAULTS`. |
| 129 | `PAUSE` | none | Finish the command currently running, discard the rest of the buffer without running it, send `COMMAND_COMPLETE`/`COMMAND_RESULT` for the finished command, then send a `STATUS_REPORT` with state `Paused`. Ignore queued commands until `RESUME`. |
| 130 | `RESUME` | none | Leave the paused state and accept queued commands again. |
| 131 | `CLEAR_FAULTS` | none | Leave the `EmergencyStopped`, `Faulted` or `Paused` state with an empty command buffer. |
| 132 | `BABYSTEP` | actuator id (uint32), 1 byte (1 = linear), offset (double) | Shift the actuator by the offset on top of whatever it is running. |
| 133 | `STATUS_QUERY` | none | Send a `STATUS_REPORT`. |
| 134 | `CLOCK_SYNC_REQUEST` | none | Reply with `CLOCK_SYNC_RESPONSE` as quickly as possible. |

Because the board doesn't send `BUFFER_BYTES_FREED` for commands it discards (pause, emergency stop, fault), the host resets its buffer count to zero in those cases.

## Messages From the Board

| Code | Name | Payload |
|---|---|---|
| 248 | `CLOCK_SYNC_RESPONSE` | uint64 board clock ticks, read as close to sending as possible |
| 249 | `STATUS_REPORT` | 1 byte state (0 `Running`, 1 `Paused`, 2 `EmergencyStopped`, 3 `Faulted`), 1 byte flag (1 if any queued command has finished), 1 byte sequence number of the last finished queued command |
| 250 | `COMMAND_RESULT` | 1 byte status (0 `Success`, 1 `TimedOut`, 2 `Failed`), uint64 completion tick, command specific data |
| 251 | `BUFFER_BYTES_FREED` | uint32 number of buffer bytes freed |
| 253 | `DISPLAY_TEXT` | ASCII text |
| 254 | `ERROR` | 1 byte [[BoardErrorCode]], optionally uint32 line number and file name |
| 255 | `COMMAND_COMPLETE` | optionally uint64 completion tick |

The sequence number of `COMMAND_COMPLETE` and `COMMAND_RESULT` must be the sequence number of the queued packet that finished.

If the board stops on its own (for example a hardware emergency stop button), it should send a `STATUS_REPORT` with `EmergencyStopped` or `Faulted`. The host then stops the machine queue (and forwards the emergency stop to the other boards).

If a conditional command fails (`TimedOut` or `Failed`), the board should discard the rest of its buffer and enter the `Faulted` state. The host pauses the other boards and waits for the user to clear faults.

## Clock Synchronization
The board keeps a free-running 64-bit tick counter at its `Board.ClockFrequency`. The host sends `CLOCK_SYNC_REQUEST` about every second (faster at first) and estimates the board clock from the replies (see `ClockSync`). Once a board is synchronized, queued commands use `AtClock` timing so commands that span several boards start at the same moment.

## Command Payloads

### `MOVE_INTO_ENDSTOPS` (2)
Payload (after the timing header): timeout (double, seconds), 1 byte entry count, then for each endstop:
1 byte endstop pin id, 1 byte inverted flag, actuator id (uint32), 1 byte (1 = linear actuator), 1 byte direction (0 negative, 1 positive), homing speed (double).

The board moves each actuator in its direction at its homing speed until its endstop triggers, or fails with `TimedOut`.

Result data: 1 byte entry count, then for each endstop: 1 byte pin id, 1 byte (1 if triggered).

### `WAIT_FOR_TEMP` (21)
Payload: 1 byte thermometer pin id, min temp (double, kelvin), max temp (double, kelvin), timeout (double, seconds).

The board waits until the thermometer reads between min and max, or fails with `TimedOut`. Queued commands after it wait in the buffer until it finishes.

Result data: optionally the measured temperature (double, kelvin).

### `SET_TARGET_TEMP` (20)
Payload: target temperature (double, kelvin).

### `BLINK_PIN` (100)
Payload: 1 byte pin id, uint32 blink count, high time (float, seconds), low time (float, seconds).
