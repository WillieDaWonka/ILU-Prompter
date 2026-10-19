# Stream A: Core (engine, speed curves, router, settings)

Status: complete against ARCHITECTURE §4.A. Branch `worktree-agent-a254c1de72e39b8e9` (stream A). No WPF, no network, no
hooks, no app launch needed.

## What is here

| Area | File | Notes |
|---|---|---|
| Scroll engine | `src/IluPrompter.Core/Engine/ScrollEngine.cs` | Every row of the §2.4 behaviour table |
| Command ring | `src/IluPrompter.Core/Engine/EngineCommandQueue.cs` | Lock-free, allocation-free, bounded (16,384) |
| Input router | `src/IluPrompter.Core/Input/InputRouter.cs` | §2.3 router rules |
| Settings | `src/IluPrompter.Core/Settings/JsonSettingsService.cs` | §2.8, plus clamping on load |
| Maths | `src/IluPrompter.Core/Layout/LayoutMath.cs` | MarginMath, TypographyMath, SpeedCurve, AxisCurve, TimeEstimator |
| Tests | `tests/IluPrompter.Core.Tests/**` | ~300 xUnit tests, no real time in engine tests |

`DefaultBindings.cs`, `CoreServiceCollectionExtensions.cs` and all `Contracts/**` are unchanged. No `STUB` marker is left in
Core.

## How to test

```powershell
dotnet build IluPrompter.sln          # 0 warnings
dotnet test tests/IluPrompter.Core.Tests
```

- Engine tests use `EngineHarness` (`Support/TestSupport.cs`): a real `ScrollEngine`, a fake `ISettingsService`, and time as
  `Stopwatch` ticks derived from seconds. `Apply(commands)` = post + one tick 100 ns later (no motion), `Run(seconds)` =
  ticks of at most 1/60 s. Synthetic geometry is `TestGeometry.Create` (100 lines of 119.6 DIPs by default, reading line
  314, sections by paragraph index).
- Router tests use `RouterHarness` (`Support/RouterSupport.cs`): a recording `FakeEngine`, fake providers per kind,
  a real `KeyModeState`.
- Settings tests write to a temp directory and use the internal constructor with a short debounce. They are the only tests
  that wait on a real clock (debounce).
- The 10-minute soak (`EngineSoakTests`) runs 36,000 ticks per seed with random commands (including NaN, infinities and
  out-of-range indices), random relayouts, viewport changes and settings changes. It asserts finite, in-bounds positions and
  sane status on every tick.
- Zero-allocation checks use `GC.GetAllocatedBytesForCurrentThread` over windows of 1,000 ticks and require the quietest
  of up to 8 windows to be exactly 0. The runtime itself (tier-up, type loads) can allocate once on a thread in one window;
  a real per-tick allocation would show in every window.

## Decisions (read these if you integrate)

### Engine

1. **Tick order.** Each `Tick` first advances time over the *old* state (velocity ramp, auto-step, step animation), then
   applies queued commands, then publishes. A command posted before `Tick(t)` takes effect at `t`: ramps and animations
   start at `t`, cuts show on that very tick, and a step command moves nothing until the next tick.
2. **Position model.** `_pos` is the logical position (the destination of any running step animation); `_remaining` is the
   animation offset still to play. Published `P = clamp(_pos - _remaining, Pmin, Pmax)`. Step animations are therefore
   superposed on continuous motion, retargeting never jumps, and chaining three steps is one animation of `3 x` the
   distance. Step targets are computed from the *logical* line, so quick presses chain exactly.
3. **Velocity is kept in lines per minute**, not UI units, so ramps are linear in velocity (DESIGN "ramps are linear in
   velocity") and layout changes do not disturb the ramp. Duration is chosen when the target changes: from rest
   `RampStart`, to rest `RampStop`, otherwise `RampChange(Acceleration)`. A settings change to the curve is just a new target
   and ramps with `RampChange`.
4. **Curve scale.** The `maxSpeed` handed to `SpeedCurve` is `max(|Scroll.MaxSpeed|, |Scroll.MinSpeed|)` (10 by default).
5. **`dt`** is clamped to 0.1 s for everything (integration, ramps, animations, auto-step). The first tick has `dt = 0`.
   Timestamps not newer than the last tick are ignored.
6. **AtEnd.** Reached when the logical position reaches `Pmax` moving forward by any means (velocity, step, `ScrollPixels`,
   `SeekToOffset`, axis, cut). It cancels velocity and keeps `SetSpeed`. Any backward move leaves it for `Paused`.
   `Play` and `TogglePlayPause` are refused while AtEnd. After a relayout, AtEnd stays AtEnd at the new `Pmax`.
7. **Reaching `Pmin`** while moving backward clamps, cancels velocity and pauses a Playing prompt (also for steps and
   `ScrollPixels` that hit the start).
8. **Steps at the edges.** `StepLines` clamps to the first/last line (the last script line is as far as line steps go;
   END is reached by playing or `JumpToEnd`). `StepParagraphs(+n)` at the last paragraph does nothing. `StepSections(+n)`
   with no section ahead does nothing (no pause). `StepSections(-n)` before the first section, or with no sections at all,
   goes to the start. Multi-count steps repeat the single-step rule.
9. **Previous paragraph/section** use y = logical position + R. "Below its first line by more than half a line" is
   `y - LineCentre(firstLine) > 0.5 x firstLine height`.
10. **Pause at sections** works on the section's *target position* (`LineCentre(first line with top >= Section.Top) - R`),
    for Display (continuous, exact clamp, immediate stop, no ramp) and for Line auto-steps. It only applies while
    Playing forward and not under axis control. Starting exactly on a section line does not re-trigger.
11. **Line mode.** The first auto-step happens one full interval after Play. The interval is `60 / lpm(|speed|)`; the
    remainder carries over. At most 4 steps per tick. Reaching the last line steps on to `Pmax`. Axis speed sets the rate
    and direction (also while Paused); releasing stops stepping at once (no ramp in Line mode). Switching to Line cancels
    velocity and snaps to the nearest line centre with a `LineStep` animation.
12. **Fail-safe.** Display: clears axis slots and ramps to 0 over 0.3 s, then Paused (the prompt keeps moving, as planned,
    during the ramp). Line: pauses at once. A new `Play` or non-zero axis cancels a pending fail-safe.
13. **Relayout.** New key: anchor of the line on the reading line plus the fractional position in that line, mapped with
    `FindLineForAnchor`. Running animations snap to their destination first. The first layout after construction, or a
    layout with no lines, goes to `Pmin`. AtEnd goes to the new `Pmax`. Same key: only the viewport is applied
    (`P + R_old - R_new`); the old `LayoutGeometry` instance is kept. `SetLayout` also publishes the position and raises
    `StatusChanged` (on the calling thread) when the status changes.
14. **Status.** `LinesPerMinute` is signed (negative when reversing), `WordsPerMinute` is absolute (the contract helper uses
    `Math.Abs`). `LineIndex` etc. describe the *published* position (what is on screen), `IsAtEnd` is the transport state.
    `SecondsRemaining` uses `SetSpeed`; it is `0` with no lines and `Infinity` when the speed is 0 and lines remain.
    `StatusChanged` handlers that throw are swallowed so a faulty UI subscriber cannot stop the render loop.
15. **Commands.** `Post` writes into a 16,384-slot ring (lock-free, no allocation). If the ring is full it spins/yields
    briefly, then drops the command and counts it (`ScrollEngine.DroppedCommands`, internal). This only happens when no
    one is ticking. NaN and infinite command values are ignored; slots outside 0..7 are ignored; `NudgeSpeed` results are
    rounded to 6 decimals so repeated 0.1 nudges do not drift.
16. **Settings read per tick.** Speed limits, step sizes, curve, acceleration and `PauseAtSections` are read from
    `ISettingsService.Current` every tick/command, so changes apply live. `StartSpeed` and `DefaultMode` are read once, in the
    constructor: **resolve `IScrollEngine` after `ISettingsService.Load()`** (the startup order in §2.15 already does).

### Router

1. **Source filtering.** Keyboard is always accepted. Other kinds only when they are `SelectedController`. For
   `MouseWheel`, `DeviceId == "app"` counts only without a dedicated mouse and a Raw Input device only with one.
2. **Bound = handled.** `Submit` returns true whenever a binding exists in the current context, including key *releases*
   (so a focused control does not "click" on Space-up) and commands that are a no-op right now. Axes with `AxisRole.None`
   (left stick by default) and relative controls whose role is `None` are *not* bound.
3. **Context.** Typing (`IKeyModeState.Mode == Typing` or `app:text`) uses `Keyboard.Global` only; Transport uses `Global`
   then `Transport`. The action is `binding.For(engine.Status.Mode)`.
4. **Repeats** act only for `SpeedUp/Down(Fine)`, `Line*`, `Paragraph*`; other commands are ignored but still handled.
5. **Relative.** Wheel forward = towards the user (`-Amount`), jog forward = clockwise (`+Amount`), both negated by
   `Invert`. `Lines` accumulates per (source, control) and posts `StepLines(n)` for whole lines (1e-9 tolerance so five
   0.2 steps make one line). Accumulators and axis slots are cleared on disconnect and when the selected controller
   changes (freed slots are zeroed in the engine).
6. **Providers.** `Start()` clears device statuses, then starts only the selected provider (MouseWheel only with a
   dedicated device; Xbox with `xinput:N` or null; Shuttle with `DeviceId`). A provider is restarted only when its
   device id changed or it is not running. Provider exceptions are logged, never thrown. `Stop()` stops all providers,
   releases slots and unsubscribes from settings; `Start()` can be called again.
7. **`Execute`** returns false for `None`, and for `Play`/`PlayPause` while the engine status says AtEnd (nothing is
   posted). `ToggleOutput` raises `AppCommandRequested`; `ToggleBlank` calls `IOutputSurface.SetBlank(!State.IsBlank)`.
8. `InputObserved`, `DeviceStatusChanged`, `AppCommandRequested` handlers that throw are logged and swallowed.
   `DeviceStatuses` is a snapshot copy; keyboard statuses are ignored.

### Settings

1. Atomic save: `settings.json.tmp` (flushed) then `File.Replace(..., .bak)` or `File.Move` for the first save. Writes are
   serialised and retried up to 3 times on sharing violations (a scanner touching the file). Failures are logged and
   `SaveAsync` still completes (it never throws).
2. 500 ms debounce after the last `Update` (timer restarts on every update); `SaveAsync` cancels a pending timer and writes
   now. `JsonSettingsService` is also `IDisposable` and flushes a pending write on dispose.
3. `Load()`: missing file gives `PrompterSettings.Default` (the same instance; nothing is created on disk). An unreadable
   or invalid file (bad JSON, `null`, empty, wrong types, unknown enum names) is moved to `settings.json.bad-yyyyMMddHHmmss`
   and defaults are used, with a `Warn`. A newer `SchemaVersion` is read leniently, kept on disk until the next save,
   logged, and held as the current schema version.
4. **Clamping on load.** `null` sections/dictionaries are replaced by defaults and numbers are clamped to the documented
   ranges (reading line 0.10 to 0.60, margins 0 to 0.40, lines visible 4 to 16, line spacing 1 to 2, speed limits, steps
   greater than 0, Xbox index -1 to 3, editor zoom 25 to 500, ...). Default settings are unchanged by this, so the
   default round trip is byte-identical (tested).
5. `Update` runs the change function under the lock, raises `Changed` *outside* it on the calling thread, and schedules the
   save. Returning the same instance is a no-op.

### Maths

- `MarginMath`: pixels are rounded to whole px, then clamped to `0 .. floor(0.40 x dimension)`. The 20 % minimum text area
  follows from that, and is additionally enforced against the opposite edge in case a file carried an out-of-range value.
  Linked edits set all four to the same px value, each clamped in its own dimension. `SetLinked(true)` copies `LastEdited`.
- `TypographyMath`: non-finite or non-positive inputs fall back to safe values (never NaN). `ComputeTextArea` clamps the
  reading line fraction to 0.10..0.60 and treats a zero DPI as 1.
- `SpeedCurve`: no `Math.Sign` (it throws on NaN); NaN gives 0, speeds clamp to `+-maxSpeed`, bad exponents become 1.
- `AxisCurve`: `sign(v) x Max x ((|v| - dz)/(1 - dz))^exp`; triggers ignore negative values; never returns `-0`.
- `TimeEstimator.SecondsForLines(0 or fewer lines)` is `0`. `SectionDurations` counts lines whose top is in
  `[section.Top, next.Top)` (END excluded); speed 0 gives `TimeSpan.MaxValue`, which `Format` prints as `--`.

## API additions (additive, no signature changed)

- `TimeEstimator.Format(double seconds)`: same house format, accepts infinity/NaN directly (`TimeSpan` cannot hold them;
  `TimeSpan.MaxValue`/`MinValue` mean infinity in `Format(TimeSpan)`).
- `JsonSettingsService : IDisposable`, and an `internal` constructor with a debounce (used by tests via
  `InternalsVisibleTo`; DI still sees only the two public constructors, verified with a real container).
- `ScrollEngine.DroppedCommands` (internal).

## Known gaps and assumptions

- Nothing here talks to hardware; provider behaviour is covered by fakes only (streams B and C own the real ones).
- The line table is assumed to contain the script lines and not the END line (`EndTop` is separate). If stream E adds END
  as a last line, everything still works (clamping to `Pmax`), only `StepLines` could then land on END.
- `StepLines` uses the line that *contains* the reading line (not the nearest centre), so while playing a "next line"
  press moves between 0.5 and 1.5 lines.
- `SeekToOffset` to a point at or past `Pmax` ends in AtEnd (the invariant is "at `Pmax` means AtEnd").
- The engine does not subscribe to `Changed`; it re-reads settings every tick. A changed `StartSpeed` only matters for
  `ResetSpeed`.
- No real-time (wall clock) behaviour is tested for the engine; the render loop's `dt` is whatever the caller feeds in.
- Battery/Detail fields of `DeviceStatus` are stored and forwarded, nothing else uses them.

## Integration changes (19 Oct 2026, see docs/streams/INTEGRATION.md)

- The line table now holds text lines only (blank paragraphs and hidden section markers are space with a paragraph top).
  `StepParagraphs` steps over paragraphs that have no lines in both directions (previously "previous paragraph" stuck on
  an empty paragraph).
- `SecondsRemaining` in Display scrolling is the distance from the current line's centre to END's centre in base lines
  (gaps included), at the set speed with the engine's curve maximum; Line scrolling still counts text lines. It changes
  only when the line does, so steady ticks still allocate nothing. `SectionDurations` and `WordsPerMinute` are
  distance-based too; new helper `TimeEstimator.SecondsForBaseLines`.

## CONTRACT-REQUESTs

None. (Notes for the integrator: `Format(double)` and `IDisposable` above are optional conveniences; nothing depends on
them.)
