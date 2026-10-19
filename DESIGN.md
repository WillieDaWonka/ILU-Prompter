---
# gstack: design-md-format=spec
name: ILU Prompter
description: A dark ILUMINA venue console for running a teleprompter. Grafana's calm blue-grey ground, Poppins and DM Mono, traffic green only for what the presenter is seeing and what is scrolling, and an output screen with nothing on it but the script.
colors:
  backdrop: "#0B0D12"
  canvas: "#111217"
  surface: "#181B1F"
  raised: "#22252B"
  pressed: "#2C3038"
  line: "#262A31"
  line-strong: "#6A707C"
  selection: "#2B4A80"
  text: "#CCCCDC"
  text-muted: "#8F8F9C"
  text-strong: "#ECECF4"
  disabled: "#5A5E68"
  ok: "#596C67"
  on-ok: "#FFFFFF"
  live: "#3EE0A4"
  on-live: "#111217"
  idle: "#3D434D"
  on-idle: "#FFFFFF"
  active: "#0072B2"
  on-active: "#FFFFFF"
  attention: "#D9981F"
  on-attention: "#111217"
  fault: "#D63A2F"
  on-fault: "#FFFFFF"
  action: "#1F60C4"
  action-pressed: "#3274D9"
  on-action: "#FFFFFF"
  focus: "#5794F2"
  margin-overlay: "#1F3274D9"
  script-paper: "#000000"
  script-ink: "#F2F2F2"
  reading-marker: "#8F8F9C"
  end-marker: "#8F8F9C"
  ink-white: "#F2F2F2"
  ink-yellow: "#FFE45C"
  ink-amber: "#FFB547"
  ink-green: "#3EE0A4"
  ink-cyan: "#6FD6FF"
  ink-pink: "#FF8FB8"
  ink-red: "#FF6B6B"
  ink-grey: "#8F8F9C"
  highlight-yellow: "#FFE45C"
  highlight-blue: "#1F4FA0"
  highlight-green: "#1E6B50"
  highlight-magenta: "#7A2160"
  highlight-grey: "#3D434D"
typography:
  title:
    fontFamily: Poppins
    fontWeight: 600
    fontSize: 20px
    lineHeight: 1.3
  heading:
    fontFamily: Poppins
    fontWeight: 600
    fontSize: 14px
    lineHeight: 1.4
  body:
    fontFamily: Poppins
    fontWeight: 400
    fontSize: 14px
    lineHeight: 1.5
  label:
    fontFamily: Poppins
    fontWeight: 500
    fontSize: 13px
    lineHeight: 1.35
  caption:
    fontFamily: Poppins
    fontWeight: 400
    fontSize: 13px
    lineHeight: 1.35
  button:
    fontFamily: Poppins
    fontWeight: 500
    fontSize: 14px
    lineHeight: 1.2
  button-primary:
    fontFamily: Poppins
    fontWeight: 600
    fontSize: 14px
    lineHeight: 1.2
  state:
    fontFamily: Poppins
    fontWeight: 600
    fontSize: 14px
    lineHeight: 1.1
  mono:
    fontFamily: DM Mono
    fontWeight: 500
    fontSize: 14px
    lineHeight: 1.3
  readout:
    fontFamily: DM Mono
    fontWeight: 500
    fontSize: 24px
    lineHeight: 1.1
  icon:
    fontFamily: Segoe Fluent Icons
    fontWeight: 400
    fontSize: 16px
    lineHeight: 1.0
  icon-transport:
    fontFamily: Segoe Fluent Icons
    fontWeight: 400
    fontSize: 20px
    lineHeight: 1.0
  editor:
    fontFamily: Poppins
    fontWeight: 500
    fontSize: 20px
    lineHeight: 1.4
  prompt:
    fontFamily: Poppins
    fontWeight: 500
    fontSize: 92px
    lineHeight: 1.3
  prompt-end:
    fontFamily: Poppins
    fontWeight: 600
    fontSize: 55px
    lineHeight: 1.3
rounded:
  none: 0px
  chip: 2px
  panel: 3px
  control: 4px
  button: 6px
spacing:
  s1: 4px
  s2: 8px
  s3: 12px
  s4: 16px
  s5: 24px
  s6: 32px
  region-gap: 8px
  window-pad: 8px
  panel-pad: 12px
  panel-title-bar: 36px
  app-bar: 44px
  status-bar: 28px
  control-primary: 52px
  control-secondary: 44px
  control-input: 32px
  control-toolbar: 32px
  segmented: 36px
  chip: 28px
  list-row: 32px
  target-gap: 8px
  play-gap: 12px
  sections-width: 240px
  live-column-wide: 720px
  live-column-compact: 480px
  live-column-narrow: 440px
  editor-paper-pad: 24px
  min-window-width: 1024px
  min-window-height: 600px
  panel-tab-bar: 32px
  controller-design-width: 320px
  controller-design-height: 160px
  live-panel-body: 168px
  live-panel-body-small: 120px
  live-panel-flyout-width: 600px
components:
  panel:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text}"
    borderColor: "{colors.line}"
    rounded: "{rounded.panel}"
    padding: "{spacing.panel-pad}"
  app-bar:
    backgroundColor: "{colors.backdrop}"
    textColor: "{colors.text}"
    rounded: "{rounded.none}"
    height: "{spacing.app-bar}"
  status-bar:
    backgroundColor: "{colors.backdrop}"
    textColor: "{colors.text-muted}"
    borderColor: "{colors.line}"
    rounded: "{rounded.none}"
    height: "{spacing.status-bar}"
  button-primary:
    backgroundColor: "{colors.action}"
    textColor: "{colors.on-action}"
    rounded: "{rounded.button}"
    height: "{spacing.control-secondary}"
    padding: 0 16px
  button-primary-hover:
    backgroundColor: "{colors.action-pressed}"
  button-primary-disabled:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.disabled}"
  button-secondary:
    backgroundColor: transparent
    textColor: "{colors.text}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.button}"
    height: "{spacing.control-secondary}"
    padding: 0 16px
  button-secondary-hover:
    backgroundColor: "{colors.raised}"
  button-secondary-pressed:
    backgroundColor: "{colors.pressed}"
    borderColor: "{colors.text-muted}"
  button-secondary-disabled:
    textColor: "{colors.disabled}"
    borderColor: "{colors.line}"
  transport-button:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-strong}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.button}"
    height: "{spacing.control-secondary}"
  transport-button-hover:
    backgroundColor: "{colors.pressed}"
  play-paused:
    backgroundColor: "{colors.action}"
    textColor: "{colors.on-action}"
    rounded: "{rounded.button}"
    height: "{spacing.control-primary}"
  play-scrolling:
    backgroundColor: "{colors.live}"
    textColor: "{colors.on-live}"
    rounded: "{rounded.button}"
    height: "{spacing.control-primary}"
  toolbar-button:
    backgroundColor: transparent
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    size: "{spacing.control-toolbar}"
  toolbar-button-hover:
    backgroundColor: "{colors.raised}"
  toolbar-button-on:
    backgroundColor: "{colors.pressed}"
    textColor: "{colors.text-strong}"
    borderColor: "{colors.line-strong}"
  segmented:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.text}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.control}"
    height: "{spacing.segmented}"
  segmented-selected:
    backgroundColor: "{colors.text}"
    textColor: "{colors.canvas}"
  input:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.text}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.control}"
    height: "{spacing.control-input}"
    padding: 0 8px
  input-focus:
    borderColor: "{colors.focus}"
  input-disabled:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.disabled}"
    borderColor: "{colors.line}"
  chip-ok:
    backgroundColor: "{colors.ok}"
    textColor: "{colors.on-ok}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  chip-live:
    backgroundColor: "{colors.live}"
    textColor: "{colors.on-live}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  chip-idle:
    backgroundColor: "{colors.idle}"
    textColor: "{colors.on-idle}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  chip-active:
    backgroundColor: "{colors.active}"
    textColor: "{colors.on-active}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  chip-attention:
    backgroundColor: "{colors.attention}"
    textColor: "{colors.on-attention}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  chip-fault:
    backgroundColor: "{colors.fault}"
    textColor: "{colors.on-fault}"
    rounded: "{rounded.chip}"
    height: "{spacing.chip}"
  section-row:
    backgroundColor: transparent
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    height: "{spacing.list-row}"
  section-row-current:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-strong}"
    borderColor: "{colors.text-strong}"
  editor-paper:
    backgroundColor: "{colors.script-paper}"
    textColor: "{colors.script-ink}"
    rounded: "{rounded.none}"
    padding: "{spacing.editor-paper-pad}"
  preview-frame:
    backgroundColor: "{colors.script-paper}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.none}"
  preview-frame-live:
    borderColor: "{colors.live}"
  output:
    backgroundColor: "{colors.script-paper}"
    textColor: "{colors.script-ink}"
    rounded: "{rounded.none}"
  menu:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.panel}"
    padding: 4px
  tooltip:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    borderColor: "{colors.line-strong}"
    rounded: "{rounded.chip}"
  settings-window:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.text}"
    rounded: "{rounded.none}"
---

# ILU Prompter

## Overview

**Creative North Star:** "The presenter sees only the words; the operator sees everything." The output is
black with large light text and nothing else. The control window is the venue's own dark console: calm at rest,
green for what is live, red only when something is wrong now.

**Product context:** a Windows desktop app (WPF, .NET 8) that runs a teleprompter at the ILUMINA venue. The
operator sits backstage or at FOH with a laptop, in the dark, often during a live event; the presenter reads a
second display (a confidence monitor or a screen at the stage edge, not mirrored glass) from several metres.
Two windows:
- **Control window** (laptop screen): script editor, sections, settings, a live preview of the output, the
  transport, and status.
- **Output window** (second display): borderless, fullscreen, the script and nothing else.

This file is the visual and wording system. It reuses the house style: the palette, faces and state rules of
`venue-monitoring/DESIGN.md` (the Grafana venue dashboards, the reference the user pointed at), and the desktop
patterns of `SpyderX Colour Corrector/DESIGN.md` (the sibling WPF-like venue console: control heights, focus
ring, voice rules, "no native white control"). Every place this file departs from either is listed in
"Deviations from the house style".

**Mode per surface:**
- Control window, editing: Operate, at arm's length, relaxed. Text editing at the desk before the show.
- Control window, live: Operate under pressure, glanced at. The live column (preview, transport, status) must
  read in a second.
- Output window: Read at a distance, by someone else, continuously. Nothing on it may compete with the script.

**Key characteristics:**
- Dark always, in every window, dialog, menu and tooltip. No light theme; the app ignores the Windows
  light/dark setting.
- Flat, like the Grafana detail pages: `surface` panels on `canvas`, 1 px lines, no glass, no shadows.
- Poppins for every word, DM Mono for every number that changes while you watch.
- Bright green (`live`) appears in two places only: the output chip while the presenter can see the script,
  and the Play button while the script is scrolling.
- Status is a filled chip with a glyph and a word in capitals, from the house state colours.
- The control window never moves; only the script moves, on the output and in its preview. The controller drawing
  (live panel, D7) changes in steps as you press and turn, but nothing in the control window slides, fades or loops.

## Colors

**Strategy:** the Grafana venue palette, unchanged, plus the control greys a desktop app needs, derived in the
same blue-grey lean. One interactive colour (`action` blue). Seven state colours keep their Grafana meanings. A
small set of script inks and highlights exists for the script only.

**Light or dark:** dark. The operator works in a blacked-out backstage or at FOH, next to show lighting; a white
flash from the laptop is visible from the stage and spoils the operator's dark adaptation.

### Chrome (control window)

| Token | Hex | WPF key | Use | Contrast |
|---|---|---|---|---|
| `backdrop` | `#0B0D12` | `Ilu.Color.Backdrop` | app bar, status bar, preview letterbox | |
| `canvas` | `#111217` | `Ilu.Color.Canvas` | window ground, input fill, settings window | |
| `surface` | `#181B1F` | `Ilu.Color.Surface` | panels (sections, editor frame, live column) | |
| `raised` | `#22252B` | `Ilu.Color.Raised` | hover, menus, tooltips, transport buttons, current section row | |
| `pressed` | `#2C3038` | `Ilu.Color.Pressed` | pressed; a toolbar toggle that is on | |
| `line` | `#262A31` | `Ilu.Color.Line` | panel edges, dividers (decorative) | |
| `line-strong` | `#6A707C` | `Ilu.Color.LineStrong` | every control boundary | 3.47 on surface, 3.09 on raised (3:1 non-text) |
| `selection` | `#2B4A80` | `Ilu.Color.Selection` | text selection, selected list row | `script-ink` 7.8, `text` 5.1 |
| `text` | `#CCCCDC` | `Ilu.Color.Text` | all running text and labels | 11.8 canvas, 10.9 surface, 9.7 raised |
| `text-muted` | `#8F8F9C` | `Ilu.Color.TextMuted` | captions, units, hints | 5.9 canvas, 5.4 surface, 4.8 raised |
| `text-strong` | `#ECECF4` | `Ilu.Color.TextStrong` | panel titles, transport icons, current section | 15.9 canvas |
| `disabled` | `#5A5E68` | `Ilu.Color.Disabled` | disabled controls only (exempt from 4.5:1) | 2.7 surface |
| `action` | `#1F60C4` | `Ilu.Color.Action` | the primary button, Play while paused | white on it 5.95 |
| `action-pressed` | `#3274D9` | `Ilu.Color.ActionPressed` | primary hover and press; draggable margin edges | |
| `focus` | `#5794F2` | `Ilu.Color.Focus` | 2 px keyboard focus ring | 5.7 surface, 5.1 raised, 4.4 pressed |
| `margin-overlay` | `#1F3274D9` | `Ilu.Color.MarginOverlay` | preview-only margin tint (`action-pressed` at 12 %, WPF `#AARRGGBB`) | |

`backdrop`, `canvas`, `surface`, `line`, `text`, `text-muted`, `text-strong`, `action` and `action-pressed` are
Grafana's own values. `raised`, `pressed`, `line-strong`, `selection`, `disabled`, `focus` and `margin-overlay`
are new (see "Deviations").

### States (chips, Play button, preview frame)

Grafana's state tokens with Grafana's meanings, applied to a prompter. Text on `live` and `attention` is always
dark (`on-live`, `on-attention`); all other fills take white.

| Token | Hex / ink | Means here | Words it carries |
|---|---|---|---|
| `ok` | `#596C67` / white 5.6 | healthy, nothing to do (ILUMINA green) | READY (a controller connected and answering) |
| `live` | `#3EE0A4` / `#111217` 11.0 | the presenter is seeing it now; the script is moving now | LIVE (output chip), the Play button while scrolling ("❚❚ Pause" on green), SCROLLING |
| `idle` | `#3D434D` / white 10.0 | off or not in use, and that's fine | OUTPUT OFF, PAUSED, AT END, NO 2ND DISPLAY (before the show) |
| `active` | `#0072B2` / white 5.2 | on purpose, informational | BLANK (output deliberately black; Grafana's BLACKOUT meaning) |
| `attention` | `#D9981F` / `#111217` 7.5 | check it; the show can go on | DUPLICATED (Windows mirroring the screens), BATTERY LOW, MISSING (controller lost while the output is not live) |
| `fault` | `#D63A2F` / white 4.7 | act now | NO DISPLAY (the output display vanished while live), MISSING (controller lost while live) |

Rules (carried over from Grafana):
- **Bright green is rare on purpose.** At most two `live` items are on screen: the output chip (LIVE) and the
  Play button while scrolling. Plus the 2 px `live` preview frame, which belongs to the output chip. The current
  section, the current line and connected controllers are not green.
- **One red.** `fault` is the only red in the chrome. Grafana's `recording` crimson is not used: the prompter has
  no recording tally. `ink-red` exists only inside the script.
- **The colour follows the verdict.** A lost controller is `fault` only while the output is LIVE; otherwise it
  is `attention`. The keyboard always works, and every controller chip says so in its tooltip.
- **Never by colour alone.** Every chip carries a glyph and a word (SpyderX D7 / WCAG 1.4.1).
- `fault` and `active` are fills only, never text on the dark ground (`fault` text on `canvas` is 4.0:1).

### Script colours (editor, preview, output only)

The script sits on `script-paper` `#000000` in all three places, so colours in the editor look as they will on
the output. These are content colours, not states; they never appear in the chrome.

| Token | Hex | Name in the menu | On black |
|---|---|---|---|
| `script-ink` / `ink-white` | `#F2F2F2` | White (default) | 18.8 |
| `ink-yellow` | `#FFE45C` | Yellow | 16.5 |
| `ink-amber` | `#FFB547` | Amber | 12.0 |
| `ink-green` | `#3EE0A4` | Green | 12.4 |
| `ink-cyan` | `#6FD6FF` | Cyan | 12.7 |
| `ink-pink` | `#FF8FB8` | Pink | 9.9 |
| `ink-red` | `#FF6B6B` | Red | 7.6 |
| `ink-grey` | `#8F8F9C` | Grey (stage directions, not to be read aloud) | 6.6 |

| Highlight | Hex | Its ink | Contrast |
|---|---|---|---|
| `highlight-yellow` | `#FFE45C` | `#111217` (marker pen) | 14.7 |
| `highlight-blue` | `#1F4FA0` | `#F2F2F2` | 7.0 |
| `highlight-green` | `#1E6B50` | `#F2F2F2` | 5.7 |
| `highlight-magenta` | `#7A2160` | `#F2F2F2` | 8.5 |
| `highlight-grey` | `#3D434D` | `#F2F2F2` | 8.9 |

- A highlight carries its own ink: text inside a highlight takes the ink above, whatever its text colour, so no
  colour pair can fall under 4.5:1. The editor shows exactly that.
- No custom colour picker in v1: the eight inks and five highlights are the whole menu, so every script stays
  readable at distance.
- `script-ink` is `#F2F2F2`, not `#FFFFFF`: about 10 % less light from a large bright display in a dark room, and
  still 18.8:1.

## Typography

Poppins is ILUMINA's typeface (Venue Showcase 2025) and sets every word in the control window and, by default,
the script. DM Mono sets numbers that change while you watch (speed, time remaining, line and section counts,
margin values), because proportional digits shuffle sideways. Both are SIL OFL and bundled with the app as WPF
font resources; the app never fetches fonts.

| Role | Face | Size (DIP) / weight | Line height | WPF style key | Use |
|---|---|---|---|---|---|
| `title` | Poppins | 20 / 600 | 26 | `Ilu.Text.Title` | settings window page titles, empty state |
| `heading` | Poppins | 14 / 600 | 20 | `Ilu.Text.Heading` | panel titles (Grafana `panel-title-wall`), in `text-strong` |
| `body` | Poppins | 14 / 400 | 21 | `Ilu.Text.Body` | running text, menus, inputs |
| `label` | Poppins | 13 / 500 | 18 | `Ilu.Text.Label` | control labels, section rows, transport captions |
| `caption` | Poppins | 13 / 400 | 18 | `Ilu.Text.Caption` | hints, units, status bar; `text-muted`; never smaller |
| `button` | Poppins | 14 / 500 | 17 | `Ilu.Text.Button` | button labels |
| `button-primary` | Poppins | 14 / 600 | 17 | `Ilu.Text.ButtonPrimary` | primary button, Play |
| `state` | Poppins | 14 / 600, capitals | 15 | `Ilu.Text.State` | chip state words (Grafana `state-compact`) |
| `mono` | DM Mono | 14 / 500 | 18 | `Ilu.Text.Mono` | values in inputs, status bar figures, durations in lists |
| `readout` | DM Mono | 24 / 500 | 26 | `Ilu.Text.Readout` | speed and time remaining in the live column (Grafana `readout`) |
| `icon` | Segoe Fluent Icons, else Segoe MDL2 Assets | 16 | 16 | `Ilu.Text.Icon` | toolbar and app bar icons |
| `icon-transport` | Segoe Fluent Icons, else Segoe MDL2 Assets | 20 | 20 | `Ilu.Text.IconTransport` | transport icons |
| `editor` | the output face (Poppins) | 20 / 500 at 100 % zoom | 28 | `Ilu.Text.Editor` | script in the editor |
| `prompt` | the output face (Poppins) | from "lines visible" (92 at the defaults on 1080p) / 500 | 1.3 × size | (output) | script on the output |
| `prompt-end` | Poppins | 0.6 × prompt / 600 | 1.3 × size | (output) | the END marker |

**WPF mapping.**
- DIP = CSS px, so Grafana's px sizes carry over 1:1. Sizes are fixed and never auto-fitted (Grafana rule); text
  never shrinks to fit a control: the control gets wider, or the label shorter.
- Families: `FontFamily="pack://application:,,,/Fonts/#Poppins, Segoe UI"` and
  `"pack://application:,,,/Fonts/#DM Mono, Cascadia Mono, Consolas"`. If WPF exposes a static weight as its own
  family (for example "Poppins Medium"), reference that family and do not add `FontWeight` on top, as SpyderX did
  for "Bahnschrift SemiBold". Check this on the build machine.
- Grafana's 0.04 em tracking on state words is dropped: WPF `TextBlock` has no letter-spacing.
- Line heights use `LineStackingStrategy="BlockLineHeight"` with the absolute `LineHeight` above.
- Icons come from the Windows icon fonts (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on 10), never
  from emoji. Every icon button has a tooltip naming the action and its shortcut.

**Fonts to bundle** (`/Fonts`, build action `Resource`):
- Already in the house repos (`venue-monitoring/grafana/branding/fonts/`): `Poppins-Regular.ttf`,
  `Poppins-Medium.ttf`, `Poppins-SemiBold.ttf`, `DMMono-Regular.ttf`, `DMMono-Medium.ttf`, `OFL-Poppins.txt`,
  `OFL-DMMono.txt`.
- To add from Google Fonts (same OFL licence) for the script's bold and italic: `Poppins-Bold.ttf`,
  `Poppins-Italic.ttf`, `Poppins-MediumItalic.ttf`, `Poppins-SemiBoldItalic.ttf`, `Poppins-BoldItalic.ttf`.
  Without them WPF synthesises a slanted and smeared bold, which reads badly at 90 DIP.

**Script faces.** The output face is a setting. The menu offers four, all legible at distance: **Poppins**
(default, house face), **Segoe UI** (narrower, true bold and italic on every Windows), **Bahnschrift** (the house
display face from SpyderX; its SemiCondensed width fits about 15 % more words per line), **Arial**. "Other
installed font…" is allowed but shows "▲ Check bold and italic on the output" under the picker.

- Base weight 500 (Medium), not 400: thin light-on-black strokes blur at distance (the same reason Grafana stepped
  Poppins Light up to Regular). Bold in the script renders at 700.
- Script sizes are relative to the base, never absolute, because the base comes from "lines visible": Small
  80 %, Normal 100 %, Large 125 %, Huge 150 %. The editor's size menu offers only these four.

## Layout

### Reference windows

| Tier | Window width (DIP) | Typical case | Sections | Live column | Transport labels |
|---|---|---|---|---|---|
| Wide | 1600 and over | 1920 × 1080 at 100 % | 240 px panel | 720 px, live panel (Controller / Output layout) inline | icon + word |
| Compact | 1200 to 1599 | 1366 × 768 at 100 %, 1920 × 1080 at 125 % | 44 px rail, list as a flyout | 480 px, live panel inline small, or its bar plus a flyout | icon only, tooltips |
| Narrow | 1024 to 1199 | 1366 × 768 at 125 % (1093 × 614 DIP) | rail | 440 px, chips move to the app bar, readouts into the status bar, live panel bar plus a flyout | icon only |

Which live panel form a window gets depends on the height left in the column, not on the tier; see "Live panel (D7)".

Minimum window 1024 × 600 DIP. The live column never scrolls; the editor and section list scroll inside their
panels. The window opens maximised on the laptop screen and remembers its size.

**Grid.** Window padding 8, region gap 8 (Grafana's grid gutter), panel padding 12, panel title bar 36. All
values on the 4 px scale (4, 8, 12, 16, 24, 32). Adjacent hit targets are at least 8 px apart; Play stands 12 px
from its neighbours.

### Wireframe, 1920 × 1080 (Wide, maximised: 1920 × 1032 work area)

```
x=0                                                                                                   x=1920
+--------------------------------------------------------------------------------------------------------+
| [Windows title bar, dark: "ILU Prompter - Keynote_Day1.ilup"]                                  32 DIP  |
+--------------------------------------------------------------------------------------------------------+
| A  File Edit Script Output Help  |  Keynote_Day1 · Saved 14:02                [Show output F11] [⚙] 44 |
+--------------------------------------------------------------------------------------------------------+
| B SECTIONS 240  | C SCRIPT EDITOR (flex, 928 at 1920)               | D LIVE COLUMN 720                 |
| +-------------+ | +-----------------------------------------------+ | +-------------------------------+ |
| | Sections  + | | | E  ↶ ↷ | Size ▾ | B I U S | A▾ ▮▾ | Tx | §+ | Q | | |D1 [▶ LIVE · Display 2] [✓ Xbox READY]| |
| |-------------| | +-----------------------------------------------+ | |-------------------------------| |
| |▶ 1 Welcome  | | | F  script-paper #000                          | | |D2 PREVIEW 696 x 392 (16:9)    | |
| |   2m:10s    | | |  ▶ ¦ Good evening and welcome to ILUMINA.     | | | +---------------------------+ | |
| |  2 Sponsors | | |    ¦ Tonight we have three speakers...        | | | |  margin tint  |  margin   | | |
| |  3 Speaker 1| | |    ¦                                          | | | |   ▶  reading line text  ◀ | | |
| |  4 Q&A      | | |  § 2 Sponsors ─────────────────────────────── | | | |      next lines of script | | |
| |  5 Close    | | |    ¦ Our thanks to ...                        | | | +---------------------------+ | |
| |             | | |                                               | | | Output · 1920 × 1080 · 60 Hz  | |
| |             | | |                                               | | |-------------------------------| |
| |             | | |                                               | | |D3 [⏮][⇤][¶↑][↑][▶ Play][↓][¶↓][⇥]| |
| |             | | |                                               | | |  Start Sect Para Line  Line ..| |
| |             | | |                                               | | |D4 [−][====|==●====][+]  4.5   | |
| |             | | |                                               | | |                    ≈ 140 wpm  | |
| |             | | |                                               | | |D5 [Display | Line scrolling]  | |
| |             | | |                                               | | |               [Blank output]  | |
| |             | | |                                               | | |D6 Line 112/384 · 3/7  6m:42s  | |
| |             | | |                                               | | |-------------------------------| |
| |-------------| | |                                               | | |D7 Controller Output layout [v]| |
| |[Add section]| | |                                               | | | [ drawing   ] Xbox controller | |
| +-------------+ | +-----------------------------------------------+ | | [ 320 x 160 ] A  Play / pause | |
|                 |                                                   | +-------------------------------+ |
+--------------------------------------------------------------------------------------------------------+
| G ⌨ Transport keys | Line 112 of 384 · Section 3 of 7 Speaker 1 | 1 912 words | Display 2 · Xbox 70 % 28 |
+--------------------------------------------------------------------------------------------------------+
```

| Region | Size at 1920 × 1032 | Contents |
|---|---|---|
| A app bar | 1920 × 44, `backdrop` | menus; script name and "Saved 14:02"; "Show output" (secondary, F11) and Settings (icon button, Ctrl+,) on the right |
| B sections | 240 × 928, `panel` | title "Sections" with a + icon; section rows; "Add section" (secondary, 44) pinned at the bottom |
| C editor | 928 × 928, `panel` | toolbar (E, 44 tall) above the paper (F, `script-paper`, 24 padding, scrolls) |
| D live column | 720 × 928, `panel` | D1 chips (36), D2 preview (696 × 392 + 24 caption), D3 transport (52 + 18 captions), D4 speed (44), D5 mode and Blank (36), D6 readouts (32), D7 live panel (the rest, about 230 not LIVE, 212 LIVE: tab bar 32 + body 168) |
| G status bar | 1920 × 28, `backdrop` | key focus, position, word count, output display, controller |

Proportions: sections 12.5 %, editor about 48 %, live column 37.5 % of the width. The live column is fixed; the
editor takes whatever is left.

### Wireframe, 1366 × 768 (Compact, maximised: 1366 × 720 work area)

```
+----------------------------------------------------------------------------------------+
| [Windows title bar, dark]                                                       32    |
+----------------------------------------------------------------------------------------+
| A  File Edit Script Output Help | Keynote_Day1 · Saved 14:02   [Show output] [⚙]  44 |
+----------------------------------------------------------------------------------------+
|B | C SCRIPT EDITOR (810)                       | D LIVE COLUMN 480                     |
|≡ | +-----------------------------------------+ | [▶ LIVE · Display 2]  [✓ Xbox READY]  |
|§ | | ↶ ↷ |Size▾| B I U S | A▾ ▮▾ | §+ | ⋯    | | +-----------------------------------+ |
|  | +-----------------------------------------+ | | PREVIEW 456 x 257                 | |
|44| | ▶ ¦ Good evening and welcome to ...     | | |  ▶ reading line              ◀    | |
|  | |   ¦ Tonight we have three speakers...   | | +-----------------------------------+ |
|  | | § 2 Sponsors ─────────────────────────  | | [⏮][⇤][¶↑][↑] [ ▶ ] [↓][¶↓][⇥]       |
|  | |                                         | | [−] [=====|==●=====] [+]  4.5         |
|  | |                                         | | [Display | Line]        [Blank]       |
|  | |                                         | | Line 112/384 · 3/7     6m:42s left    |
|  | +-----------------------------------------+ | Controller  Output layout        [^]  |
+----------------------------------------------------------------------------------------+
| ⌨ Transport keys | Line 112 of 384 · Section 3 of 7 | Display 2 · Xbox 70 %        28   |
+----------------------------------------------------------------------------------------+
```

What changes from Wide, in this order as the window narrows:
1. **Below 1600:** sections become a 44 px rail (≡ opens the list as a 280 px flyout over the editor, Esc or a
   jump closes it); the live column drops to 480; transport captions hide (icons keep tooltips with the
   shortcut); the speed readout loses "≈ 140 wpm" (it moves to the readout tooltip); the mode switch reads
   "Display" | "Line"; "Blank output" reads "Blank"; the D7 live panel's body goes small, or, if the column is too
   short, into a flyout over the editor with only its 32 px tab bar left in the column (see "Live panel (D7)"). The
   editor toolbar folds Find and zoom into a ⋯ overflow menu.
2. **Below 1200:** the live column drops to 440 (preview 416 × 234); the two chips move into the app bar beside
   the script name; the D6 readouts move into the status bar, except time remaining, which stays as `readout` at
   the right of the speed row.
3. **Never:** the preview, Play, the speed control or the chips never hide or scroll. The live panel's tab bar never
   hides either; when the column is short, the transport captions and then the preview's output caption give way to
   it (see "Live panel (D7)"). If the window is smaller than 1024 × 600 DIP, Windows stops the resize there.

## Elevation & Depth

Flat, like the Grafana detail pages and SpyderX: depth comes from the lightness steps `backdrop` < `canvas` <
`surface` < `raised` < `pressed` and 1 px lines. No shadows (`DropShadowEffect` is not used), no gradients, no
glows, no glass. Menus, combo lists, flyouts and tooltips are `raised` with a `line-strong` border. The preview is
the one deliberate inversion: a black window into the output, framed by `line-strong` (2 px `live` while LIVE).

## Shapes

| Radius | WPF key | Used on |
|---|---|---|
| 0 | `Ilu.Radius.None` | editor paper, preview frame, output, app bar, status bar, menu items, tables |
| 2 px (`chip`) | `Ilu.Radius.Chip` | state chips (Grafana `tile`), tooltips, check boxes |
| 3 px (`panel`) | `Ilu.Radius.Panel` | panels, menus, flyouts (Grafana `panel`) |
| 4 px (`control`) | `Ilu.Radius.Control` | inputs, toolbar buttons, segmented switch, section rows |
| 6 px (`button`) | `Ilu.Radius.Button` | push buttons: transport, Play, primary, secondary (Grafana `button`) |

No pills, nothing over 6 px. Radio indicators are circles.

## Components

### State chip (output and controller)

A Grafana tile shrunk to a chip: `chip-*` fill by state, 28 px tall, padding 0 8, radius 2. Content: glyph, the
subject in `label` weight, a `·`, the state word in `state` (capitals). One chip, one subject. No hover state;
click opens the matching Settings page. Tooltip: one sentence on what it means and what to do.

**Output chip** (D1, left):

| Situation | Chip | Tooltip |
|---|---|---|
| Output window closed | `idle` "○ Output · OFF" | "Press F11 to show the script on Display 2." |
| Output open, script showing | `live` "▶ Display 2 · LIVE" | "The presenter can see this. Changes show on Display 2 now." |
| Output open, blanked | `active` "■ Display 2 · BLANK" | "The presenter sees black. Press B to show the script." |
| Windows duplicating the laptop screen | `attention` "▲ Display 2 · DUPLICATED" | "Windows is copying your laptop screen. Set **Windows › Display › Extend these displays** (Win+P › Extend)." |
| Only one display connected | `idle` "○ Output · NO 2ND DISPLAY" (`attention` once you press Show output) | "Connect the presenter's display, then press F11." |
| Chosen display vanished while LIVE | `fault` "✕ Display 2 · NO DISPLAY" | "Display 2 disconnected. The output will come back when it does." |

The output never jumps onto the laptop screen when its display vanishes: it waits, hidden, and returns to the same
display when Windows reports it again.

**Controller chip** (D1, right):

| Situation | Chip |
|---|---|
| Mouse wheel chosen | `ok` "✓ Wheel · READY" (always: a wheel can't report) |
| Xbox or jog-shuttle connected | `ok` "✓ Xbox · READY" / "✓ Shuttle · READY" |
| Xbox battery under 20 % | `attention` "▲ Xbox · BATTERY LOW" |
| Controller lost, output not LIVE | `attention` "▲ Xbox · MISSING" |
| Controller lost, output LIVE | `fault` "✕ Xbox · MISSING" |

Every controller tooltip ends "The keyboard still works: Space plays and pauses."

### Transport bar (D3)

One row, symmetric around Play, backwards on the left and forwards on the right:

| Button | Icon | Caption (Wide) | Tooltip | Size |
|---|---|---|---|---|
| Jump to start | ⏮ | Start | "Jump to start and pause (Home)" | 44 × 44 |
| Previous section | ⇤ | Section | "Previous section (Ctrl+Up)" | 44 × 44 |
| Previous paragraph | ¶↑ | Para | "Previous paragraph (Page Up)" | 44 × 44 |
| Previous line | ↑ | Line | "Previous line (Left or Up in Line scrolling)" | 44 × 44 |
| Play / Pause | ▶ / ❚❚ | Play / Pause | "Play or pause (Space, F5)" | 96 × 52 (72 × 52 Compact) |
| Next line | ↓ | Line | "Next line (Right or Down in Line scrolling)" | 44 × 44 |
| Next paragraph | ¶↓ | Para | "Next paragraph (Page Down)" | 44 × 44 |
| Next section | ⇥ | Section | "Next section (Ctrl+Down)" | 44 × 44 |

- Icons are the Segoe Fluent / MDL2 equivalents of the glyphs above, in `icon-transport`. Captions sit under the
  icons in `caption`; at Compact they hide.
- Transport buttons are `transport-button`: `raised` fill, `line-strong` edge, `text-strong` icon. Hover
  `pressed`; pressed `pressed` with a `text-muted` edge. Disabled (at the start, Previous is disabled; at the end,
  Next): `disabled` icon on a `line` edge.
- **Play** is the view's one primary. Paused: `play-paused` (`action` fill, "▶ Play"). Scrolling: `play-scrolling`
  (`live` fill, dark ink, "❚❚ Pause"). The label names what a press does; the fill says what is happening. At the
  end: `action` fill, "▶ Play" disabled until you jump back, caption "At end".
- Jumps (start, section) are cuts and leave the transport paused at the new place; line and paragraph steps keep
  the current play state.

### Speed control (D4)

`[−]  [ reverse ◀──────┼──●─────▶ forward ]  [+]   4.5  ≈ 140 wpm`

- A horizontal track centred on zero (reverse left, forward right), 8 px tall `line` track, a fill from the
  centre to the thumb (`text` while paused, `live` while scrolling), a 20 px round `text-strong` thumb with a
  `canvas` ring, a 1 px `line-strong` centre tick. Whole row 44 px.
- − and + are 44 × 44 `transport-button`s, step 0.5 (Shift: 0.1). Range −10.0 to +10.0; reverse is allowed but
  never the default.
- The value is `readout` (DM Mono 24), one decimal, with a sign only when negative. "≈ 140 wpm" is `caption`
  `text-muted`, derived from speed and the script's line length.
- In Line scrolling the track hides and the readout reads "Step" with the step delay (`mono`, "0.8 s") when the
  controller auto-steps; otherwise "Manual".
- Drag, click on the track, arrow keys when focused, or the controller. The mouse wheel never changes it (house
  rule: a wheel scrolls; it doesn't edit values), except as the chosen controller over the preview (see below).

### Segmented mode switch (D5)

Two segments: **Display scrolling** | **Line scrolling** (Compact: **Display** | **Line**). 36 px tall,
`segmented`: `canvas` fill, `line-strong` outer edge, radius 4, a 1 px `line-strong` divider. Selected segment:
`text` fill with `canvas` ink (11.8:1), SpyderX's checked look. Unselected hover: `raised`. Keyboard: arrow keys
move the selection; M toggles from anywhere in transport focus. Tooltip per segment: "Smooth, at the set speed" /
"One line per step". Switching while scrolling is allowed and keeps the position.

Beside it, **Blank output**: a 36 px toggle (`button-secondary` off; on: `active` fill, white "■ Blank output").
B or F8.

### Readouts (D6)

One row, 32 px: "Line 112 / 384" and "Section 3 / 7" in `mono` `text`, with labels in `caption`; on the right,
time remaining in `readout`: `6m:42s` plus "left" in `caption`. House duration format (Grafana's `clocks`):
`6m:42s` under an hour, `1h:05m` over. While paused it is the estimate at the set speed, captioned "at 4.5".
At the end it reads `0m:00s` and "At end".

### Preview pane with margin overlay (D2)

- **The preview is the output, scaled.** It lays out the script at the output display's own logical size (for
  example 1920 × 1080) and scales the result with a `Viewbox` / `ScaleTransform`, so every line break, the reading
  line and the margins match the output exactly. It shows the same position, and follows the output within one
  frame.
- Letterboxed in `backdrop`, at the output's aspect ratio. Frame 1 px `line-strong`; 2 px `live` while LIVE.
- **Preview-only overlays** (never on the output):
  - margins tinted `margin-overlay` with a 1 px dashed `focus` inner edge;
  - the reading line extended across the margins as a 1 px dashed `text-muted` line, with "Reading line" in
    `caption` at its left end;
  - while BLANK, the script dimmed to 35 % and a centred `chip-active` "■ BLANK · the presenter sees black", so the
    operator can still navigate.
- **Direct editing:** hovering a margin edge shows a 4 px `action-pressed` handle and the resize cursor; drag to
  change that margin (Shift snaps to 8 px); the reading line drags the same way. While LIVE a caption under the
  preview reads "Changes show on Display 2 now".
- **Mouse wheel controller:** with Wheel chosen, the wheel drives the prompt only while the pointer is over the
  preview (a `caption` hint "Wheel drives the prompt here" appears on its bottom edge). Elsewhere the wheel scrolls
  what it is over. Middle-click over the preview plays and pauses.
- Caption under the frame (`caption`): "Output · 1920 × 1080 · 60 Hz · Display 2 (LG 55UH5)".

### Live panel (D7): Controller and Output layout

The foot of the live column is one panel with two tabs and a hide control. It replaces the old fixed "Output
layout" block. Drafts: `docs/design/live-panel-layout.svg`.

```
 ────────────────────────────────────────────────────────────────────  1 px line
   Controller   Output layout                                    [v]   tab bar, 32
 ━━━━━━━━━━━━━─────────────────────────────────────────────────────────  2 px text-strong under the open tab
 +-- drawing 320 x 160 --+  Xbox controller · player 1   ▮▮▮ Battery full
 |                       |  RT     Speed forward                  62 %
 |  (pressed controls    |  LT     Speed reverse                   0 %
 |   lit, sticks and     |  A      Play / pause
 |   triggers live)      |
 +-----------------------+  Last: A · Play / pause
```

**Tabs, order and default.** Two tabs, left to right: **Controller**, **Output layout**. First run: Controller, panel
open. Controller comes first because it is the show-time view, glanced at to confirm "did my press land, and what
did it do?". Output layout is set-up work done before doors, and it already has two other homes: dragging the
margins on the preview, and Settings › Output. The tab you leave open is remembered, so after set-up the panel stays
where you put it.

**Tab bar** (32 px, on `surface`, a 1 px `line` rule above and below):
- Tabs are text buttons, 32 tall, padding 0 12, both in `heading` (Poppins 14 / 600) so their width never changes.
  Open tab: `text-strong`, with a 2 px `text-strong` bar along its bottom edge (the settings navigation's selected
  bar, turned on its side). The other tab: `text-muted`; hover `text` on `raised`. While the panel is hidden neither
  tab has the bar and both are `text-muted`.
- Hide / show: a 32 × 32 `toolbar-button` at the right end; ChevronDown (E70D) while open, ChevronUp (E70E) while
  hidden. Tooltip "Hide this panel" / "Show this panel". The whole panel is mouse-operated: there is no keyboard shortcut for it
  (user decision, 19 Oct 2026).
- While the controller drawing is not on screen (panel hidden, Output layout open, or the flyout closed), the
  last-input line sits in the bar, right-aligned between the tabs and the toggle, in `caption`: "Last: A · Play /
  pause". It clips at its left edge, with the full text in its tooltip. While the drawing is on screen the line is in
  the panel instead.
- Clicking a tab while hidden opens the panel on that tab. Clicking the open tab does nothing: it never hides the
  panel by accident.

**What hiding frees.** Nothing above the bar moves: the bar stays where it was and the space below it stays empty
`surface`. The preview does not grow. Its size follows the column width and the output's shape, and resizing it
mid-show would rescale what you are watching and move the transport under your pointer (open question 5).

**Where the body goes.** Decided from the window size only, never from state, with the LIVE caption under the
preview counted as if it were showing, so going LIVE never moves or reshapes the panel:

| Height left below D6 in the column | Body | Drawing |
|---|---|---|
| 209 or more (rule 1 + bar 32 + gap 8 + body 168) | inline, under the bar | large: 320 × 160 at 1.0 |
| 161 to 208 (body 120) | inline, under the bar | small: 240 × 120 (0.75) |
| less than 161 | the bar only; the body opens as a flyout over the editor | large if the flyout is 600 wide, else small |

From the reference windows (estimates; measure, don't hard-code): 1920 × 1080 at 100 % (Wide) inline large; 1920 ×
1080 at 125 % (Compact) inline small; 1366 × 768 at 100 % (Compact) and at 125 % (Narrow): bar plus flyout.

**The bar always fits.** If the column has less than 40 px (bar 32 + gap 8) left for it, these give way, in order,
until it fits: (1) the transport captions hide, as at Compact; (2) the preview's output caption ("Output · 1920 ×
1080 · 60 Hz · Display 2 (LG 55UH5)") moves into the preview's tooltip; the LIVE caption ("Changes show on Display 2
now") stays. Nothing else gives way. By the estimates, 1600 × 900 (Wide), 1366 × 768 (Compact), 1093 × 614 (Narrow)
and the 1024 × 600 minimum all fit after step 2 at most.

**Flyout** (when the body can't be inline):
- Over the editor's right edge: its right edge 8 px left of the live column, its bottom level with the bar's
  bottom. Width min(600, editor width − 16); the large drawing when its inner width is at least 576, else the small
  one. Height: title row 28 + 8 + body + padding.
- The menu and flyout skin: `raised`, 1 px `line-strong`, radius 3, padding 12, no shadow. Title row: the open
  tab's name in `heading`, and at the right a 32 × 32 Hide button (Cancel, E711; tooltip "Hide this panel").
  Controls drawn on `raised` take `pressed` as their rest fill (see "Controller drawing").
- Non-modal and quiet: it never takes focus; a click elsewhere doesn't close it (it is a monitor you keep open
  through a show); Esc doesn't close it (Esc keeps its transport meaning, pause).
- It hides while the caret is in the script (keys "Typing") and comes back when the keys return to the transport,
  so it never covers the line you are typing.
- It never opens by itself: it is closed at every launch and whenever the window changes size, and opens only from
  a tab or the toggle (a mouse click).

**Remembered** (app settings, not per script, in `ControlWindow`): `LivePanelTab` (`Controller` | `OutputLayout`,
default `Controller`) and `LivePanelOpen` (default true). An inline body follows `LivePanelOpen` at launch; the
flyout ignores it at launch (above), but opening or hiding the flyout writes it. This is a contract change to
ARCHITECTURE §2.8 `WindowSettings`.

**Keyboard.**
- **No shortcut for the panel.** Showing, hiding and switching tabs are mouse clicks on the tab bar (the user asked for a
  clickable icon tab); F9 and Ctrl+F9 are not used, so nothing else loses a key and no key can hide the panel by accident.
- Focus order, after Blank output: the Controller tab, the Output layout tab, Hide / show, then the open tab's
  fields (Output layout only; the drawing is a readout and takes no focus).
- The tabs and the toggle answer Enter and the mouse only. Space, the arrows, Home, Page Up / Page Down, Ctrl+Up /
  Ctrl+Down, B, M and 0 keep their transport meanings with focus on a tab, because transport keys are handled at
  window level first. So the WPF `TabControl`'s own keys (arrows, Home / End, Ctrl+Tab, Ctrl+Page Up / Down) are not
  used: build the tabs as buttons, or remove the `TabControl`'s key handling.
- In the Output layout number boxes, keys work as in any text input (`app:text`: only the global shortcuts reach
  the transport), as before.

**Screen readers.** The bar is a tab list named "Live panel" with two tabs; the toggle's name is its tooltip. The drawing is named "Controller drawing: Xbox controller" (and so on); the panel's text is plain text. The
last-input line is not a live region: it would speak over every press.

### Output layout tab (D7)

The old D7 content, without its title (the tab is the title):
- **Large body** (168 tall: inline large, or a 600 px flyout): Lines visible (`[−] 8 [+]`) and Line spacing stacked
  in a 200 px column on the left; on the right the margin diagram with its four number boxes and the link toggle
  (152 tall: Top 32, 8, diagram 72, 8, Bottom 32).
- **Small body** (120 tall, no diagram): three rows of 32, 8 apart: `Lines [ 8 ]  Spacing [1.30]  [⛓ Link]` /
  `Left [160] px  Right [160] px` / `Top [ 64] px  Bottom [ 64] px`. The link toggle is the 32 px toolbar toggle,
  chain icon with its word beside it ("Link" / "Linked").
- Settings › Output keeps the full margin editor, as before.

### Controller drawing (D7, Controller tab)

A live, flat line drawing of the controller chosen in Settings › Controller, lit where you press it, with what each
press did. Exact geometry, with every part's `id` set to its ControlId: `docs/design/controller-mouse.svg`,
`controller-xbox.svg`, `controller-shuttlexpress.svg`, `controller-shuttlepro.svg` (the hex values inside them are
these tokens, for previewing only).

**Body layout.** The drawing on the left, the info column on its right, both top-aligned:

| Body | Size | Drawing | Info column |
|---|---|---|---|
| Large | W × 168 (W = 696 inline at Wide, 576 in a 600 px flyout) | 320 × 160 at 1.0, at (0, 4) | x 336 to W: device line 20, detail line 18, gap 8, up to 4 rows of 22, gap 8, last input 18 |
| Small | W × 120 (W = 456 Compact, 416 Narrow) | 240 × 120 at 0.75, at (0, 0) | x 252 to W: device line 20, up to 3 rows of 22, gap 8, last input 18; no detail line |

**Building it.** One `Canvas` per controller in a 320 × 160 design box, each part a `Path`, `Rectangle`, `Ellipse`
or `Line` named by its ControlId (the SVG `id`s, with `.Fill`, `.Dot`, `.Arc`, `.Now`, `.Up`, `.Down`, `.Dimple` for
the moving parts), scaled by one `ScaleTransform` (1.0 or 0.75); stroke widths scale with it. No text inside the
scaled canvas, because text never shrinks below 13 DIP. The one exception is the Xbox A / B / X / Y letters, which
are `TextBlock`s placed over the button centres in the large drawing only (`label`, Poppins 13 / 500). The drawing
has no ground of its own; it sits on the panel. Which drawing: MouseWheel gives the mouse; Xbox gives the Xbox; for
JogShuttle, the **ShuttlePRO v2**, the venue's unit and the most polished drawing: it is shown when the device's name
(`DeviceStatus.DisplayName`) contains "PRO", when no name is known yet (before the first report, or a generic HID name),
and whenever a button above 5 has been seen. Only a device that names itself a ShuttleXpress, and has shown no button
above 5, gets the ShuttleXpress drawing (user decision, 19 Oct 2026).

**Roles and tokens** (no new colour tokens, no hex outside `IluTokens.xaml`):

| Role | Fill | Stroke | Used for |
|---|---|---|---|
| Body | none | `line-strong` 1.5 | the device outline |
| Inert | none | `line-strong` 1 | parts that aren't inputs: the Xbox Guide button, the D-pad centre, the mouse's own left and right buttons |
| Bound, at rest | one step above the ground: `raised` on `surface`, `pressed` in the flyout | `text-muted` 1.5 | a control that does something in the current mode |
| Unbound, at rest | none | `disabled` 1 | dim: a control that does nothing (its letter `disabled`) |
| Lit, bound | `text` | `text` 1.5 | pressed, or inside its hold (its letter `canvas`) |
| Lit, unbound: the neutral flash | `line-strong` | `line-strong` 1.5 | an input that does nothing |
| Indicator | `text` when bound and outside the deadzone; `text-muted` when inside it; `line-strong` when unbound | | stick dots, trigger fills, the ring's arc |
| Deadzone | none | `line-strong` 1, dash 2 2 | stick deadzone rings; the trigger deadzone tick is `text-muted` 1, solid |
| Position now | | `text-strong` 3, round caps | the shuttle ring's current tick |
| Missing | none | `disabled` 1 on every part | the whole drawing while the controller is MISSING: no fills, nothing lit |

Never green (`live` stays for LIVE and scrolling), never `action` blue, never red. "Bound" means bound in the current
scrolling mode (`BindingAction.For(mode)`, `RelativeBinding.For(mode)`), so switching mode can change what is dim.

**Hold.** A button stays lit while it is down and for 300 ms after its last edge, so no control can blink faster
than about three times a second (WCAG 2.3.1) and fast repeats read as one steady light. The wheel and the jog, which
have no release, stay lit for 400 ms after their last notch. Lit and unlit are instant changes; nothing fades.

**Mouse wheel** (`controller-mouse.svg`). The mouse from above, cable end up, on the left; the wheel, enlarged, on
the right, joined by a dashed `line-strong` callout.
- Wheel (`Wheel`): the enlarged wheel is a 40 × 120 rounded rect with ten treads, 12 apart, clipped to it. Each notch
  moves the treads 3 px (a quarter of their spacing): up for a notch away from you, down for a notch towards you. A
  poll moves them by its notches mod 4 in one jump. The up chevron lights for 400 ms after a notch away from you,
  the down chevron after a notch towards you; the small wheel on the mouse lights its edge with them.
- Middle click (`Middle`): the small wheel's fill lights. Side buttons (`XButton2` above `XButton1`) light like any
  button; unbound by default, so they show the neutral flash.
- The mouse's left and right buttons are yours, not the prompt's: inert, never lit.
- Your mouse and a second mouse use the same drawing. With your mouse, only what reaches the preview counts.

**Xbox controller** (`controller-xbox.svg`).
- Sticks (`LeftThumb…`, `RightThumb…`): a ring r 18; a dashed deadzone ring r = max(3, 18 × deadzone) from the stick's
  binding (else XInput's own: 0.24 left, 0.265 right); a dot r 5 at (centre + 13 × (x, −y)), clamped to the unit
  circle. The dot moves even when the stick is unbound (`line-strong`). A stick press lights the ring's stroke
  (`text`, 2.5).
- Triggers (`LeftTrigger`, `RightTrigger`): vertical meters 12 × 60, filled from the bottom by the pull (0 to 1);
  a tick across each at 1 − deadzone (0.12 by default).
- D-pad: four arms, each its own control. A, B, X, Y, the bumpers (LB, RB), View and Menu: each lights on press.
- Battery, in the device line: a 20 × 10 outline (`text-muted` 1) with a 2 × 4 nub and three 4 × 5 segments (`text`):
  Full 3, Medium 2, Low 1 in `attention`, Empty 0 with the outline in `attention`; then the word in `caption`:
  "Battery full", "Battery medium", "Battery low", "Battery empty". Wired: no glyph, the word "Wired". Unknown:
  nothing.

**Contour ShuttleXpress (5 buttons) and ShuttlePRO v2 (15 buttons)** (`controller-shuttlexpress.svg`,
`controller-shuttlepro.svg`). The PRO's button layout is schematic and follows the provider's numbering (left to
right, top to bottom); check it against the unit before building (open question 6).
- Shuttle ring (`Ring`): an annulus with 15 position ticks at 15° steps, −105° to +105° from the top. Position k =
  round(value × 7). A filled arc band runs from the top to 15k°; the tick at k is drawn as "position now". At k = 0
  there is no band and the top tick is `text-muted`.
- Jog wheel (`Jog`): a circle with a dimple at 36° × (detent count mod 10), stepping per poll; its edge lights for
  400 ms after a detent.
- Buttons light on press; the ShuttleXpress's five sit on an arc above the ring, rotated to it.

**Info column.**
- **Device line** (`label`, `text`): the controller's name from the glossary, then its detail: "Mouse wheel · your
  mouse", "Mouse wheel · second mouse", "Xbox controller · player 1", "Jog-shuttle". On the right, the Xbox battery.
- **Detail line** (`caption`, `text-muted`; large body only): "Over the preview only" (your mouse); the second mouse's
  name; "Contour ShuttleXpress" or "Contour ShuttlePRO v2"; nothing for the Xbox.
- **Rows**, 22 tall: `[control 88: label, text-muted] [action: label] [value 64: mono, right-aligned]` (small body:
  control 60, value 44). First, one persistent row for each bound analogue or relative control, in a fixed order
  (triggers, then sticks; ring, then jog; the wheel). Then transient rows for lit buttons, in the controller's own
  order: up to 2 in the large body, 1 in the small; any more show only in the drawing. A lit row: action
  `text-strong`, value `text`. A resting persistent row: all `text-muted`. Rows never reorder by time.
- **Last input** (`caption`, `text-muted`): the most recent input, bound or not: "Last: A · Play / pause", "Last: Y ·
  Not used", "Last: Wheel ▲ 3 · Speed − 1.5".
- Only inputs the router accepts for the chosen controller show: never the keyboard, never another controller kind,
  and for your mouse only what reached the preview.

Short names, for the drawing only (Settings › Controller keeps the long ones from `Labels`):

| Controls | Short name |
|---|---|
| `Wheel`, `Middle`, `XButton1`, `XButton2` | Wheel, Middle, Side 1, Side 2 |
| Xbox | LT, RT, LB, RB, A, B, X, Y, D-pad ↑, D-pad ↓, D-pad ←, D-pad →, View, Menu, LS, RS, LS press, RS press |
| Contour | Ring, Jog, Button 1 … Button 15 |

| Command or role | Action label | Command or role | Action label |
|---|---|---|---|
| PlayPause | Play / pause | SectionNext / Previous | Next section / Previous section |
| Play / Pause | Play / Pause | JumpToStart / End | Jump to start / Jump to end |
| SpeedUp / Down | Speed + / Speed − | ToggleScrollMode | Switch mode |
| SpeedUpFine / DownFine | Speed + fine / Speed − fine | SetDisplay / SetLineScrolling | Display scrolling / Line scrolling |
| SpeedReset | Start speed | ToggleBlank | Blank output |
| LineNext / Previous | Next line / Previous line | ToggleOutput | Output on / off |
| ParagraphNext / Previous | Next para / Previous para | None, or no binding | Not used |
| Axis Speed / Forward / Reverse | Speed / Speed forward / Speed reverse | Relative Speed / Lines / Pixels | Speed + 0.5 (signed amount) / Next line, Previous line / Scroll |

A resting persistent row shows its role without a direction: "Speed", "Speed forward", "Lines", "Scroll".

| Control | Value |
|---|---|
| Trigger | the pull, "0 %" to "100 %" |
| Stick axis (when bound) | signed, "+62 %" |
| Shuttle ring | the position, "−7" to "+7" |
| Wheel | notches in the current burst, "▲ 3" (away from you) or "▼ 2", one decimal for high-resolution wheels; the burst ends after 2 s without a notch, and the value returns to "0" |
| Jog | detents in the current burst, "+4" (clockwise) or "−2"; same 2 s rule |

**States.**

| State | Drawing | Info column | Last-input line in the bar |
|---|---|---|---|
| No controller chosen (keyboard only) | none | centred: "No controller chosen." (`body`), "The keyboard drives the prompt." (`caption`), link button "Choose a controller" (opens Settings › Controller) | "No controller" |
| Chosen, never seen (`NotFound`) | Missing | the controller chip exactly as in D1 (`attention` "▲ Xbox · MISSING"; `fault` while LIVE), then "No Xbox controller found. Connect it, then wait a moment." and "The keyboard still works: Space plays and pauses." in `caption` | "Xbox missing" |
| Lost (`Disconnected`) | Missing | the same chip, then "The Xbox controller stopped answering. Reconnect it." and the keyboard sentence; the last-input line stays | "Xbox missing" |
| Connected, no input yet | at rest | device line; in place of the rows "No input yet." (`label`, `text-muted`) and a hint in `caption`: "Turn the wheel over the preview." / "Turn the wheel." / "Press a button or move a stick." / "Turn the ring or the jog wheel." | "No input yet" |
| Connected, in use | live | device line, rows, last input | "Last: A · Play / pause" |
| Battery low or empty | live | the battery in `attention` (D1 shows BATTERY LOW) | as in use |

Your mouse has no connection to lose: it is always connected, as its chip is always READY.

**Updates: state changes only.** ARCHITECTURE §3 rule 10 forbids per-frame UI-thread work in the control window
while the output is live, so:
- The input thread only records. The `InputObserved` handler writes into a small per-control table (button down or
  up and the time of its last edge; an axis's latest value; a relative control's amount added to an accumulator) and
  keeps the last event. It never touches WPF. A burst of 200 wheel notches a second is 200 table writes, not 200 UI
  updates.
- The UI reads the table on the existing 10 Hz status poll (the `DispatcherTimer` in `MainWindow`) and sets only the
  properties whose value changed. Values are rounded first, so noise changes nothing: stick dots to 1 DIP of the
  design box, trigger fills to 1/60, the ring to its 15 positions, the wheel and jog to whole steps per poll.
- No `CompositionTarget.Rendering`, no storyboards, no animations of any kind (no opacity fades either), no timer of
  its own. Every change is an instant step at poll time: at most 10 visual updates a second.
- While the drawing isn't on screen (panel hidden, the other tab open, the flyout closed or hidden while typing), the
  poll skips it and updates only the bar's last-input line.

### Margin editor with link toggle (Output layout tab, and Settings › Output)

```
                 Top [ 64 ] px
            +----------------------+
 Left [160] |        [ ⛓ ]         | [160] Right
            |        Linked        |
            +----------------------+
               Bottom [ 64 ] px
```

- A small diagram of the output (16:9 outline in `line-strong`, margin bands in `margin-overlay`), one number box
  on each side, and the link toggle in the middle.
- Number boxes: `input`, 72 × 32, value in `mono`, unit "px" in `caption` outside the box, up/down buttons.
  Up/Down keys ±1, Shift ±8. The wheel never changes them. Units are pixels of the output display; the app stores
  them as fractions of that display, so they survive a change of resolution.
- **Link toggle:** a 32 × 32 toolbar-style toggle with the chain icon and a word under it. Off: transparent,
  "Link". On: `toolbar-button-on`, "Linked". Turning it on sets all four to the value of the box you last changed
  (Left if none). While linked, changing any box changes all four.
- With the diagram: **Lines visible** `[−] 8 [+]` (4 to 16) and **Line spacing** (1.0 to 2.0, step 0.05), so all
  output layout sits in one place (beside the diagram in the Output layout tab, above it in Settings).

### Editor toolbar (E)

44 px tall, on `surface`, groups separated by 1 px `line` dividers 20 px tall:

| Group | Controls |
|---|---|
| History | Undo, Redo |
| Size | drop-down "Normal ▾" (Small, Normal, Large, Huge) |
| Style | Bold, Italic, Underline, Strikethrough (toggles) |
| Colour | Text colour ▾ (the eight inks as 20 px swatches with names), Highlight ▾ (none + five highlights) |
| Clear | Clear formatting |
| Structure | Insert section marker (§+) |
| Right side | Find (Ctrl+F), editor zoom "100 % ▾" (70 to 200 %) |

- `toolbar-button`: 32 × 32, `icon` in `text`, transparent; hover `raised`; on `toolbar-button-on` (`pressed`
  fill, `text-strong` icon, `line-strong` edge), so "on" differs from hover by its edge. Colour buttons show the
  current colour as a 4 px bar under the icon.
- The toolbar reflects the formatting at the caret, like any word processor.
- **Paper (F):** `editor-paper`, the script in its own output face at `editor` size × zoom. Left gutter 24 px:
  the reading line's position shows as a `reading-marker` ▶; the line on the reading line gets a `raised`
  band behind it (never green). Double-click in the gutter: "Jump the prompt here".
- **Section markers** in the paper: a non-editable full-width row, "§ 2 Sponsors" in `label` `text-muted`,
  followed by a 1 px `line-strong` rule. Double-click renames it inline.
- **Editing while scrolling** (proposal): allowed, because a script is often fixed during a show. While LIVE the
  editor's left edge carries a 2 px `live` bar and the caption "Changes show on Display 2 as you type". Changes
  above the reading line never move the line the presenter is reading.
- Selection is `selection`; spell-check underlines use `attention` and never reach the output.

### Section list (B)

- Title "Sections" (`heading`), a + icon button (Insert section marker at the caret).
- Rows `section-row`, 32 px (two lines, 48 px, when a duration is shown): number and name in `label`, the
  estimated duration at the current speed in `mono` `text-muted` ("2m:10s"). Names never ellipsise mid-word: they
  wrap to two lines, then clip with the full name in the tooltip.
- **Current section** (the one at the reading line): `section-row-current`, `raised` fill, a 3 px `text-strong`
  left bar, ▶ glyph, `text-strong` name. Not green.
- Single click: shows that section in the editor (the prompt doesn't move). Double-click or Enter: "Jump the
  prompt to this section" (a cut, then paused). Right-click: Rename, Delete marker.
- Empty state: "No sections yet. Press Ctrl+Shift+Enter in the script to add one." in `caption`.

### Settings window

A non-modal, owned window (720 × 560 DIP minimum), so the operator can change layout while watching the preview.
Dark title bar, `canvas` ground, left navigation on `surface` (200 px, rows 32 px, selected row `raised` with a
3 px `text-strong` left bar). Changes apply as you make them: there is no OK or Apply (SpyderX rule 2). Bottom row:
"Reset this page to defaults" (link, with a confirmation) on the left, "Close" (secondary) on the right; Esc
closes.

| Page | Contents |
|---|---|
| Controller | Choose the controller as three 64 px selectable cards (Mouse wheel / Xbox controller / Jog-shuttle), each with an icon, a one-line description and its live chip; the selected card has a `text` 2 px edge and a ✓. Below: the mapping table for the chosen controller (read-only in v1), speed sensitivity, "Test: move the controller" with a live readout |
| Output | Display picker ("Display 2 · LG 55UH5 · 1920 × 1080"), the margin editor, lines visible, line spacing, reading line position (10 to 60 % of the text area, default 33 %), reading-line marker on/off, "Dim lines above the reading line" on/off, "Show section names on the output" on/off |
| Text | Output face (four plus "Other…"), weight (Regular / Medium), default ink, editor zoom default |
| Scrolling | default mode, speed step, start speed, acceleration (Gentle / Normal / Quick), "Pause at section markers" on/off |
| Shortcuts | the keyboard map below, read-only, with "Print this list" |
| About | the ILUMINA logo, version, licences (Poppins and DM Mono OFL) |

Check boxes and radios follow SpyderX: 22 px indicators, `line-strong` rim on `canvas`, checked a `text` fill
with a dark tick; the whole row is the hit target. Drop-downs have a 28 px arrow button divided by a
`line-strong` rule.

### App bar, menus, dialogs, status bar

- App bar (A): menus in `body`, script name in `label` `text-strong`, "Saved 14:02" in `caption`. Unsaved:
  "● Not saved" in `caption` `text`.
- Menus and context menus: `menu` (`raised`, `line-strong` edge, radius 3), items 32 px, hover `pressed`,
  shortcut text right-aligned in `caption` `text-muted`.
- Message boxes: an app dialog in the same skin (never the native white `MessageBox`), title in `heading`, one
  primary (verb + object) and one secondary.
- Status bar (G): `caption` in `text-muted` with values in `mono`: key focus, position, words, output display,
  controller. Transient notes ("Saved") show here for 8 s.

## Output window

**Rule: the output carries no UI chrome.** It shows the script, and optionally the reading-line marker and the
END marker. Never: a title bar, border, cursor, scroll bar, focus rectangle, selection, caret, spell-check
underline, section marker row, logo, clock, chip, toast, tooltip or error message. Problems are reported in the
control window only.

**Window.** WPF `WindowStyle="None"`, `ResizeMode="NoResize"`, `AllowsTransparency="False"`, `Topmost="True"`,
`ShowInTaskbar="False"`, `ShowActivated="False"`, not focusable (`WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW` so it stays
out of Alt+Tab), `Cursor="None"`, sized to the chosen display's full bounds (not the work area). Background
`script-paper` from the first frame, so it never flashes white. Keys always go to the control window. Not
mirrored; there is no mirror setting in v1 (direct-view display).

**Defaults** (1920 × 1080 output):

| Setting | Default | Range | Notes |
|---|---|---|---|
| Background | `#000000` | fixed | |
| Ink | `#F2F2F2` | the eight inks | per run of text in the script |
| Face | Poppins Medium (500) | four faces + other | bold renders 700 |
| Lines visible | 8 | 4 to 16 | counts Normal-size lines in the text area |
| Font size | derived: 92 DIP | | `size = (height − top − bottom) / (lines × spacing)`; (1080 − 64 − 64) / (8 × 1.3) = 91.5 |
| Line spacing | 1.3 | 1.0 to 2.0 | `LineHeight = spacing × size`, `BlockLineHeight` |
| Paragraph gap | 0.5 line | | |
| Margins | L 160, R 160, T 64, B 64 px | 0 to 40 % | about 29 characters (5 words) per line at the defaults |
| Text alignment | left | left / centre | ragged right; never justified (rivers at large sizes) |
| Reading line | 33 % down the text area | 10 to 60 % | the line to read sits here |
| Reading-line marker | on | on / off | see below |
| Dim above the reading line | off | 45 % opacity when on | |
| Section names on output | off | | when on: `caption`-proportioned (0.4 × size), `ink-grey`, capitals |
| Start position | the first line on the reading line | | the space above is black |

Sizing by lines visible keeps the output independent of the display's resolution and Windows scaling: a 4K
television at 150 % shows the same 8 lines as a 1080p monitor.

**Reading-line marker.** Two solid triangles, ▶ in the left margin and ◀ in the right, centred vertically on the
reading line, height 0.5 × line height, colour `reading-marker` `#8F8F9C` (6.6:1 on black: findable, not bright).
If a margin is narrower than 0.6 × line height, that side's marker becomes a 6 px bar at the screen edge. No line
across the text.

**End-of-script marker.** After the last line: one blank line, then a centred `── END ──` (`prompt-end`, 0.6 ×
size, `end-marker` grey, rules 2 px `line-strong`, 30 % of the text width), then black space so END can reach the
reading line. Scrolling stops by itself when END reaches the reading line; the transport shows "At end".

**Rendering.** Formatting maps one-to-one from the editor: bold 700, italic the true italic face, underline and
strikethrough at 0.06 em thickness in the run's ink, highlights as a fill 0.1 em wider than the run on each side.
Motion follows "Motion" below; the output must never judder or tear.

## Keyboard shortcuts (proposal)

**Two key modes, always shown in the status bar.** When the caret is in the script, keys type ("⌨ Typing"); press
Esc to hand the keys to the transport ("⌨ Transport keys"). Clicking the preview, transport or sections also gives
the keys to the transport. Function keys and the global shortcuts work in both modes, so the show can always be
paused while typing.

**Global (both modes):**

| Key | Action |
|---|---|
| F5 | Play / pause |
| F8 | Blank output on / off |
| F11 | Show / hide the output window |
| Esc | In the script: leave the script (keys go to the transport). In transport: pause (never blanks, never closes) |
| Ctrl+, | Settings |
| Ctrl+/ | Shortcut list |
| Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S | New, open, save, save as |
| F2 | Go to the script (keys type) |

**Transport keys** (when keys are with the transport):

| Key | Display scrolling | Line scrolling |
|---|---|---|
| Space | play / pause | play / pause (auto-step) |
| Down / Up | speed +0.5 / −0.5 (Shift: 0.1) | next / previous line |
| Right / Left | next / previous line (jog) | next / previous line |
| Page Down / Page Up | next / previous paragraph | next / previous paragraph |
| Ctrl+Down / Ctrl+Up | next / previous section | next / previous section |
| Home | jump to start and pause | jump to start and pause |
| B | blank output on / off | blank output on / off |
| M | switch to Line scrolling | switch to Display scrolling |
| 0 | speed back to the start speed | |

Presenter clickers send Page Down / Page Up and Right / Left, so a clicker works as a controller with no setup.

Transport keys are handled at window level (`PreviewKeyDown`), so Space always plays and pauses, even when a
transport button has keyboard focus (WPF would otherwise press that button); Enter presses the focused button.

**Script keys** (while typing; WPF `RichTextBox` built-ins kept):

| Key | Action |
|---|---|
| Ctrl+B / Ctrl+I / Ctrl+U | bold / italic / underline |
| Ctrl+Shift+X | strikethrough |
| Ctrl+Shift+H | highlight with the last highlight |
| Ctrl+] / Ctrl+[ | size up / down one step (Small, Normal, Large, Huge); replaces the built-in point-size step |
| Ctrl+Space | clear formatting |
| Ctrl+Shift+Enter | insert section marker |
| Ctrl+F / F3 | find / find next |
| Ctrl+Z / Ctrl+Y | undo / redo |
| Ctrl+= / Ctrl+− | editor zoom (the output is unaffected) |

Not used, on purpose: single-letter keys while typing; any single key that closes the output or deletes. Stop does
not exist: pausing is always enough.

**Controller defaults** (proposal, shown read-only in Settings › Controller):

| Controller | Speed | Play / pause | Lines | Paragraphs | Sections | Blank |
|---|---|---|---|---|---|---|
| Mouse wheel (over the preview) | Display: one notch ±0.5 | middle click | Line: one notch = one line | | | |
| Xbox controller | right trigger forward, left trigger reverse (analogue) | A | D-pad up / down | LB / RB | D-pad left / right | View button |
| Jog-shuttle | shuttle ring (±7 positions) | button 3 (the centre one) | jog wheel, one click = one line | buttons 2 / 4 | buttons 1 / 5 | button 6 (ShuttlePRO) |

Also bound on the ShuttlePRO v2: button 7 Jump to start, button 8 Switch mode. Not bound by default: the mouse's side
buttons; Xbox B, X, Y, Menu, both sticks and stick presses; ShuttlePRO buttons 9 to 15. (This row was corrected on
19 Oct 2026 to match `DefaultBindings`, which is the source of truth.)

## States

| State | Buttons | Inputs, toggles, rows | Chips |
|---|---|---|---|
| Rest | as specified per component | `line-strong` edge | state fill |
| Hover | `raised` (transparent ones) or `pressed` (transport); primary `action-pressed` | rim `text-muted` | none (not interactive by hover) |
| Pressed | `pressed`, edge `text-muted`; primary `action-pressed` | | |
| Focus (keyboard) | 2 px `focus` ring outside the control; on `action` and `live` fills a 2 px `canvas` gap between fill and ring | 2 px `focus` edge, padding reduced 1 px so nothing shifts | focusable only in Settings |
| Disabled | `disabled` text, `line` edge, fill removed (primary: `raised` fill) | `surface` fill, `disabled` text | n/a |
| Toggled on | `toolbar-button-on`; Blank output on: `active` fill | `text` fill, dark tick | |
| Scrolling | Play `live` "❚❚ Pause"; speed fill `live` | | `live` SCROLLING in the readout tooltip |
| LIVE (presenter sees the script) | | editor left bar 2 px `live`; preview frame 2 px `live` | output chip `live` LIVE |
| BLANK | Blank output `active` | preview dimmed 35 % | output chip `active` BLANK |
| Fault | | | `fault` chip; no flashing, no sound |

Focus is drawn with WPF `FocusVisualStyle` replaced by a template trigger on `IsKeyboardFocused`, so mouse clicks
never show a ring. Focus order: app bar, sections, editor toolbar, script, preview, transport (Play first), speed,
mode, Blank, live panel (Controller tab, Output layout tab, Hide / show, then the Output layout fields when open).

## Accessibility and the dark venue

- **No white, anywhere.** Every window, dialog, menu, tooltip, combo list, scroll bar and title bar is dark
  (`DWMWA_USE_IMMERSIVE_DARK_MODE` on every window). The window background is set in XAML so the first frame is
  `canvas`, not white. Known exception: the Windows Open and Save dialogs follow the Windows theme; set the
  operator laptop to Windows dark mode (**Settings › Personalisation › Colours › Dark**).
- **Contrast.** All text 4.5:1 or better on its ground (table under Colors); `text-muted` is 4.8:1 even on
  `raised`. Control edges `line-strong` are 3:1 or better on `surface` and `raised`. Focus ring 4.4:1 or better on
  every ground. Script inks 6.6:1 to 18.8:1 on black; highlights 5.7:1 to 14.7:1 with their inks.
- **Colour-blind safety.** The chip fills are Grafana's, chosen for at least 11 ΔE2000 between every pair for
  protan, deutan and tritan vision; every chip also has a glyph and a word.
- **Sizes.** Nothing under 13 DIP. Play 52 px, transport 44 px, toolbar and inputs 32 px, targets 8 px apart, Play
  12 px. Everything is reachable by keyboard; the live column is reachable without the mouse at all.
- **Glare.** The control window's brightest large areas are the Play button (scrolling) and the selected mode
  segment; nothing larger than a button is lighter than `pressed`. The output uses `#F2F2F2`, not pure white.
- **Motion safety.** Nothing flashes or blinks (WCAG 2.3.1); the only moving thing is the script. The controller
  drawing follows your hands in steps (at most 10 a second), each control holds its light at least 300 ms (so at most
  about three changes a second), and every lit part is far smaller than the general flash threshold's area.
- **Windows High Contrast.** When it is on, the control window switches to `SystemColors` brushes; the output
  keeps black and the script's inks (it is content the presenter needs).
- **Screen readers.** Every icon button has `AutomationProperties.Name` (its tooltip text without the shortcut)
  and `AutomationProperties.AcceleratorKey`; chips expose their full sentence.
- **No sound.** Unlike SpyderX, the app makes no sounds: it runs next to live microphones.

## Voice and wording

The SpyderX voice rules, kept, with Grafana's capitals for state words:

1. Action or result first, in 12 words or fewer; the reason second.
2. Buttons are a verb plus an object ("Show output", "Jump to start", "Add section"). Never a bare OK, Apply,
   Yes or No. "…" only when more input follows.
3. Every warning says what happened, why it matters and what to do, and the "what to do" is a key or a button
   where possible.
4. One term per concept (glossary below). Windows paths in bold with ›: **Settings › System › Display**.
5. Sentence case, British spelling ("colour", "centre", "personalisation"), a space before units ("160 px",
   "33 %", "1920 × 1080"). No capitals for emphasis.
6. **State words in chips are capitals** (Grafana): LIVE, BLANK, READY, MISSING, BATTERY LOW. One or two words.
   Everything else is sentence case.
7. Glance states use 4 words or fewer; tooltips one sentence.
8. Durations in the house format: `6m:42s`, `1h:05m`.

| Concept | Use | Retire |
|---|---|---|
| the text being read | **script** | document, text, copy, prompt (as a noun) |
| the presenter's screen | **output** (the window), **Display 2** (the screen) | prompter screen, talent monitor, second screen |
| the person reading | **presenter** | talent, speaker (except as a section name) |
| the person at the laptop | **you** | operator (in UI text) |
| mouse wheel / Xbox / jog-shuttle | **controller** | input device, remote |
| the marked line | **reading line** | cue line, eye line, arrow |
| a named part of the script | **section** ("section marker" in the editor) | chapter, bookmark, cue |
| one row of text as shown on the output | **line** | row, sentence |
| how fast it moves | **speed** | velocity, rate |
| the two modes | **Display scrolling**, **Line scrolling** | continuous, step mode |
| black screen on purpose | **Blank output** / BLANK | blackout, hide, freeze, black |
| go back to the top | **Jump to start** | reset, rewind, top |
| move by a line | **next line**, **previous line**; "jog" only for the jog wheel | nudge, step |
| the end | **END** (output), **At end** (control window) | finish, done |

Before and after, for the tone: "No secondary monitor detected" → "Connect the presenter's display, then press
F11."; "Controller disconnected!" → "Xbox · MISSING", tooltip "The Xbox controller stopped answering. The keyboard
still works: Space plays and pauses."; "Are you sure?" → "Close the output? The presenter's screen goes to the
Windows desktop." with "Keep output" (default) and "Close output".

## Motion

The house rule is "no motion", and the control window keeps it: hover, press, focus and state changes are
instant, nothing blinks, pulses, fades or slides. The script is the one exception, because a prompter's job is to
move text; it moves on the output and in its preview only.

| Token | WPF key | Value | Use |
|---|---|---|---|
| `ui` | `Ilu.Motion.Ui` | 0 ms | every control-window change |
| `ramp-start` | `Ilu.Motion.RampStart` | 400 ms, linear velocity ramp | Play: 0 to the set speed |
| `ramp-stop` | `Ilu.Motion.RampStop` | 200 ms, linear | Pause: the set speed to 0 |
| `ramp-change` | `Ilu.Motion.RampChange` | 300 ms, linear | a speed change while scrolling (Gentle 600, Quick 150) |
| `line-step` | `Ilu.Motion.LineStep` | 220 ms, cubic ease-out | one line (Line scrolling, jog) |
| `paragraph-step` | `Ilu.Motion.ParagraphStep` | 320 ms, cubic ease-out | one paragraph |
| `jump` | `Ilu.Motion.Jump` | 0 ms (a cut) | start, section, "jump the prompt here" |

- Position is computed from elapsed time (a `Stopwatch` read on `CompositionTarget.Rendering`), never by frame
  count, and applied as a `TranslateTransform` on text laid out once, so the scroll is smooth at the display's
  refresh rate. No layout pass per frame.
- Velocity ramps, never position easing, during continuous scrolling: the presenter's eyes track a steady speed.
- Repeated line steps within 220 ms chain into one smooth move; they never queue up behind each other.
- **The controller drawing is a readout, not motion.** It changes in instant steps on the 10 Hz status poll: lights
  on and off, stick dots, trigger fills, the ring's arc, wheel treads and the jog dimple jump to their new state, never
  tween. No storyboard, fade, `CompositionTarget.Rendering` or timer of its own (ARCHITECTURE §3 rule 10). Its
  timings are input rules, not motion tokens: hold 300 ms (buttons), 400 ms (wheel, jog), burst end 2 s.

## Brand assets

- ILUMINA logo, white on transparent, 1181 × 332 PNG: `venue-monitoring/grafana/branding/wall-logo.png` (the same
  file is `ILU Service Wrapper/site/assets/logo.png`). Used on the About page and as the editor's empty state (30 %
  opacity, as on the walls). Never on the output.
- Footer logos (ILUMINA and Harry the Hirer), 355 × 64 PNG: `venue-monitoring/grafana/branding/footer-logos.png`.
  Not used in the app.
- Fonts and licences: `venue-monitoring/grafana/branding/fonts/` (Poppins Regular, Medium, SemiBold; DM Mono
  Regular, Medium; `OFL-Poppins.txt`, `OFL-DMMono.txt`); copies in `ILU Service Wrapper/site/assets/fonts/`.
- No app icon exists in the repos. One is needed (`.ico`, 16 to 256 px): proposed as the ILUMINA mark on `canvas`
  with a `live` reading-line triangle.

## WPF resource dictionary (how the tokens land)

One `Themes/IluTokens.xaml` holds every token; nothing else in the app contains a hex literal (SpyderX's test
rule, ported: a unit test compares the dictionary with this file's front matter and fails on any `#RRGGBB` in XAML
or C# outside it). Excerpt:

```xml
<Color x:Key="Ilu.Color.Canvas">#111217</Color>
<SolidColorBrush x:Key="Ilu.Brush.Canvas" Color="{StaticResource Ilu.Color.Canvas}"/>
<Color x:Key="Ilu.Color.Live">#3EE0A4</Color>
<Color x:Key="Ilu.Color.MarginOverlay">#1F3274D9</Color>
<FontFamily x:Key="Ilu.Font.Ui">pack://application:,,,/Fonts/#Poppins, Segoe UI</FontFamily>
<FontFamily x:Key="Ilu.Font.Mono">pack://application:,,,/Fonts/#DM Mono, Cascadia Mono, Consolas</FontFamily>
<sys:Double x:Key="Ilu.Size.ControlPrimary">52</sys:Double>
<CornerRadius x:Key="Ilu.Radius.Button">6</CornerRadius>
<Thickness x:Key="Ilu.Pad.Panel">12</Thickness>
<Duration x:Key="Ilu.Motion.LineStep">0:0:0.220</Duration>
```

Key pattern: `Ilu.Color.*` (Color), `Ilu.Brush.*` (SolidColorBrush), `Ilu.Font.*` (FontFamily), `Ilu.Text.*`
(TextBlock styles), `Ilu.Size.*` (Double), `Ilu.Radius.*` (CornerRadius), `Ilu.Pad.*` (Thickness),
`Ilu.Motion.*` (Duration). Front-matter names map by PascalCase: `line-strong` → `Ilu.Color.LineStrong`.

**Sizes added with the live panel build.** The six sizes below are in the front matter's `spacing` block and in
`IluTokens.xaml` (`Ilu.Size.PanelTabBar`, `ControllerDesignWidth`, `ControllerDesignHeight`, `LivePanelBody`,
`LivePanelBodySmall`, `LivePanelFlyoutWidth`), added in one commit because `ThemeTokenTests` compares the two both
ways. No colour tokens are new.

| Front-matter name (`spacing`) | Value | Use |
|---|---|---|
| `panel-tab-bar` | 32px | live panel tab bar |
| `controller-design-width` | 320px | controller drawing design box |
| `controller-design-height` | 160px | controller drawing design box |
| `live-panel-body` | 168px | large body |
| `live-panel-body-small` | 120px | small body |
| `live-panel-flyout-width` | 600px | flyout, at most |

## Do's and Don'ts

- Do: keep the output to the script, the reading-line marker and END. Report every problem in the control window.
- Do: show the output's state at all times in the output chip, and frame the preview in green while LIVE.
- Do: keep `live` green for the two things live now (the presenter sees it; it is scrolling).
- Do: give every chip a glyph and a capital state word; every icon button a tooltip with its shortcut.
- Do: lay out the preview at the output's real size and scale it, so its line breaks are the output's.
- Do: use DM Mono for every changing number, Poppins for every word.
- Don't: use red for anything but a fault that needs action now; don't use Grafana's `recording` crimson.
- Don't: let any native white control, dialog or flash appear, or set a white editor page.
- Don't: animate the control window, blink a chip, or play a sound. The controller drawing steps; it never tweens,
  fades or loops, and never lights green.
- Don't: let the live panel take a transport key (Space, arrows, Home, Page Up / Down) for its tabs.
- Don't: auto-fit or shrink text; don't ellipsise a section name mid-word.
- Don't: let the mouse wheel change a setting value; it scrolls, or drives the prompt over the preview.
- Don't: move the output onto the laptop screen, ever, even when its display disappears.
- Don't: offer free colour pickers for the script; use the eight inks and five highlights.

## Deviations from the house style

| # | Where | House rule | This app | Why |
|---|---|---|---|---|
| 1 | Surfaces | Grafana walls: glass panels over the logo backdrop | Flat, Grafana detail-page style | Glass is "walls only" by the Grafana rule; a working console is a detail page. WPF blur is also costly next to a 60 fps scroll |
| 2 | Chrome tokens | Grafana has only `canvas`, `surface`, `line`, three text tones | Adds `raised`, `pressed`, `line-strong`, `selection`, `disabled` in the same blue-grey lean | A desktop app needs hover, press, control edges (3:1) and selection that Grafana's touch walls don't |
| 3 | Focus ring | SpyderX: amber ring | `focus` `#5794F2` (Grafana's own blue), a new token | Amber means "check it" in the Grafana palette; `action` `#1F60C4` is only 3.1:1 on `canvas` and fails on `raised` |
| 4 | Primary colour | SpyderX: amber primary, no blue anywhere | Grafana's `action` blue primary | The prompter needs `attention` amber for warnings (DUPLICATED, BATTERY LOW); SpyderX's "no blue" protects LED measurements, which don't apply here |
| 5 | Greys | SpyderX: every grey R = G = B | Grafana's blue-leaning greys | The neutral rule exists so the laptop doesn't tint a colour measurement; this app follows the dashboards the user pointed at |
| 6 | Faces | SpyderX: Segoe UI, nothing bundled | Poppins and DM Mono bundled (OFL), Segoe UI fallback | Poppins is ILUMINA's typeface and the Grafana face; WPF embeds fonts with no installer step. Needs five more Poppins files for bold and italic |
| 7 | Status display | SpyderX: coloured text and glyph, no fills; Grafana: filled tiles | Filled chips (Grafana) with a glyph (SpyderX) | Grafana's fills have verified contrast and colour-blind spacing; `fault` as text would be only 4.0:1. The glyph keeps WCAG 1.4.1 |
| 8 | State word casing | SpyderX: sentence case | Grafana capitals in chips only | They are status words read at a glance, as on the walls; all other text stays sentence case |
| 9 | Motion | Both: no motion | The script scrolls, with ramps and steps; the control window stays still | Moving text is the product. Motion is confined to the output and its preview |
| 10 | Sound | SpyderX: sound as a status channel | No sound | The app runs next to live microphones and the operator faces the laptop |
| 11 | Tracking | Grafana: 0.04 em on state words | None | WPF `TextBlock` has no letter-spacing |
| 12 | Radii | Grafana: 2 / 3 / 6 (detail pages); SpyderX: 2 / 4 / 6 | 0 / 2 / 3 / 4 / 6 | Grafana's three plus SpyderX's 4 px for inputs and toolbar buttons, which need a step between chip and button |
| 13 | New colour sets | none in either | Script inks, highlights, `script-paper`, `script-ink`, `reading-marker`, `end-marker`, `margin-overlay` | The script needs content colours tuned for black at distance; kept out of the chrome so they never read as states. `ink-green` shares `live`'s hex but appears only inside the script |
| 14 | Units | Grafana px; SpyderX pt | DIP | WPF's unit; 1 DIP = 1 CSS px, so Grafana's sizes carry over unchanged |
| 15 | Control-window motion | Both: no motion; this file: "the control window never moves" | The controller drawing changes in steps up to 10 times a second (lights, stick dots, trigger fills, ring arc, wheel treads), never tweened | It is a readout of your hands, like the speed readout. Steps on the existing 10 Hz poll keep ARCHITECTURE §3 rule 10 (no per-frame work while LIVE) |
| 16 | Flashing | "Nothing flashes or blinks" | Controls light on press, held at least 300 ms (400 ms for the wheel and jog) | So a control changes at most about three times a second, in areas far below WCAG 2.3.1's flash threshold; fast repeats read as one steady light |
| 17 | Illustration | Neither house file draws hardware | Flat line drawings of four controllers, from existing tokens only (`line-strong` outline, `text` when lit) | The user asked to see the controller react. Schematic, not photographic: no shading, no gradients, no product colours, no logos |
| 18 | Tabs | No tab pattern in either; Windows tabs move with the arrows and Ctrl+Tab | Text tabs with a 2 px `text-strong` bar under the open one; Enter and the mouse only | Arrows, Home and Page Up / Down are transport keys and must never be taken by a focused tab; the panel has no shortcut, so a click is the only way to hide or switch it |
| 19 | Flyouts | Sections flyout: closes on Esc or a jump | The live panel's flyout stays open until you hide it, hides itself only while you type in the script, never takes focus | It is a monitor kept open through a show; Esc must keep meaning pause |
| 20 | Margin editor at Compact | This file (first version): "margin editor in Settings" below Wide | The Output layout tab, in every tier (a compact form without the diagram in the small body) | The tab replaces the fixed block, so it exists wherever the panel does; Settings › Output keeps the full editor |

## Open questions for the user

1. Default **lines visible**: 8 (about 5 words per line on 1080p). Fewer, larger lines suit a far stage-edge
   screen; more suit a close confidence monitor.
2. **Editing while LIVE**: allowed with the green edge and caption (proposed), or locked until paused?
3. **Section names on the output**: off by default (proposed), or shown small and grey?
4. **App icon**: the proposal above, or something else?
5. **Hiding the live panel**: the space it frees stays empty (proposed), so nothing moves mid-show. The alternative
   lets the preview grow, which only helps outputs taller than 16:9 and moves the transport down.
6. **ShuttlePRO v2 layout**: the drawing places its 15 buttons as rows of 4 and 5, two either side of the ring and
   two thumb buttons, numbered left to right, top to bottom. Please check against the venue's unit (or a photo), and
   whether the venue has a ShuttleXpress, a ShuttlePRO v2, or both. (The venue's jog controller is a ShuttlePRO v2, so
   that drawing is the primary one; the layout still needs checking against the unit.)
7. ~~F9 for the live panel~~: dropped. The user wants a mouse-clickable icon tab, no keyboard shortcut.
8. **Default tab**: Controller (proposed) or Output layout on first run?

## Decisions Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-10-19 | Initial design system created | Adapted from `venue-monitoring/DESIGN.md` (palette, faces, state rules, voice) and `SpyderX Colour Corrector/DESIGN.md` (desktop controls, focus, wording rules); deviations listed above |
| 2026-10-19 | Grafana palette and blue `action` primary; `live` green only for LIVE and scrolling | User pointed at the Grafana design guidelines; green stays rare, as on the walls |
| 2026-10-19 | Poppins and DM Mono bundled; Poppins Medium as the default script face | House typeface; Medium for light-on-black legibility at distance |
| 2026-10-19 | Output sized by lines visible, script sizes relative (80 / 100 / 125 / 150 %) | Independent of display resolution and Windows scaling |
| 2026-10-19 | Preview lays out at the output's size and scales | Exact line breaks and margins in the preview |
| 2026-10-19 | Output carries no chrome; problems only in the control window | The presenter sees only the words |
| 2026-10-19 | Two key modes (typing / transport), F5, F8, F11, Esc global | Space and arrows can drive the transport without stealing them from the editor |
| 2026-10-19 | D7 becomes the live panel: tabs Controller (default) and Output layout, hideable by a clickable chevron (no shortcut), body inline large / inline small / flyout by the column's height | User request: margin editor as a tab that can be hidden, and a live controller mock-up in its place. Controller is the show-time view; layout is set-up |
| 2026-10-19 | Controller drawing: flat line drawings with ControlId-named parts, lit on press with a 300 ms hold, what-it-did rows, updated only on the 10 Hz poll | Feedback you can trust mid-show, without per-frame UI work while LIVE (ARCHITECTURE §3 rule 10); SVG drafts in `docs/design/` |
| 2026-10-19 | Controller defaults table corrected to `DefaultBindings` (jog-shuttle buttons) | The table had drifted from the code |
| 2026-10-19 | No F9 or Ctrl+F9; the live panel is mouse-only (clickable tabs and a clickable hide chevron). Hidden panel frees nothing. The ShuttlePRO v2 is the primary jog-shuttle drawing | User decisions on the live panel: "a mouse clickable icon tab"; the venue's unit is a ShuttlePRO v2 |
