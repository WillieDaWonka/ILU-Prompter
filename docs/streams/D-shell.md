# Stream D: control window shell, theme, settings UI

Branch: `worktree-agent-acccb1a241da86c35` (from the scaffold commit). Owner paths: `src/IluPrompter.App/**`, `src/IluPrompter.Theme/**`,
`tests/IluPrompter.App.Tests/**`, this file. Status: **done**, see "Gaps" for what is deliberately not finished.

## What is here

### Theme (`src/IluPrompter.Theme`)

`Themes/IluControls.xaml` now merges six dictionaries, each of which merges `IluTokens.xaml` itself (WPF resolves `StaticResource` at parse time):

| File | Contents |
|---|---|
| `IluText.xaml` | every `Ilu.Text.*` style (Title, Heading, Body, Label, Caption, Button, ButtonPrimary, State, Mono, Readout, Icon, IconTransport, Editor) plus `Ilu.Text.Glyph` for chip shapes |
| `IluButtons.xaml` | focus visuals (`FocusVisual`, `.Gap`, `.Small`), secondary (also the implicit `Button`), primary, transport, Play (driven by `Tag` = Paused / Playing / AtEnd), link, toolbar button and toggle (plain, wide, colour with 4 px bar), segmented segment, Blank toggle, state chips (`Ilu.Button.Chip` and `Ilu.Chip`, driven by `Tag` = ok / live / idle / active / attention / fault) |
| `IluInputs.xaml` | TextBox (+ mono), editor paper RichTextBox, CheckBox, RadioButton, ComboBox (+ items), speed slider (`Tag` = Playing paints the fill `live`) |
| `IluMenus.xaml` | Menu, MenuItem (all four roles), ContextMenu, Separator, ToolTip |
| `IluSurfaces.xaml` | Window, Panel, Divider, ScrollBar, ListBox, section row (`Tag` = `current`), settings navigation row |
| `IluControls.xaml` | the merge, plus `GridLength` forms of the bar heights (`Ilu.Grid.AppBar` 44, `StatusBar` 28, `PanelTitleBar` 36), because the `sys:Double` tokens cannot be a `RowDefinition.Height` |

Additions to the Theme project (the frozen `ThemeResources` members are untouched): `IluIcons` (Segoe Fluent / MDL2 code points),
`CentreFillConverter` (speed slider fill from the centre), `TextContextMenus` (skins the built-in TextBox menu).

Fonts: checked on the build machine that WPF groups `Poppins` Regular / Medium / SemiBold under one family name, so styles use `FontWeight`
(`Medium`, `SemiBold`), not per-weight family names. `ThemeTokenTests.BundledFonts_Resolve...` is the S6 check for the UI and mono faces.

No hex literals outside `IluTokens.xaml` (test-enforced). No `DropShadowEffect`, no animation, no sound.

### App (`src/IluPrompter.App`)

- `App.xaml.cs`: startup order frozen (settings `Load()` â†’ output `InitializeAsync()` â†’ main window â†’ router `Start()` â†’ open last script). Nothing resolves
  `IScrollEngine` before `Load()` (Stream A's engine reads `StartSpeed` and `DefaultMode` in its constructor). `--window-size WxH` is a dev flag (exact size, not
  remembered). Unhandled UI exceptions are logged and marked handled so a glitch cannot end a show.
- `Composition/AppServiceCollectionExtensions.cs`: all DI. Everything is a singleton except `SettingsWindow` (a transient behind `Func<SettingsWindow>`).
- `Views/MainWindow`: regions A (app bar with menus, script name, saved text, Show output, Settings), B (sections panel or 44 px rail with a 280 px flyout),
  C (`EditorPanel`), D (`LiveColumn`), G (status bar). Tiers are `LayoutTiers.ForWidth` (Wide 1600+, Compact 1200 to 1599, Narrow below 1200) applied in
  `ApplyTier`. Window bounds are restored and saved through `Settings.ControlWindow` (opens maximised by default). Dark title bar via `Interop/DarkTitleBar`
  (an attached property; `DWMWA_USE_IMMERSIVE_DARK_MODE`, plus caption colour on Windows 11) on every window.
- `Views/EditorPanel` + `Editing/EditorController`: toolbar (Undo, Redo, Size, B I U S, ink and highlight pop-ups, Clear, section marker, Find, zoom; Find and
  zoom fold into a â‹¯ menu below Wide), the paper with a 24 px gutter (reading-line â–¶ and `raised` band, double-click to jump the prompt), 2 px `live` edge and
  caption while LIVE, find bar, zoom as a `LayoutTransform`. Script keys: Ctrl+] / [, Ctrl+Space, Ctrl+= / âˆ’, Ctrl+Shift+X / H / Enter, Ctrl+F, F3. Esc leaves the
  script. 250 ms debounced snapshots through `ISnapshotBuilder`/`IPromptDocumentService.Publish`; section styling after edits; recovery copy every
  `Editor.RecoverySeconds` under `%LOCALAPPDATA%\ILU Prompter\recovery` (removed when the script is saved).
- `Views/LiveColumn`: D1 chips, D2 preview frame (1 px `line-strong`, 2 px `live` while LIVE) in an `AspectRatioDecorator` so it follows the output's shape,
  wheel hint, caption, D3 transport (8 buttons, Play 96 / 72 / 56 wide), D4 speed (âˆ’ / track / + / readout / wpm), D5 segmented mode switch and Blank toggle,
  D6 readouts, D7 the live panel (see "Live panel (D7)" below; it replaced the Wide-only output layout block).
- `Views/SettingsWindow` + `Views/Pages/*`: Controller (three cards, device picker from `IScrollInputProvider.EnumerateDevices`, mapping table, sensitivity,
  test readout), Output (display picker, margin editor, lines, spacing, reading line, three toggles), Text, Scrolling, Shortcuts (+ print), About. Non-modal,
  owned, applies as you go, Esc closes, "Reset this page to defaults" asks first.
- `Views/AppDialog` + `Services/DialogService`: the app's own dialog (one primary, up to two others, optional text prompt). Never `MessageBox`.
- `Services/ScriptSession`: New / Open / Save / Save as / Export, recent files, unsaved-changes question, per-script `Overrides` (applied on open, written on save),
  imports (rtf, txt, docx) open as "not saved" until saved as `.ilup`. `Services/OutputController`: F11 and the button; "Close the output?" while LIVE with
  "Keep output" as the default.
- View models (CommunityToolkit.Mvvm, field-based): `MainViewModel`, `LiveViewModel`, `SectionsViewModel`, `MarginEditorViewModel`, `OutputLayoutViewModel`,
  `EditorToolbarViewModel`, `SettingsViewModel` and its pages. Pure logic (no WPF) is in `Logic/`: `ChipMapper`, `LayoutTiers`, `ReadoutFormat`, `Labels`,
  `ShortcutCatalog`.

### Threading

`IUiThread.Post` marshals every event from the output thread and the provider threads (`StateChanged`, `DeviceStatusChanged`, `InputObserved`, settings
changes). Status is polled at 10 Hz from one `DispatcherTimer` in `MainWindow` (live column, sections, status bar, reading marker, Settings test readout).
`InputObserved` is only stored on the provider thread; the page renders it on the poll, so input never waits for the UI.

## How to test

```powershell
dotnet build IluPrompter.sln          # 0 warnings
dotnet test  IluPrompter.sln          # App.Tests: 268 (after the live panel)
dotnet run --project src/IluPrompter.App -- --window-size 1366x720     # exact size; also 1920x1064 and 1093x600
```

Tests (`tests/IluPrompter.App.Tests`): `ThemeTokenTests` (every DESIGN.md colour, size and radius equals `IluTokens.xaml` both ways; the no-hex scan over
`src/**/*.cs|xaml`; the scan's own pattern; the control dictionary and its implicit styles load; bundled fonts resolve), `ChipMapperTests` (every row of both
chip tables), `FormatAndTierTests` (tiers by width, speed and duration formats, number parsing, key map), `ViewModelTests` (live column, margin editor via
`MarginMath`, sections, output controller, Settings pages). The margin tests compare the view model to `MarginMath`'s own result, so they hold for the scaffold
stub and for Stream A's real body.

Screenshots (`docs/streams/screens/`, taken from the running app by a screen capture of the window): `layout-1920x1080.png`, `layout-1366x768.png`,
`layout-1093x614.png`, `sections-flyout-1366x768.png`, `editor-colour-menu-1366x768.png`, `settings-*.png`. The dev machine is 1920 Ã— 1200, so the three
reference sizes are made with `--window-size`, not by changing Windows display settings.

## Decisions and deviations from DESIGN.md

- **App bar buttons are 36 px, not 44.** The bar is 44 px tall, so a 44 px button would touch both edges.
- **Selection opacity 0.4.** WPF paints the selection over the text and ignores `SelectionTextBrush` on a `RichTextBox`; an opaque `selection` fill hid the
  script. Text stays readable, but the 7.8:1 in DESIGN.md is not met while text is selected.
- **Spell-check is off** in the editor. WPF underlines in red and cannot be recoloured to `attention`; red is reserved for faults.
- **Toolbar buttons never take keyboard focus** (so the caret and selection stay in the script). The shortcuts reach every action; the toolbar itself is not in the tab
  order. DESIGN's focus order lists the toolbar, so this is a known gap.
- **Chip glyphs** (â—‹ â–¶ â–  â–² âœ• âœ“) are set in Segoe UI Symbol (text, not emoji). Transport and toolbar icons are Segoe Fluent / MDL2 code points; the
  glyphs were checked on screen on this machine (Windows 11). "Tx" and "Â§+" are text, as in the wireframe.
- **Narrow tier fits 8 targets in 440 px** by dropping the live column padding to 8 and Play to 56 wide (gaps stay 8).
- **Margin editor** is the diagram plus four number boxes plus the link toggle; the number box (`Controls/NumberBox`) clamps only after load, so a late
  `Maximum` binding cannot squash a value.
- **Esc in the script** goes to the preview if it is focusable, else to Play.
- **Key mode follows focus** in `MainWindow.OnPreviewGotKeyboardFocus`: focus in the editor is Typing; focus in the live column or sections is Transport.
- **Per-script overrides**: opening a script applies its `Overrides` to the app settings; saving writes the current lines, spacing, margins and start speed.
- **Print this list** opens the Windows print dialog (like Open and Save, it follows the Windows theme).

## Gaps (not done, or waiting for another stream)

- Engine-driven readouts show zeros until Stream A merges (the scaffold engine reports no lines, wpm or remaining time). The view models read only `EngineStatus`.
- The preview shows a black box until Stream C merges; the wheel hint, preview caption and LIVE frame are wired and tested through view models, but the LIVE
  frame colour and the editor LIVE edge were not seen on screen (no second display on this machine and the stub output cannot go live).
- Section markers are styled by `IScriptFormatter.StyleSectionParagraphs` (Stream E); the editor only calls it when the set of section paragraphs changes.
  If E's body changes paragraph properties, it can add to the undo stack; E should keep it undo-neutral.
- Rename and delete of a section marker act on the paragraph text; durations in the section list stay empty until Stream A's `SectionDurations` is real.
- No automated UI test of the windows themselves (view models and theme only). The windows were driven by hand-scripted mouse and key input and checked by screenshot.
- Paste keeps source formatting (no "paste as plain text").
- The Settings window does not forward keys to the input router, so F5 and the like work only in the main window.
- High Contrast mode (DESIGN.md: switch to `SystemColors`) is not implemented.
- The â‹¯ overflow on the editor toolbar is a context menu (Find, zoom in / out / 100 %); the zoom list is only in the Wide toolbar.

## Integration changes (19 Oct 2026, see INTEGRATION.md)

- Command line: a script path opens that script; dev flag `--settings-file <path>` uses a throwaway settings file.
- `--diag` logs each face/weight's resolved font file (`Services/FontDiagnostics`, Phase-0 S6).
- Sections: rebuilding rows no longer "shows the section in the editor" (the caret stayed where you type).
- Editor: section styling runs before each snapshot (no small grey text on the output after un-marking a section);
  changing the section marker setting republishes at once; undo is re-enabled even if a script is rejected.
- Script session: per-script overrides apply only after the script loaded.
- Saved window bounds: never Â±Infinity (empty `RestoreBounds` when the window opened maximised).
- Controller page: re-lists devices only when the input settings change.

## Live panel (D7), 19 Oct 2026

The foot of the live column is one panel: tabs **Controller** | **Output layout** and a clickable hide chevron. Built from DESIGN.md "Live panel (D7)",
"Output layout tab" and "Controller drawing", with the user's three decisions applied (they override DESIGN.md, which was updated to match): the freed
space stays empty when the panel is hidden; **no F9 / Ctrl+F9**, the panel is mouse-only (clickable tabs, clickable icon, tooltips "Hide this panel" /
"Show this panel"); the **ShuttlePRO v2** is the primary jog-shuttle drawing (also shown while no device name is known yet), the ShuttleXpress is kept.

- **Tabs and hide** (`Views/LivePanel`, `ViewModels/LivePanelViewModel`): plain `PanelTabButton`s (`Controls/PanelTabButton`), not a `TabControl`, so they answer Enter
  and the mouse only and never take Space, the arrows, Home, Page Up / Down (transport keys stay transport keys; the control declines them in `OnKeyDown`).
  Open tab: 2 px `text-strong` bar (`Ilu.Button.PanelTab`, `Tag` = open); neither tab has it while hidden. Clicking the open tab does nothing; clicking a tab while
  hidden opens the panel on it. While the drawing is not on screen the last-input line sits in the bar, right-aligned, clipped on its left.
- **Remembered**: `WindowSettings.LivePanelTab` and `LivePanelOpen` (ARCHITECTURE Â§2.8, contract addition with defaults that keep old files loading, `Sanitize` guards an
  undefined tab number). `MainWindow.SaveBounds` now copies the record with `WindowBounds.Apply` (it used to build a fresh `WindowSettings`, which would have reset them).
- **Where the body goes** (`Logic/LivePanelFit`, measured in `LiveColumn.OnLayoutUpdated`): free height below D6 with the LIVE caption's room always reserved (the 18 px
  note row is now always there). 209+ inline large (168 body, drawing 320 x 160), 161 to 208 inline small (120, 0.75), else the bar only and a flyout over the editor
  (width min(600, editor - 16), large drawing when its inner width is 576+, bottom level with the bar's bottom, set in `MainWindow.PositionFlyout`). Under 40 px the
  transport captions hide, then the preview's output caption moves into its tooltip. Constants (20 and 24 px) are the measured heights of those two rows.
  The flyout is closed at launch and when the window changes size, never takes focus (its buttons are not focusable), is not closed by a click elsewhere or Esc, and hides
  while the key mode is Typing. Opening or hiding it writes `LivePanelOpen`.
- **Output layout tab** (`Views/OutputLayoutTab`): the existing `MarginEditor`, Lines and Spacing number boxes, unchanged, in the large body; a three-row form (no diagram) in
  the small body. Settings > Output keeps the full editor.
- **Controller tab** (`ViewModels/ControllerPanelViewModel`, `Logic/Controller*`, `Views/ControllerPanelView`, `Views/Drawings/*`): `IInputRouter.InputObserved` is handled on the
  provider thread by `ControllerInputTable.Record` only (a lock and a few numbers, no marshalling, no WPF); only inputs the router would accept for the chosen controller
  are kept. `DeviceStatusChanged` goes through `IUiThread`. The existing 10 Hz poll (`LiveViewModel.Poll` -> `LivePanelViewModel.Poll`) takes a snapshot, builds the
  model with `ControllerModelBuilder` (pure) and replaces it only when its signature changed. While the drawing is not on screen the poll builds only the bar's last-input
  line. No `CompositionTarget.Rendering`, storyboard, animation or timer of its own: every change is an instant step. Lit = bound and pressed or within 300 ms of the last edge
  (400 ms for the wheel and jog, burst ends after 2 s), neutral flash for unbound, dim for unbound at rest; "bound" follows the current scrolling mode and the current settings
  bindings. Four drawings as `Canvas` XAML from `docs/design/*.svg` with parts named by ControlId, painted from `Ilu.Brush.*` only (`DrawingPainter`); sticks, triggers, wheel
  treads, ring arc and jog angle are set from rounded values. The ShuttlePRO v2 drawing was redrawn on 20 Oct 2026 from the unit's product photo (see "ShuttlePRO v2 layout" below).
- **`--demo-input`** (dev only, logs a warning, never default): `Services/DemoInput` feeds a scripted loop of fake mouse-wheel-over-preview events, plus six jog-wheel clicks forward
  (2.0 s) and six back (12.0 s) from a made-up Contour source (`demo-input:jog`, not a device), through `IInputSink.Submit` from a background thread. It never uses Xbox, a real Contour
  or a dedicated mouse (a test asserts the sources). The router counts the jog clicks only when Jog-shuttle is the chosen controller (as for a real one), so with the default
  mouse wheel they are ignored; choosing Jog-shuttle starts the real Contour provider, which is why the build's own runs never did.
- Screenshots (`docs/streams/screens/`, run with `--settings-file <throwaway> --window-size WxH`, mouse wheel, no device): `livepanel-1920x1080-controller.png`,
  `-controller-wheel.png`, `-output-layout.png`, `-hidden.png`, `livepanel-1366x768-bar.png`, `-flyout.png`. The Xbox, ShuttlePRO and ShuttleXpress drawings need hardware to be
  selected, so they are rendered from fake inputs by `DrawingRenderTests` (set `ILU_DRAWING_PNG_DIR`): `livepanel-drawing-*.png` (the ShuttlePRO: `-shuttlepro.png` normal with a lit button and the ring, `-normal-turned.png` (dimples turned by three jog clicks), `-reverse`, `-missing`; the ShuttleXpress `-shuttlexpress.png`; a test also renders every button lit in turn, written only with the variable set).
- Tests: `ControllerPanelTests` (input to lit state, hold, burst, axes, ring, jog, labels, unbound / unknown, status states, battery, filtering, no marshalling per input),
  `LivePanelTests` (tabs, hidden, persistence, fit by height, flyout rules, keys), `DrawingRenderTests`, `ThemeTokenTests` (new size tokens), settings round-trip in Core.Tests.

**ShuttlePRO v2 layout (redrawn 20 Oct 2026).** `Views/Drawings/ShuttleProDrawing.xaml` and `docs/design/controller-shuttlepro.svg` are drawn from the unit's product photo (a vendor photo,
used for reference only and not in the repo): a pear-shaped body 104 x 154 in the 320 x 160 design box, four ovals on a gentle arc, five on an arc curving down at the ends (the outer two
lower, at the shoulders, angled), the ring (outer r 28) round the jog disc (r 19) with three dimples, a tall side button each side, and four long curved buttons below the ring. Both
files come from one script's numbers, so they agree; the body is wider than the real unit's (about 0.67 rather than 0.5 of its height) because the box is only 160 tall and the buttons
would otherwise be too small to read. The "ShuttlePRO v2" wordmark is left out.
- **Numbering (confidence: good, verify on the device).** `Logic/ShuttleProLayout.cs` is the ONE table from the provider's logical numbers (raw bit + 1, unchanged in `ContourModels.cs`) to
  positions; the drawing parts are named by position (`Pos_*`), so a wrong guess is a one-line fix. Source: Contour's ShuttlePRO v2 manual, as reproduced at
  https://www.twistidea.com/manual/contour-shuttle-pro-2-manual/ ("numbered from left to right, from the top row to the bottom row, with the exception of the side buttons that are numbered
  the last": 1 to 4 top row, 5 to 9 second row, 10 and 11 the third row (two buttons below the ring), 12 and 13 the fourth row (above the palm rest), 14 the left side button, 15 the
  right). Consistent with the LinuxCNC `shuttle` driver (https://linuxcnc.org/docs/html/drivers/shuttle.html: the older ShuttlePRO has 13 buttons, the ShuttlePRO2 15, in bits 0 to 14,
  so the two extra, 14 and 15, are the v2's side buttons) and with the Cinelerra-GG manual (K14 and K15 work only on the v2). One community project, shuttle-go
  (https://github.com/smcnally/shuttle-go), uses its own names (F1 to F9, M1 and M2, B1 to B4) and no bit numbers, so it neither confirms nor contradicts it. Not checked on a unit (no
  device is touched, by rule): the order within the two curved pairs (10 upper left, 11 upper right, 12 lower left, 13 lower right) follows the manual's "left to right" and is the part
  most worth a quick test. The default bindings stay by number (DESIGN.md says where they physically sit now: button 3 is the top row's third, not a "centre" one).
- **Jog detents per revolution: 10** (`ContourJog.DetentsPerRevolution`, so 36 degrees a click), from the LinuxCNC `shuttle` driver manual ("a 10 counts/revolution jog wheel with detents",
  for the ShuttleXpress, ShuttlePRO and ShuttlePRO2). One source; Contour's own manual gives no figure. Verify on the device (a one-constant fix).

Deviations from DESIGN.md in this build: the small body leaves the keyboard sentence out of the Missing state (it stays in the chip's tooltip) and drops the detail line;
a persistent row for a stick shows "LS" or "LS X" / "LS Y" when both axes are bound; a quiet axis (inside its deadzone) does not count as "input" for "No input yet" and the last line;
the wheel and jog action label is the router's own conversion of the burst ("Speed - 1.5", "Next line"). A generic HID name draws the ShuttlePRO.

Gaps: the ShuttlePRO numbering and the jog's 10 clicks a turn were taken from documents, not a unit; the Xbox, ShuttlePRO and ShuttleXpress drawings were seen only as renders from fake inputs, not live (no hardware selected, by rule); the LIVE (output on) look of the panel was not
seen on a second display; the small-body Missing layout is tight at the Narrow tier.

## CONTRACT-REQUEST

None. Everything needed was in the frozen contracts.

