# Stream C: output window, monitors, preview, prompter surface

Branch: `worktree-agent-a746779758b80a353` (from the scaffold commit). Owner: stream C. Contract: ARCHITECTURE §4.C.

## Status

| Piece | State |
|---|---|
| `Monitors/MonitorResolver`, `DisplayComposer`, `DisplayEnumerator`, `MonitorService` | done, tested (pure logic + one real-hardware smoke test) |
| `Surface/PrompterSurface` | in progress |
| `Output/OutputHost` + `OutputWindow` | in progress |
| `Preview/PrompterPreview` | in progress |
| `Diagnostics/FrameStats`, `DisplaySleepGuard` | in progress |

## Decisions

- **Monitors.** StableId is the display-config *target device path*; with two targets on one GDI source Windows is
  mirroring, so both entries get `IsDuplicated = true` and are never usable. The resolver returns the saved display if it
  is connected and usable, else the lowest-numbered candidate, never the primary. While the output is open it uses
  `MonitorResolver.Find`/`ClassifyOpen`, which never fall back to another display.
- DPI scale is the *effective* monitor DPI (`GetDpiForMonitor`) / 96. The app manifest is PerMonitorV2, so `rcMonitor` is
  in physical pixels.

## How to test

`dotnet test tests/IluPrompter.Rendering.Tests`
