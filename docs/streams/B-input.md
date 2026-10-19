# Stream B: Input providers (notes)

Branch: `worktree-agent-ae2905c299e5a6360` (stream B). Contract: `docs/ARCHITECTURE.md` §4.B, §2.3, §2.12.

## Status

| Piece | State |
|---|---|
| `AppInputBridge` (keys + preview wheel/buttons) | done, unit-tested (pure helpers + STA event raising, no devices) |
| `RawMouseWheelProvider` | done, unit-tested helpers only; NOT run on hardware |
| `XInputProvider` | pending |
| `ContourShuttleProvider` | pending |

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

## How to test

`dotnet test tests/IluPrompter.Input.Tests`. Tests use pure helpers, fakes, and WPF elements on a private STA
thread. No test registers Raw Input, opens HID or polls XInput.
