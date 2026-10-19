# Stream C: output window, monitors, preview, prompter surface

Branch: `worktree-agent-a746779758b80a353` (from the scaffold commit). Owner: stream C. Contract: ARCHITECTURE §4.C.
Owns `src/IluPrompter.Rendering/**` (except the csproj), `tests/IluPrompter.Rendering.Tests/**`, this file.

## Status

All of §4.C "Implements" is done and no `STUB` marker is left in `src/IluPrompter.Rendering`.
`dotnet build IluPrompter.sln`: 0 warnings. `dotnet test IluPrompter.sln`: green (Rendering: 135 passed, 3 skipped by default,
see "How to test").

| Piece | File(s) | State |
|---|---|---|
| Monitor rules | `Monitors/MonitorResolver.cs`, `DisplayComposer.cs` | done, unit tested |
| Monitor enumeration | `Monitors/DisplayEnumerator.cs`, `MonitorService.cs` | done; real enumeration smoke-tested on this laptop (single display) |
| Prompter surface | `Surface/PrompterSurface.cs`, `SurfaceLayout.cs` | done, unit + pixel tested |
| Output thread, window | `Output/OutputHost.cs`, `OutputWindow.cs` | done; state machine tested headless, window mechanics tested on the primary display with a small rectangle |
| Preview | `Preview/PrompterPreview.cs`, `PreviewScene.cs`, `PreviewOverlay.cs`, `PreviewMath.cs` | done, tested incl. pixel render |
| Diagnostics | `Diagnostics/FrameClock.cs`, `FrameStats.cs`, `DisplaySleepGuard.cs` | done, unit tested |
| `IOutputRenderer` seam | `Surface/PrompterSurface.cs` | declared, WPF renderer only (D2D fallback not implemented, as specified) |

## The important finding: two UI threads that both animate halve each other's frame rate

Measured on this laptop (Tier 2 hardware rendering, one 1920 x 1200 / 60 Hz panel), `CompositionTarget.Rendering` intervals:

| Setup | Result |
|---|---|
| One window animating | 16.67 ms, 60 fps |
| Two windows animating, **same** UI thread | 16.67 ms, 60 fps |
| Two windows animating, **each on its own UI thread** (the §3 design) | **33.33 ms on both** |
| Output thread animating + UI thread doing occasional updates (2 Hz, 10 Hz readout) | output unaffected (0 slow frames in 4 s) |
| Output thread animating + UI thread **with a `CompositionTarget.Rendering` subscription** painting the preview, even only 10 to 20 times a second | output loses 0.35 to 0.4 frames per preview paint |
| Output thread animating + preview painted **from a pulse sent by the output thread** (no Rendering subscription on the UI thread), 5 to 60 Hz | output unaffected, 0 to 1 slow frames in 5 s, **even at 60 Hz** |

So the cost is not the preview's drawing (collapsing the surface, the overlay, hiding the text, `BitmapCache`, half size all
gave the same result), it is a per-frame `Rendering` subscription on a second UI thread, which makes both threads sync with the
single WPF render thread every frame. Following §3 literally (preview calls `TickIfStale` from its own `Rendering` handler)
would have put the output at 30 fps whenever the preview is visible.

**What the code does instead** (`PrompterPreview`, "driving the picture"):

- Output window rendering (`State.IsOpen && Display == Ready`): the preview has **no** Rendering subscription. `OutputHost`
  implements the internal `IFramePulse`, raised on the output thread after each frame in which the position changed (and every
  250 ms as a heartbeat). The preview coalesces pulses into one `Dispatcher.BeginInvoke(Render)` that sets the transform. A UI
  thread watchdog (250 ms timer) falls back to Rendering if pulses stop for 500 ms and returns to pulses when they resume.
- Output not rendering (closed, hidden, Missing, Duplicated, or an `IOutputSurface` without a pulse): the preview subscribes to
  `Rendering` and is the only ticker. It calls `TickIfStale(frameTime, 0.001)` so it ticks **every** frame (with the contract's
  default 0.05 s the engine would only advance at 20 Hz while the output is closed).
- Result with the real stack (stub engine, both windows animating, 10 s windows after warm-up): output p99 16.67 ms,
  0.17 to 0.5 % of frames above 1.5 x nominal, max 33 to 50 ms; preview p99 about 21 ms.

**Consequence for stream D (and everyone):** while the output is live, do not add per-frame work on the UI thread: no
`CompositionTarget.Rendering` subscriptions, no looping storyboard animations (spinners, pulsing chips, marquee). Occasional
updates (10 Hz readouts, typing, caret blink) are fine, measured.

CONTRACT-REQUEST: ARCHITECTURE §3 rule 5 and §4.C "Preview" say the preview calls `TickIfStale(now, 0.05)` from its own
Rendering handler. Please change the prose to the design above (pulse-driven while the output renders, Rendering-driven and
sole ticker otherwise) and add the "no per-frame UI-thread work while live" rule to §3. No frozen signature changes: the
pulse is an internal interface that `OutputHost` implements and `PrompterPreview` discovers with `Output as IFramePulse`.

## Decisions

### Monitors
- StableId = display-config *target device path*; with two targets on one GDI source Windows is mirroring, so both entries get
  `IsDuplicated = true` and are never usable. FriendlyName "Built-in display" is substituted for an empty name on an internal
  panel. DisplayNumber is parsed from `\\.\DISPLAYn`. If QueryDisplayConfig fails the GDI name is the id and refresh is 60 Hz.
- `MonitorResolver.Resolve`: saved display if connected and usable (not primary, not mirrored), else the lowest-numbered
  candidate, else null. Never the primary. `Find`/`ClassifyOpen` (used while the output is open) never fall back to another
  display. Mirroring while open reports `Duplicated`, a vanished display `Missing`.
- DPI scale = effective monitor DPI / 96 (`GetDpiForMonitor`). The manifest is PerMonitorV2, so `rcMonitor` is physical pixels.
- `MonitorService` caches, listens to `SystemEvents.DisplaySettingsChanged` and raises `MonitorsChanged` 500 ms after the last
  event (timer, restarted by each event). It is `IDisposable` (the DI container disposes it). Test seam: an internal ctor with a
  fake enumerator and a short debounce.

### Output window and host
- `OutputHost` public constructor is frozen; an internal overload adds `OutputHostOptions(DevOutputOnPrimary, Headless)`. The
  public one reads `--dev-output-on-primary` from the command line. `Headless` (tests) runs the whole state machine with the
  real thread and Dispatcher but never creates a native window.
- Thread "ILU Output" (STA, background). `Dispatcher.UnhandledException` logs and marks handled. All awaits inside the host use
  `ConfigureAwait(false)` because `App.OnExit` blocks on `ShutdownAsync().GetAwaiter().GetResult()` from the UI thread.
- `State` is updated synchronously under a lock (a fast B-B toggle reads the right value), `StateChanged` is raised on the
  output thread, in order, never for a no-op change.
- Show order: resolve display, persist `MonitorStableId` (if null) and `Last*`, publish the state with the new geometry, start the
  surface and **rebuild synchronously before the window is visible**, place the window while hidden (twice if WPF applied a DPI
  suggested rectangle), `Show()`, verify with `GetWindowRect` and place again if it differs, then subscribe to Rendering.
- While closed the output surface is not started (no wasted layout on every edit); `State.Display/Monitor/Geometry` still
  follow the display so the preview has the right aspect before the first F11.
- Choosing another display in Settings while open moves the output to it. Unplugging hides the window (`Missing`) and it
  returns to the same StableId only.
- `OutputWindow`: borderless/topmost/no-activate/`WS_EX_TOOLWINDOW`/no cursor (`ForceCursor`), black from construction, DWM
  transitions disabled, `WM_MOUSEACTIVATE` answers `MA_NOACTIVATE`. The surface sits in a `ScaleTransform` of
  `displayDpi / windowDpi` (1.0 normally) so a DPI mismatch between window and display cannot change what fills the screen.
  Blank = a black rectangle over the surface; scrolling continues underneath.
- Output ticking: `FrameClock` maps `RenderingEventArgs.RenderingTime` to Stopwatch ticks (anchored, re-anchored on 250 ms
  drift, strictly increasing, repeated RenderingTime rejected). This takes dispatcher latency jitter out of `pos += v*dt`.
- `DisplaySleepGuard` (output thread only, execution state is per thread): display+system awake and
  `GCLatencyMode.SustainedLowLatency` while Playing and visible; both released otherwise and on dispose.

### Surface
- Layout key exactly as §4.C; a second `ContentKey` adds the default ink so a colour change rebuilds the document without the
  engine seeing a new layout. Anything else cheap (margins, reading line, markers, dim band) only repositions and calls
  `engine.SetLayout(sameGeometry, newViewport)`.
- Rebuild is coalesced (`Interlocked` flag + one `BeginInvoke(Background)`), never throws, logs failures and rebuilds over
  50 ms, and traces the first 50 `LineTops` per key with `--diag` (S7).
- `Documents.Current` is read at rebuild time (it is always the newest published snapshot), so no snapshot field is needed.
- Reading-line markers: triangles 0.5 x line height, 0.866 of that wide, 0.1 x line height from the text; a margin narrower
  than 0.6 x line height gets a 6 DIP bar at the screen edge instead. Dim band ends half a base line above the reading line so
  the current line is never dimmed.
- Moving: sub-pixel Y, `TextHintingMode.Animated`, grayscale, Ideal. Still for 2 frames: Y snapped to a device pixel
  (including the text area's fractional top) and `Fixed` hinting. The preview never snaps.

### Preview
- Letterbox in `backdrop`, `ScaleTransform` render transform on a surface laid out at the output's DIP size (never laid out
  smaller). 2 DIP reserved around the stage for the frame (1 px `line-strong`, 2 px `live` while `State.IsLive`).
- Overlay drawn at 1:1 in control coordinates (so dashes stay 1 px), redrawn only when its state record changes.
- Drag: grab zone 5 DIP; margin edges beat the reading line at their corner; the reading line only reacts inside the text area.
  Margin drag calls `MarginMath.SetEdgePixels` (Shift snaps to 8 output px); reading line writes `ReadingLineFraction`
  clamped to 0.10..0.60. Settings are not touched when the value did not change.
- BLANK: surface opacity 0.35 and the `active` chip "■ BLANK · the presenter sees black".

## Facts other streams should know

- **Stream E (geometry builder).** `TextPointer.GetCharacterRect(...).Top` is the *glyph box*, not the line box. With Poppins
  and `LineHeight` 1.3 x size (below the font's natural height) the first paragraph's "top" measured -13.4 DIP at an 91.5 DIP
  size, i.e. above y = 0. If the builder records line tops from it, document Y is off by that constant; the engine's
  "line centre on the reading line" is then off too. The hosting is correct (width = text-area width, unconstrained height,
  explicit Measure/Arrange then `UpdateLayout`, valid layout even when detached); compensate in the builder.
- **Stream E (factory).** `OutputDocumentFactory` and `LayoutGeometryBuilder` are called from the output thread *and* the UI
  thread on shared singleton instances: keep them stateless. The scaffold factory's `LoadInto` brings the editor's own
  Foreground/FontSize in through the saved XAML and overrides the style (black text on black); the real one must flatten.
- **Stream A.** `TickIfStale(ts, staleAfter)` is called with `staleAfter` 0.001 by the preview when no output is rendering.
  `SetLayout` is called by both surfaces on every rebuild (same key => viewport only).
- **Stream D.** `TextOptions.TextHintingMode` is not a property-inherited value (the DP reads Auto on children) but WPF
  propagates it at the visual level, so set it on the element that scrolls (the surface does). No per-frame UI-thread work
  while live (see above).

## How to test

```
dotnet test tests/IluPrompter.Rendering.Tests                       # 135 pass, 3 skipped
$env:ILU_VISUAL_TESTS = "1"; dotnet test tests/IluPrompter.Rendering.Tests --filter OutputWindowHardwareTests   # 3 more
```
The three `OutputWindowHardwareTests` show a real 640 x 360 window on the primary display for a moment (placement, extended
styles, not activated, DPI compensation), so they are opt-in. They pass on this laptop.

| Test class | Covers |
|---|---|
| `MonitorResolverTests`, `DisplayComposerTests`, `MonitorServiceTests` | saved present/missing, only primary, mirrored, reordered, display numbers, duplicated detection, debounce, real enumeration smoke |
| `LayoutKeyTests`, `MarkerPlacementTests`, `PreviewMathTests` | key changes for every input and is stable otherwise (and culture invariant), marker rule and threshold, letterbox, hit test, drag maths |
| `FrameClockTests`, `FrameStatsTests`, `DisplaySleepGuardTests` | duplicate rejection, jitter removal, ring buffer, percentiles, zero allocation, GC mode restore |
| `SurfaceTests` | `[WpfFact]` rebuild with a fake engine/factory/builder: geometry + `SetLayout`, host layout validity, cheap refresh vs rebuild per setting, coalescing, failure handling, markers, dim, snap, **pixel checks** of the rendered surface |
| `OutputHostTests` | real thread + Dispatcher, headless: show/hide, persist, Missing/Duplicated/return, never another display, settings-driven move, blank, event thread and order, shutdown/restart, dev flag |
| `PreviewTests` | `[WpfFact]` scale/letterbox, geometry change, blank dimming, margin and reading-line drag, drive mode (pulses, watchdog, fallbacks), rendered pixels |

## Needs a real second display (not verifiable on this single-display laptop)

1. **S5**: F11 puts the window exactly on `BoundsPx` of the other display (log line "Shown on ..." prints actual vs display
   bounds), focus stays in the control window, not in Alt+Tab/taskbar, cursor hidden; at 150 % still 8 lines.
2. Unplug while LIVE: window hides (`Missing`), nothing appears on the laptop screen, replug returns it; Win+P Duplicate gives
   `Duplicated`. `DisplayComposer` treats two targets on one source as mirrored but that has only been tested with synthetic data.
3. Per-monitor DPI move on show (laptop 100/125 % to a TV at 150 %): the double `PlaceOn` and the post-show check cover the
   WPF suggested-rectangle behaviour; confirm no flash on the operator display.
4. **S2/S3 on the real pair of displays.** The 30 fps interaction above was measured with both windows on one display. Different
   panels (and different refresh rates) may behave differently; run with `--diag` and read `frames:output` / `frames:preview`.
5. S8 (display stays awake while playing) was not run.

## Known gaps

- D2D/DirectWrite output (`IOutputRenderer`) is a seam only.
- The preview's per-pulse `Dispatcher.BeginInvoke` allocates one small object per painted frame (coalesced; about 60 per
  second while playing). The output handler itself allocates only WPF's own boxing of the `TranslateTransform.Y` value.
  `--diag` GC counts over 10 s windows with both windows animating: +0 to +2 per generation.
- Highlight "0.1 em wider than the run" is E's/deviation 8.
