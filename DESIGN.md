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
- The control window never moves; only the script moves, on the output and in its preview.

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
| Wide | 1600 and over | 1920 × 1080 at 100 % | 240 px panel | 720 px, with the margin editor | icon + word |
| Compact | 1200 to 1599 | 1366 × 768 at 100 %, 1920 × 1080 at 125 % | 44 px rail, list as a flyout | 480 px, margin editor in Settings | icon only, tooltips |
| Narrow | 1024 to 1199 | 1366 × 768 at 125 % (1093 × 614 DIP) | rail | 440 px, chips move to the app bar, readouts into the status bar | icon only |

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
| |-------------| | |                                               | | |D7 OUTPUT LAYOUT               | |
| |[Add section]| | |                                               | | | Lines 8 [-][+]  Spacing 1.3   | |
| +-------------+ | +-----------------------------------------------+ | | margin editor (see below)     | |
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
| D live column | 720 × 928, `panel` | D1 chips (36), D2 preview (696 × 392 + 24 caption), D3 transport (52 + 18 captions), D4 speed (44), D5 mode and Blank (36), D6 readouts (32), D7 output layout (the rest, about 230) |
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
|  | +-----------------------------------------+ |                                       |
+----------------------------------------------------------------------------------------+
| ⌨ Transport keys | Line 112 of 384 · Section 3 of 7 | Display 2 · Xbox 70 %        28   |
+----------------------------------------------------------------------------------------+
```

What changes from Wide, in this order as the window narrows:
1. **Below 1600:** sections become a 44 px rail (≡ opens the list as a 280 px flyout over the editor, Esc or a
   jump closes it); the live column drops to 480; transport captions hide (icons keep tooltips with the
   shortcut); the speed readout loses "≈ 140 wpm" (it moves to the readout tooltip); the mode switch reads
   "Display" | "Line"; "Blank output" reads "Blank"; D7 output layout moves to Settings › Output. The editor
   toolbar folds Find and zoom into a ⋯ overflow menu.
2. **Below 1200:** the live column drops to 440 (preview 416 × 234); the two chips move into the app bar beside
   the script name; the D6 readouts move into the status bar, except time remaining, which stays as `readout` at
   the right of the speed row.
3. **Never:** the preview, Play, the speed control or the chips never hide or scroll. If the window is smaller
   than 1024 × 600 DIP, Windows stops the resize there.

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

### Margin editor with link toggle (D7, and Settings › Output)

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
- Above the diagram: **Lines visible** `[−] 8 [+]` (4 to 16) and **Line spacing** (1.0 to 2.0, step 0.05), so all
  output layout sits in one place.

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
| Jog-shuttle | shuttle ring (±7 positions) | centre button | jog wheel, one click = one line | buttons 1 / 2 | buttons 3 / 4 | button 5 |

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
mode, Blank, output layout.

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
- **Motion safety.** Nothing flashes or blinks (WCAG 2.3.1); the only moving thing is the script.
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

## Do's and Don'ts

- Do: keep the output to the script, the reading-line marker and END. Report every problem in the control window.
- Do: show the output's state at all times in the output chip, and frame the preview in green while LIVE.
- Do: keep `live` green for the two things live now (the presenter sees it; it is scrolling).
- Do: give every chip a glyph and a capital state word; every icon button a tooltip with its shortcut.
- Do: lay out the preview at the output's real size and scale it, so its line breaks are the output's.
- Do: use DM Mono for every changing number, Poppins for every word.
- Don't: use red for anything but a fault that needs action now; don't use Grafana's `recording` crimson.
- Don't: let any native white control, dialog or flash appear, or set a white editor page.
- Don't: animate the control window, blink a chip, or play a sound.
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

## Open questions for the user

1. Default **lines visible**: 8 (about 5 words per line on 1080p). Fewer, larger lines suit a far stage-edge
   screen; more suit a close confidence monitor.
2. **Editing while LIVE**: allowed with the green edge and caption (proposed), or locked until paused?
3. **Section names on the output**: off by default (proposed), or shown small and grey?
4. **App icon**: the proposal above, or something else?

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
