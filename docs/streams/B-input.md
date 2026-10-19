# Stream B: Input providers (notes)

Branch: `worktree-agent-ae2905c299e5a6360` (stream B). Contract: `docs/ARCHITECTURE.md` §4.B, §2.3, §2.12.

## Status

| Piece | State |
|---|---|
| `AppInputBridge` (keys + preview wheel/buttons) | done, unit-tested (pure helpers + STA event raising, no devices) |
| `RawMouseWheelProvider` | done, unit-tested helpers only; NOT run on hardware |
| `XInputProvider` | done, unit-tested helpers only; NOT run on hardware |
| `ContourShuttleProvider` | done, unit-tested helpers only; NOT run on hardware |

## Decisions

- **Key chords.** `KeyChordFormatter` is pure (Key + ModifierKeys). `Key.System` is replaced by `SystemKey`;
  pure Ctrl/Alt/Shift/Win presses, `ImeProcessed`, `DeadCharProcessed` and anything pressed with the Windows key held
  are ignored. Aliased `Key` members are pinned: `Return`->`Enter`, `Prior`->`PageUp`, `Next`->`PageDown`,
  `Capital`->`CapsLock` (Enum.ToString does not promise which alias wins). Settings must use these spellings.
- **Key down and up.** Down is submitted with `IsPressed=true, IsRepeat=e.IsRepeat`. Up is submitted with
  `IsPressed=false` (contract §2.12 says Down/Up; the router acts on presses only). If a press was handled, its
  release is also marked handled (so a handled Space never reaches a focused Button as a lone KeyUp).
- **Typing context.** `Keyboard.FocusedElement` is `TextBoxBase`, `PasswordBox` or editable `ComboBox` -> DeviceId
  `app:text`, else `app`.
- **Wheel.** `Delta / 120.0`, never rounded (test raises a real `MouseWheelEventArgs` of 30 and sees 0.25).
  Only Middle / XButton1 / XButton2 are forwarded; Left/Right are left to WPF.

- **Raw Input mouse.** `RawMouseWheelProvider` registers usage page 1 / usage 2 only, flags `InputSink | DevNotify`,
  on a message-only `HwndSource` (thread "ILU RawInput"). Per record it reads the small header first
  (`User32.GetRawInputDataHeader`) and drops other devices' records before parsing (`GetRawInputMouseData`),
  instead of `RawInputData.FromHandle`, which also resolves the device (name lookup) for every mouse move.
  `deviceId` null/blank means "no dedicated mouse": it does not start and reports `NotFound` with the detail
  "Choose the prompter mouse in Settings" (a "first available mouse" would be the operator's own mouse and would
  scroll the prompter from any window). Horizontal wheel is ignored; only Middle/XButton1/XButton2 edges are sent.
  Fallback for Phase-0 S4b (no `WM_INPUT` unfocused): set env `ILU_RAWINPUT_TOOLWINDOW=1` to use a zero-size,
  never-shown top-level tool window instead of the message-only window; no code change needed.

- **XInput.** Thread "ILU XInput", `Thread.Sleep(8)`. Slot probing at most 1/s per slot (initial probe is immediate).
  `deviceId` null or malformed = first connected pad (-1). Connect: buttons already held are the baseline (no press
  edges), all six axes are emitted once, status `Connected` with battery. `PacketNumber` unchanged = no work.
  Axis events: change > 0.002, plus a return to exactly 0 always fires (so a released stick/trigger never keeps a
  tiny residue; one trigger step is 0.0039 so triggers fire on every step anyway). Disconnect (`GetState` false) ->
  `Disconnected` status, then 1 s probing for reconnect. Switching device or stopping emits zero for the old pad's
  non-zero axes so the router's slots are not left "held". Guide is not exposed by documented XInput, so not mapped.
  Battery re-read every 30 s; a change re-reports `Connected` with the new level.

- **Contour.** HidSharp, VID/PID 0B33:0020 (ShuttleXpress), 0B33:0030 (ShuttlePRO v2), 05F3:0240 (ShuttlePRO v1).
  `ContourShuttleReportParser.TryParse` takes 5 bytes, or 6+ with a leading 0x00 report id; anything else is
  rejected (logged once). `ShuttleReportTracker` is the pure state machine: the first report after each open is
  the baseline (ring emitted once, no jog event, held buttons do not fire); then ring on change (k/7), jog as the
  wrapped `(sbyte)(cur-prev)`, button edges diffed per raw bit and mapped via `ContourModels`.
  **Button maps to verify on hardware:** ShuttleXpress raw bits 4..8 = buttons 1..5 (from the LinuxCNC
  `shuttle.c` report description, not a unit in hand); ShuttlePRO v1/v2 raw bits 0..14 = buttons 1..15; any other
  bit passes through as bit+1. A specific `DeviceId` that is not attached reports `NotFound` (it never silently
  uses another unit); `null` = first Contour device. Open failure -> `NotFound` with Detail "Close the Contour
  ShuttlePro software, then reconnect". `DeviceList.Local.Changed` wakes the loop to reconnect; read loop ends on
  IOException/unplug -> `Disconnected`.
- **Stubs.** `Providers.Stubs.cs` is deleted; no `STUB` markers remain in `src/IluPrompter.Input`.

## Hardware verification still needed (not done: Defender rule, ask the user first)

1. Raw Input INPUTSINK on the message-only window actually delivers `WM_INPUT` while another app is focused
   (Phase-0 S4b); otherwise set `ILU_RAWINPUT_TOOLWINDOW=1` or switch the code default.
2. `RawInputDevice.DevicePath` for the chosen mouse equals what `EnumerateDevices` listed, and the
   removal/arrival handle round trip (`WM_INPUT_DEVICE_CHANGE`) reports Disconnected/Connected.
3. XInput triggers/sticks/buttons unfocused; unplug mid-scroll shows Disconnected (router ramps to 0 in 0.3 s).
4. Contour ring/jog/buttons unfocused; button numbering on both models; behaviour when Contour's own software holds
   the device.

## How to test

`dotnet test tests/IluPrompter.Input.Tests`. Tests use pure helpers, fakes, and WPF elements on a private STA
thread. No test registers Raw Input, opens HID or polls XInput.
