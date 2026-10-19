# Integration (after streams A–E merged)

Date: 19 Oct 2026. Branch `worktree-agent-a37a42b8e5dc45987`, from `main` with all five streams merged.
Result: `dotnet build IluPrompter.sln` 0 warnings; `dotnet test IluPrompter.sln` **871 passed**, 3 skipped (the opt-in
`OutputWindowHardwareTests`; run with `ILU_VISUAL_TESTS=1` they pass too). No `STUB` marker in `src/`.

How it was checked: code review of every cross-stream path, new integration tests that use the real implementations
together, and four driven runs of the real app on this single-display laptop (1920 × 1200, 100 %, 60 Hz) with
`--dev-output-on-primary --diag --settings-file <scratch>` and `samples/sample-20-pages.txt`. The app was driven only
through UI Automation patterns (Invoke, Toggle, SelectionItem, Value): no synthetic keyboard or mouse input, no hooks.

## Safety notes for the runs

- Settings: a throwaway file per run through the new dev flag `--settings-file` (missing file = defaults). The
  operator's `%APPDATA%\ILU Prompter\settings.json` was not touched. Logs went to `%LOCALAPPDATA%\ILU Prompter\logs\`
  as usual; no recovery copy was written (no run lasted 60 s with unsaved edits).
- Input: defaults select Mouse wheel with no dedicated device. `InputRouter.ApplyInputSettings` then starts no provider
  (`WantsProvider` is false), provider constructors have no side effects, and `RawMouseWheelProvider.Start` with no
  device reports NotFound without registering Raw Input. Xbox and Jog-shuttle were never selected. The Settings window
  was opened during one run (not by the driver script); its controller page only *lists* mice (read-only), which is how
  the device-listing bug below was found. No unfocused input test (S4) was run.
- No network, no admin, no keyboard hooks, no display-setting changes. The app was closed after every run.
- Screenshots are cropped to the app's own window; each was checked for overlapping notifications before committing.

## Fixes

### 1. Geometry offset (stream C finding) — already fixed in E; now proven end to end
C measured `GetCharacterRect().Top` −13.4 DIP above the line box (Poppins, 91.5 DIP). That number was the *raw*
rectangle (C's fake builder records it). E's merged builder already converts it to the line-box top with the font's
baseline ratio. New integration tests with the real surface + factory + builder at the default 1920 × 1080:
- `IntegratedSurfaceTests.LineTops_AreLineBoxes_…`: first top 0, fixed pitch 119 DIP.
- `IntegratedSurfaceTests.JoggingToALine_PutsItsInkOnTheReadingLine`: renders the surface at `LineCentre(k) − R` and
  checks the line's ink sits inside its line box, centred on the reading line within 0.08 L. A mutation check (raw rect
  tops) fails both tests.

### 2. Geometry speed: 7 ms per 1,000 lines (was 45–120 ms)
The line walk (`GetLineStartPosition`) re-formats lines in WPF and cannot be made fast. After layout, WPF already has one
`LineVisual` per laid-out line, offset to its line-box top, grouped under one paragraph visual. `LayoutGeometryBuilder`
now reads those offsets and finds each line's start offset by locating its first glyph run (`GlyphRun.Characters`) in
the paragraph text. It is used only when every check passes (flat document, one group per paragraph, text found in
order, tops increasing, first and last paragraph agree with `GetCharacterRect` to 0.5 DIP); otherwise the walk runs and
gives identical tables. Measured on the 1,234-line sample in Release: builder 8–10 ms (walk 45–125 ms); whole rebuild
100–150 ms (was about 300 ms), now dominated by `TextRange.Load` and WPF layout. `LayoutGeometrySourceTests` prove
fast = walk (mixed formatting, line breaks, shown/hidden sections, centred, the 20-page sample) and that the fast path is
really taken (so a future WPF change shows up as a failing test, not a slow show). Rebuilds stay off the per-frame path
(ARCHITECTURE §3 rule 11).

### 3. List-item anchors
Inside a list item the snapshot/output paragraph starts with its marker ("1. ", "• ") as text, the editor paragraph does
not. `DocumentNavigator` now counts the marker for editor paragraphs inside list items (same walk as the flattener,
`DocumentFlattener.MarkerOf`) and subtracts it in `GetPointer`; empty list items no longer add a snapshot paragraph.
Test: `ListAnchors_MapToTheSameOutputText_AndBackToTheSameEditorCaret`, and the every-caret test now needs no correction.

### 4. Threading rule (stream C CONTRACT-REQUEST) applied
ARCHITECTURE §3 rule 5 and §4.C now describe the pulse-driven preview while the output renders (Rendering-driven, sole
ticker with `TickIfStale(now, 0.001)` otherwise). New rule 10: no per-frame UI-thread work in the control window while
live (no Rendering subscriptions, no looping storyboards). Stream D checked: no Rendering subscription, storyboard or
animation; timers are the 10 Hz status poll (sets only changed properties; measured harmless), the 250 ms debounce and
the recovery timer. New rule 11: rebuild cost and that it is never per frame.

### 5. Wiring bugs found and fixed
| # | Bug (how found) | Fix (owner area) | Test |
|---|---|---|---|
| 5a | **Start, section jumps and line steps landed on black.** The start put the hidden "## Welcome" half-line on the reading line with the first words 1.5 lines below; every other "next line" or Line-scrolling step landed on an empty paragraph (.txt keeps blank lines) — seen in the first run | Lines are text lines: blank paragraphs (incl. hidden markers; WPF draws empty lines as U+200B) keep a paragraph top but get no line (E); paragraph steps skip line-less paragraphs both ways (A; "previous paragraph" used to stick); ARCHITECTURE §2.1 | builder, engine and surface tests |
| 5b | **Time left under-read by 25–45 %**: counted text lines, but Display scrolling also scrolls gaps and blank lines | Display: distance to END in base lines from the current line (still changes only per line, steady ticks allocate nothing); Line: text lines; section durations and wpm distance-based (A) | `Status_SecondsRemaining_*` |
| 5c | Typing above a marker after clicking a section pulled the caret to that section (rows rebuilt → selection restored → "show in editor") (review) | ignore selection changes while rebuilding rows (D) | `RebuildingRows_DoesNotShowTheSectionInTheEditor…` |
| 5d | A marker turned back into text went to the output small and grey (snapshot built before section styling) (review) | style sections before building the snapshot; republish when the marker setting changes (D) | `EditorControllerTests` (mutation-checked) |
| 5e | Clicking the preview did not take focus: Space kept typing into the script (DESIGN: a click gives keys to the transport) (review) | preview focuses itself on click (C) | `ClickingThePreview_TakesFocus…` |
| 5f | Device list empty and a warning on *every* settings change once Settings had been opened (`InvalidDeviceId` from one device) (run 2 log) | list mice one device at a time (B); re-list only when input settings change (D) | `ControllerPage_ListsDevicesAgainOnly…` |
| 5g | Saved window bounds were ±Infinity when the window opened maximised (found in the real settings file) | keep remembered bounds, store only the maximised flag (D) | — |
| 5h | A failed open left the script's overrides in settings; undo stayed off if a script was rejected (review) | apply overrides after load; `finally` re-enables undo (D) | — |
| 5i | `--diag` frame stats reported 5–40 % "slow" frames whenever paused or jogging (WPF need not render still frames; preview gets 250 ms heartbeats) | count only intervals touching a moving frame (C) | `StillFrames_AreNotCounted…` |
| 5j | Dev output window vanished behind the maximised control window on one display | dev window topmost (C, dev flag only) | hardware test updated |

Added for testing and operators (D): a script path on the command line opens that script (Explorer "Open with"), and
the dev flag `--settings-file <path>`. S6 diagnostics: `--diag` logs each face/weight's resolved font file.

Checked and fine: snapshot versioning and coalesced rebuilds on both surfaces; editor base size and section marker reach
the factory; identical layout keys on both surfaces; `TranslateTransform.Y = −P` matches the engine's `LineCentre − R`
targets and §2.1 bounds; every transport button and default key maps to the right command; Display/Line mode radios;
margin link; section double-click/Enter = GoToSection; .ilup open is not dirty, imports are (Save becomes Save as);
`.ilup` round-trip covered by E's tests.

## Phase-0 results

| # | Result | Evidence |
|---|---|---|
| S1 | **Pass (with a caveat).** `dotnet publish … -p:SelfContained=true -o publish/ILUPrompter` (263 files, 153 MB) started from the folder with no AppLocker or SmartScreen block; window after 5.9 s (cold), exit code 0. Started with `Start-Process`, not by double-click in Explorer, and a locally built file has no Mark of the Web, so SmartScreen was not really exercised: repeat with a copy that came over the network or a USB stick | log 14:59 |
| S2 | **Pass on one display** (dev output window 960 × 540, fonts 45.8 DIP at 8 lines and 91.5 DIP at 4 lines). Display scrolling at 2, 4.5, 8 and 10: output p99 16.67 ms, 0.00–0.42 % of frames over 1.5 × nominal, max ≤ 33 ms (criteria: p99 ≤ 25 ms, < 0.5 %, max < 50 ms). 4 lines: 0.00 %. Line scrolling: 0.54 % of moving frames. Preview (pulse-driven) p99 about 21 ms. Hitches only at rebuilds (50–190 ms) and under the UIA driver's own load. **Not done:** the real 1080/2160 output sizes (92/≈200 DIP), and typing continuously while playing (needs keyboard input) | log 14:40–14:56 |
| S6 | **Pass.** Poppins 400/500/600 resolve to `Poppins-Regular/Medium/SemiBold.ttf` in `IluPrompter.Theme;component/Fonts/` with no simulation; 700 uses SemiBold (no Bold file yet). DM Mono 400/500 real files, 600/700 simulated bold (unused) | `[fonts]` log lines; `BundledPoppins_GroupsItsWeights…` test |
| S7 | **Pass.** Output and preview log identical first-50 line tops and line counts for every layout key in every run; `TwoSurfacesOnTwoUiThreads_GiveIdenticalLineTables_S7` checks it on two real UI threads (Δ ≤ 0.5 DIP, equal offsets) | log `surface:output` / `surface:preview` |
| S3 | Not run: needs a second display with a different refresh rate | — |
| S4a–c | Not run: needs controllers and the user's explicit go-ahead (Defender rule) | — |
| S5 | Not run: needs a second, extended display (placement, focus, Alt+Tab, 150 %, hot-plug, Duplicate) | — |
| S8 | Not run: needs the user to set a 1-minute screen timeout and watch | — |

Screenshots (`docs/streams/screens/`): `integration-02-output-shown.png`, `-03-playing-4.5.png`,
`-05-line-scrolling.png`, `-06-jump-to-start.png`, `-09-next-section-x2.png`, `-11-four-lines.png`,
`-12-margins-linked-300.png`, `-13-blank.png` (dev output window top left, preview top right).

## Remaining gaps, by priority

1. **Second-display checks** S2 at real sizes, S3, S5, S8, and S4 after the user's go-ahead.
2. **An edit or layout change while live hitches the output** for the rebuild (100–150 ms warm, about 0.5 s for the
   first rebuild after start in Release). Options: defer output rebuilds while Playing until the next pause or a quiet
   moment, make `TextRange.Load` cheaper (build runs directly instead of loading XAML), or ReadyToRun publishing for
   the cold case.
3. **Editing above the reading line while live jumps the prompt by a paragraph**: relayout keeps the paragraph *index*.
   Needs the editor to shift the anchor by the edit's paragraph delta (DESIGN open question "editing while live").
4. **Section list and transport keys**: by DESIGN the list is in Transport mode, so Up/Down change speed and Esc pauses
   instead of moving in the list / closing the flyout. Needs a design decision.
5. **Margin and reading-line drags rebuild the output on every mouse move** (coalesced) — stutter on long scripts while
   live; consider applying on release, or viewport-only for the reading line (already cheap).
6. `TimeEstimator.SectionDurations` uses the default curve maximum (±10); custom Min/Max speed skews section times
   (the engine's own time-left uses the right maximum).
7. Blank lines in `.txt` imports still make two-line gaps (0.5 gap + empty line + 0.5 gap); they are no longer stops.
   Collapsing them on import would change the frozen import rule.
8. Cosmetic: the preview's "Reading line" caption overlaps the left ▶ marker; the editor does not scroll with the prompt
   (gutter marker only) — check against DESIGN.
9. UI Automation tree walks over the 6,000-word editor take seconds and load the UI thread (seen with the driver);
   screen readers may feel this. Preview frame stats at a motion start include the heartbeat gap (read with care).
10. Poppins Bold/Italic not bundled (bold renders SemiBold; italic is synthetic) — DESIGN open question.
