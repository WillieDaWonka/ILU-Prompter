# ILU Prompter — Technical Research

Date: 19 Oct 2026. Scope: framework, prompter UX conventions, Windows input, rendering, storage, risks.
Status markers: **[verified]** = confirmed from a cited primary source or source code; **[inferred]** = engineering judgement, validate in the Phase 0 spike.

---

## 0. Headline findings

1. **Use WPF on .NET 10 (LTS), not .NET 8.** .NET 8 reaches end of support on **10 Nov 2026**, three weeks from now. .NET 10 is LTS until Nov 2028 [verified: https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/]. The .NET 10 SDK can be installed per user without admin (`dotnet-install.ps1 -InstallDir %LOCALAPPDATA%\dotnet`). A self-contained publish bundles the runtime, so the target machine needs nothing installed.
2. **WPF is the best fit** because it ships with a mature rich-text editor (RichTextBox/FlowDocument, with RTF and XAML load/save built in), and its multi-window, DPI and interop support are proven. Its weak spot is that animation runs on the UI thread. Mitigate that by **running the output window on its own UI thread (Dispatcher)** and driving the scroll from a time-integrated `CompositionTarget.Rendering` loop.
3. **Established prompters all share one speed model.** QPrompt and Imaginary Teleprompter (both open source) compute `velocity = base × |step|^curvature × sign(step)` [verified from source]. Autocue and Autoscript expose the same ideas as gain, sensitivity, deadband and max speed. Hardware convention: **shuttle = velocity, jog = position.**
4. **Input without focus:** Raw Input with `RIDEV_INPUTSINK` covers the mouse wheel, a dedicated second mouse (told apart by `hDevice`) and HID jog devices. **XInput** works for an Xbox pad. **Windows.Gaming.Input withholds input from unfocused apps**, so avoid it.
5. **Contour Shuttle devices are trivially decodable.** They send a 5-byte report with no driver needed [verified from LinuxCNC source]. Stream Deck+ is best integrated **through Bitfocus Companion**, which already runs at the venue, over OSC.

---

## 1. Framework choice

### 1.1 Comparison

| Criterion | WPF (.NET 10) | WinUI 3 / WinAppSDK | Avalonia 11 | Electron / Tauri | Native D2D/DirectWrite (Vortice) |
|---|---|---|---|---|---|
| Rich-text editing | **RichTextBox + FlowDocument; RTF, XAML and XamlPackage built in** | RichEditBox (RichEdit/ITextDocument, RTF load/save) — capable, less flexible for custom inlines | Official RichTextEditor is **paid (Avalonia Pro)**; free community control only does bold/italic/underline/size on save [https://docs.avaloniaui.net/controls/input/text-input/richtexteditor, https://www.nuget.org/packages/Simplecto.Avalonia.RichTextBox] | Best-in-class (ProseMirror/TipTap/Quill) | None — you would write an editor |
| Smooth scroll engine | `CompositionTarget.Rendering` once per frame on the UI thread; animation is **not** compositor-independent, so hitches on that thread cause stutter | Composition animations run **independently of the UI thread** (strongest native option) | Composition renderer on a render thread, Skia | Chromium compositor; CSS `transform` transitions are GPU-independent (Imaginary Teleprompter uses exactly this) | Full control: DXGI flip model with a waitable swap chain [https://learn.microsoft.com/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model] |
| Preview of output | VisualBrush (same thread only) or a second render of the same model | Second render (no VisualBrush) | Second render / VisualBrush | Second DOM render | Second draw call at scale |
| Multi-monitor / per-monitor DPI | PerMonitorV2 via manifest; mature | AppWindow + DisplayArea; good | Good | Good | Manual |
| Raw Input / HID / XInput | Easy via `HwndSource.AddHook` + P/Invoke | Easy (Win32 interop) | Easy (Win32 interop) | Needs native Node addons (toolchain); WebHID available [https://electronjs.org/docs/latest/tutorial/devices] | Easy |
| Packaging | Self-contained folder or single-file exe | Self-contained unpackaged supported; single-file since WinAppSDK 1.5; larger output [https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps] | Self-contained | Electron ~150 MB+; Tauri needs a Rust toolchain (not installed) and WebView2 | Small |
| Maturity / risk | Very mature, open source, maintained | Still evolving; tooling quirks | Mature, but editor gap | Mature; heavy; less "native" input | Highest effort |

### 1.2 WPF render-loop details and pitfalls

- **`CompositionTarget.Rendering`** fires once per composed frame on the subscribing UI thread. Cast `EventArgs` to `RenderingEventArgs` and use **`RenderingTime`**, the estimated time the next frame reaches the screen. The handler can fire more than once per frame, so **ignore duplicate `RenderingTime` values** [https://source.dot.net/PresentationCore/System/Windows/Media/RenderingEventArgs.cs.html, Petzold: https://ftp.charlespetzold.com/blog/2010/06/CompositionTarget-Rendering-and-RenderEventArgs.html].
- **Integrate position as `pos += v × Δt`** using the `RenderingTime` delta. Never step a fixed amount per tick, because tick intervals are irregular.
- **Don't use `DispatcherTimer` or `System.Timers`** to drive animation. Their ~15.6 ms granularity guarantees judder.
- **Multiple UI threads are supported.** Each thread has its own Dispatcher, and all of them share one render thread per process [https://learn.microsoft.com/dotnet/desktop/wpf/advanced/threading-model, https://learn.microsoft.com/troubleshoot/developer/dotnet/framework/general/wpf-render-thread-failures]. Put the output window on its own STA thread so editing and layout in the control window can't stall the output. Consequence: **VisualBrush cannot cross threads**, so the preview must be a second render of the same document snapshot (Section 4.4). This is also the cleaner design.
- **Moving text:** set `TextOptions.TextHintingMode="Animated"` on the scrolling container. ClearType falls back to grayscale whenever text goes through an intermediate surface (VisualBrush, effects, opacity masks, layered windows) [https://learn.microsoft.com/archive/blogs/text/cleartypehint]. On moving text, grayscale is the right choice anyway, because ClearType colour fringes shimmer at sub-pixel offsets [inferred].
- **`UseLayoutRounding` and `SnapsToDevicePixels` affect layout, not `RenderTransform`.** A fractional `TranslateTransform.Y` renders at sub-pixel positions. That is what you want for smooth slow scroll (Section 4.1).
- **`BitmapCache`:** a transform on a cached element's parent doesn't invalidate the cache. However, the software fallback limits caches to 2048×2048, and the hardware limit isn't documented [https://learn.microsoft.com/dotnet/api/system.windows.media.bitmapcache]. A 30,000 px-tall script is too big to cache whole. Only use caching per tile (Section 4.2), and only if the benchmark needs it.
- **Mixed refresh rates:** in Win10 2004+, DWM composes at the highest monitor rate. Running 60 Hz and 120 Hz panels together can still stutter on one of them [https://blurbusters.com/?p=23058]. It's not documented whether WPF's frame clock follows the primary display. **Test this in the spike** (log Rendering intervals on the output display). If it misbehaves, match refresh rates or make the prompter display primary [inferred].

### 1.3 Recommendation

**WPF on .NET 10, self-contained folder publish.** Behind an `IOutputRenderer` interface, keep an **escape hatch**: if the Phase 0 benchmark shows unacceptable frame pacing, swap the output window's content for a Direct2D/DirectWrite flip-model swap chain hosted in an `HwndHost`, using Vortice.Direct2D1 (MIT). The editor, input and model stay unchanged. WinUI 3 is the runner-up (better compositor animation, weaker editor, more deployment friction). Avalonia is out because of the paid editor. Electron is out because native input needs addons and the footprint is heavy.

---

## 2. How established prompter software works

### 2.1 Speed models (verified from source)

- **QPrompt** (GPL-3.0, Qt/QML; https://github.com/Cuperino/QPrompt-Teleprompter):
  - Velocity is an integer step `__i`, and speed is `__baseSpeed * Math.pow(Math.abs(__i), __curvature)` with sign = direction (`src/prompter/Prompter.qml`). The default step is 3. Both base speed and curvature are user sliders.
  - Speed is **scaled by font size and viewport width**, so reading pace stays constant when text size changes.
  - Motion is a `NumberAnimation` on `contentY` to the end of the document, with duration = remaining distance ÷ speed. The engine interpolates, and each velocity change re-targets the animation.
  - Keys: Down/Up = faster/slower (crossing zero reverses), Space = pause, Ctrl+Space = stop, I = reverse, R/F = rewind/fast-forward (hold), PageUp/PageDown = skip, Ctrl+PageUp/PageDown = previous/next marker, 1–0 = speed presets, and named markers can have their own hotkeys.
  - Mouse wheel is either **direct scroll** or **"scroll as velocity dial"**, with optional throttling (factor 8) for touchpads (`WheelSettingsOverlay.qml`).
  - The **reading region** (overlay band) can be placed Top/Middle/Bottom/Free/Fixed. Jumps place the target line *in the reading region*, not at the top of the screen (`goTo`, `goToNextMarker`).
  - Also: an end-of-script action (Stop/Exit/Loop), edit while prompting (double-click), a timer with time-to-end, Contour Shuttle and presenter-clicker support via key mapping, and import of DOCX/ODT/RTF/MD [https://qprompt.app/, https://docs.cuperino.com/s/T2N_NMluU].
- **Imaginary Teleprompter** (GPL-3.0, web/Electron; https://github.com/ImaginarySense/Teleprompter-Core): uses the same formula, `velocity = speedMultip*Math.pow(Math.abs(x),sensitivity)*sign`, and animates with CSS `transform` transitions (`transition: transform <ms> linear`) re-targeted on change (`js/teleprompter.js`). It has a focus area, flip H/V, and rich text.
- **Autocue Explorer:** a "virtual scroll controller" (the mouse wheel) with **wheel gain, reverse, sensitivity (wheel travel to reach max speed), deadband, and max speed**. ShuttleXpress buttons are remappable. It has a cue marker (shape, colour, offset), margins, line spacing, and a clock [https://www.autocue.com/education/tutorials/how-to-customise-settings-in-autocue-explorer/].
- **Autoscript WinPlus-IP:** a joystick hand controller sets speed and direction, with adjustable sensitivity profiles. Its programmable buttons map to **previous marker / next marker / top of run order**. The pedal variant has a single-tap reverse [https://www.adorama.com/asa90090005.html].
- **Cinegy Prompter:** speed presets in **words per minute** on keys 1–10, and analog stick axis selection with an invert option [https://open.cinegy.com/products/air/26.2/prompter/user-manual/installation-and-configuration/speed/].
- **Elgato Prompter:** a Stream Deck "Prompter Scroll" action changes speed by key or **dial turn** through the Camera Hub plugin [https://www.elgato.com/explorer/products/teleprompter/prompter-pro-teleprompting-made-easy/].
- **PromptSmart:** voice-tracked scrolling (VoiceTrack) instead of fixed speed. Out of scope for an operator-driven venue prompter.

### 2.2 Operator expectations: feature checklist

- Continuous scroll with a **power-curve velocity** (fine control at low speed, fast at the top), **reverse through zero**, a pause that remembers speed, and a stop that zeroes it.
- **Deadband** at rest for analog controls; configurable **sensitivity** and **max speed**; an invert option per controller.
- **Shuttle = velocity** (spring-return ring), **jog = position** (detented wheel, e.g. 1 detent = 1 line or N px).
- **Cue / reading-line marker** (arrow or band) at an adjustable height, typically about 1/3 down. All jumps align the target to the reading line.
- **Markers / sections:** previous/next marker, jump to top, an optional named marker list with hotkeys, and a "top of run order" button.
- **Fast forward / rewind** (hold), and speed presets on number keys.
- Font sizing expressed as **"lines visible"** or as a % of screen height; line spacing; L/R/T/B margins.
- **Edit while prompting** without losing position; a live progress bar and time-to-end readout.
- Flip H/V (not needed now, but cheap to keep).
- Black background, high-contrast text, hidden cursor on output, nothing else ever shown on the output display.

### 2.3 "Line scrolling" mode (design)

None of the studied apps has a true line-step mode. QPrompt's PageUp/PageDown "skip" is closest. Proposed design:
- Build a **line table** (y of each visual line in output DIPs) from layout (Section 4.3).
- Step = animate to `lineTop[k] − readingLineY` with a short ease-out (120–200 ms). A one-step overshoot or tick count is ignored while the animation runs (queue at most one).
- Optional **auto-step**: advance one line every `60 / linesPerMinute` seconds, with the speed control adjusting the rate.
- In display-scroll mode, jog = ±1 line per detent. In line mode, the shuttle ring sets the auto-step rate.

---

## 3. Input handling on Windows (.NET)

### 3.1 Mouse wheel

- `WM_MOUSEWHEEL` delta is a multiple of `WHEEL_DELTA` (120) on notched wheels. High-resolution or free-spinning wheels send **smaller deltas**, which must be **accumulated**, not rounded away [https://learn.microsoft.com/windows/win32/inputdev/wm-mousewheel]. Treat delta as continuous: `velocityDelta = delta / 120 × gain` (dial mode) or `pixels = delta / 120 × pxPerNotch` (direct mode).
- **Unfocused capture plus a dedicated second mouse:** `RegisterRawInputDevices` (usage page 0x01, usage 0x02) with **`RIDEV_INPUTSINK`** delivers `WM_INPUT` even when the app is in the background [https://learn.microsoft.com/windows/win32/inputdev/about-raw-input].
  - Wheel data: `RAWMOUSE.usButtonFlags & RI_MOUSE_WHEEL`, delta = `(short)usButtonData` [https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-rawmouse].
  - `RAWINPUTHEADER.hDevice` identifies the physical device. Map it to a stable name via `GetRawInputDeviceInfo(RIDI_DEVICENAME)` (the device path contains VID/PID) and let the user pick "this mouse is the prompter wheel".
  - The prompter mouse's pointer still moves the system cursor. Raw Input can't block that, so suggest a wheel-only device or a trackball with the ball unused [inferred].
- Package: **RawInput.Sharp** (zlib, net8.0+, exposes `Header.DeviceHandle`, HID data) [https://www.nuget.org/packages/RawInput.Sharp], or plain P/Invoke generated by **Microsoft.Windows.CsWin32** (MIT, official). Raw Input needs no admin rights or drivers.

### 3.2 Xbox controller

| API | Background input | Install | Notes |
|---|---|---|---|
| **XInput 1.4** (`xinput1_4.dll`, in-box) | Works for desktop apps in practice (Steam relies on it). The desktop docs carry an ambiguous focus note under `XInputEnable` [https://learn.microsoft.com/windows/win32/api/xinput/nf-xinput-xinputenable] — **verify in spike** | None | Polling; max 4 pads; triggers separate |
| Windows.Gaming.Input | **No: blocked when the window isn't focused** [Unity tracker: https://issuetracker.unity3d.com/issues/controller-input-is-ignored-when-editor-is-not-focused] | None | Avoid |
| GameInput | Off by default; `GameInputEnableBackgroundInput` focus policy [https://learn.microsoft.com/gaming/gdk/docs/reference/input/gameinput-v2/enums/gameinputfocuspolicy-v2] | Newer versions need a redistributable (admin) | Overkill |
| SDL2/SDL3 (zlib) | Has a background-events hint | Ships native DLL | Extra native dependency |
| Raw Input HID (gamepad usage 0x05) | Yes (INPUTSINK) | None | Xbox HID report merges triggers; fallback only |

- Package: **Vortice.XInput 3.6.2** targets net8.0 (MIT) [https://www.nuget.org/packages/Vortice.XInput/3.6.2]. 3.8.x targets net9/net10, which is fine on .NET 10. **SharpDX is unmaintained** (archived 2019), so don't use it. Alternatively, ~30 lines of P/Invoke to `XInputGetState`.
- Poll on the output thread's Rendering tick (60–120 Hz is enough) or on a 250 Hz background thread.
- **Deadzone and curve:** use a radial deadzone (XInput's suggested left-stick deadzone is 7849/32767 ≈ 0.24; let the user tune it), rescale, then apply `v = vMax × sign(y) × ((|y| − dz) / (1 − dz))^γ` with γ ≈ 2–3. This is the analog equivalent of QPrompt's curvature.
- Suggested mapping: left stick Y = velocity, right stick Y = fine position nudge, D-pad up/down = previous/next line, LB/RB = previous/next cue, A = play/pause, B = stop, Y = reverse, X = mode toggle, View = top, LT/RT = proportional rewind/fast-forward.
- **Disconnect** (`ERROR_DEVICE_NOT_CONNECTED`): ramp velocity to 0 over about 300 ms (fail-safe) and show a banner. Never hold the last speed.

### 3.3 Jog/shuttle and other controllers

| Device | Protocol | Integration notes |
|---|---|---|
| **Contour ShuttleXpress** (0b33:0020, 5 buttons) / **ShuttlePRO v2** (0b33:0030, 15 buttons) / ShuttlePRO v1 (05f3:0240) | Plain USB HID, **5-byte report**: byte0 = shuttle ring **int8 −7..+7**; byte1 = **8-bit wrapping jog counter** (diff with wrap at ±128); byte2 unused; bytes 3–4 = 16-bit little-endian button mask. Jog is 10 counts/rev [verified: LinuxCNC `src/hal/user_comps/shuttle.c`, https://linuxcnc.org/docs/html/drivers/shuttle.html]. On Windows, `ReadFile` prepends a report-ID byte (0x00), giving 6 bytes [inferred, standard Windows HID behaviour] | Read with **HidSharp** (Apache-2.0; v2.6.4 Oct 2025) [https://www.zer7.com/software/hidsharp] or Raw Input HID. No Contour driver needed. Contour's own software, if installed, may remap or grab the device. First jog event only establishes the baseline. **Best value; recommended primary controller.** |
| X-keys XK-12 Jog & Shuttle | HID; raw positioning data via free SDK, or emulates keyboard/mouse/game controller; 15 shuttle zones [https://xkeys.com/xk12jsh.html, https://piengineering.com/pages/hid-data-reports] | HidSharp; report layout in PI Engineering SDK on GitHub |
| Elgato Stream Deck+ | 4 dials; plugin `dialRotate` event with signed `ticks` (Node.js SDK) [https://docs.elgato.com/sdk/plugins/events-sent] | **Easiest: via Companion** (Section 3.5): dial rotate-left/right actions → OSC to the app. A custom Stream Deck plugin is a later option |
| Loupedeck / Logitech MX Creative | Vendor plugin SDK (C#) | Plugin only; low priority |
| MIDI controllers (e.g. Behringer X-Touch Mini, Korg nanoKONTROL) | MIDI CC; endless encoders send relative CC (two's complement or offset-64) | **Melanchall.DryWetMidi** (MIT, per mirror — confirm) or **NAudio** (MIT, WinMM). Avoid Windows MIDI Services (`Windows.Devices.Midi2`) for now: GA only on Win11 24H2/25H2, and its SDK runtime is a separate install [https://midi.org/midi-2-0-coming-to-windows-11] |
| Commercial prompter hand controllers (Autoscript, CueScript, Autocue, Telescript, Fortinge) | Mostly **proprietary** and tied to vendor software. Some are "HID", e.g. Telescript USB HID controllers; Fortinge HCD uses Contour Shuttle software [https://www.bhphotovideo.com/c/buy/teleprompter-controllers/ci/23719]. Protocols aren't published | Don't target. Support "keyboard-emulating" and "HID gamepad" generically, and the Contour family explicitly |
| Glide Gear / ikan / generic Bluetooth prompter remotes, presenter clickers | **Keyboard emulation** (PageUp/PageDown, arrows, Space, B, F5) | Keymap layer (Section 3.4) |

### 3.4 Keyboard shortcuts and clickers

- Default: handle keys when either app window is focused (WPF `PreviewKeyDown` at window level, after the editor has had its chance in edit mode). Configurable keymap, QPrompt-style defaults (Section 2.1).
- Global without focus:
  - `RegisterHotKey` is safe and official, but it **steals the key system-wide**. Only use it for unusual chords (e.g. Ctrl+Alt+F9).
  - **Don't** use `SetWindowsHookEx(WH_KEYBOARD_LL)`, and don't use a raw keyboard `RIDEV_INPUTSINK` on all keyboards. Both are keylogger patterns that **Defender for Endpoint may flag** on this corporate machine.
  - If a background clicker is needed, use Raw Input INPUTSINK on keyboards **filtered to the clicker's `hDevice` only**, and clear it with IT first. The keys still reach the foreground app.

### 3.5 Bitfocus Companion and network control (later phase)

- Companion already runs at the venue. It supports Stream Deck+ encoders through **"Enable Rotary Actions"** per control (rotate-left/right action groups) [https://companion.free/user-guide/surfaces/elgato_streamdeck]. It sends with the Generic OSC, TCP/UDP and HTTP modules.
- Expose a small control API: **OSC over UDP** (e.g. `/ilu/speed f`, `/ilu/speed/nudge f`, `/ilu/play`, `/ilu/pause`, `/ilu/line/next`, `/ilu/cue/next`, `/ilu/cue/goto s`, `/ilu/top`) plus **HTTP** (Kestrel minimal API, no URL ACL needed) for the same verbs. Optionally add OSC feedback (current cue, speed) for button labels, or a later custom Companion module.
- Listening on a LAN port triggers a **Windows Firewall inbound-rule prompt (admin)**. Default to `127.0.0.1` binding and ask IT for a rule if Companion runs on another host.

---

## 4. Rendering details

### 4.1 Crisp vs smooth

- **Integer pixel snapping** gives the sharpest text but judders whenever speed isn't an integer number of px per frame. At 90 px/s and 60 Hz you get 1.5 px/frame, i.e. a 1,2,1,2 cadence, which is visibly uneven.
- **Sub-pixel translation** gives even motion. With grayscale AA and large prompter fonts (60–200 px), the softening is invisible at reading distance [inferred].
- **Default:** sub-pixel, with `TextHintingMode=Animated` and `TextRenderingMode=Grayscale` while moving. When stopped, snap to the nearest device pixel (and optionally re-enable Fixed hinting), so static text is razor-sharp.
- Use **`TextFormattingMode="Ideal"`** (resolution-independent metrics) on both output and preview, so line breaks are identical at any scale. `Display` mode snaps glyph advances to pixels and can re-wrap the preview differently [inferred from WPF text model; verify].

### 4.2 Large-font performance

- Live layout of a FlowDocument is done once per content change, not per frame. Per frame only the transform changes, and WPF re-renders the visible glyph runs on the render thread. Expect this to be fine at 1080p/4K, but it's **unknown for 150–250 px fonts** (WPF's large-glyph rasterisation path isn't documented). **Benchmark in Phase 0.**
- Fallback ladder:
  1. Hide off-screen blocks (virtualise by paragraph).
  2. Rasterise into **tiles** (e.g. 2048 px tall `RenderTargetBitmap` strips at output device resolution) and scroll the bitmaps. This is perfectly smooth, but re-tiling is needed after edits.
  3. The D2D/DirectWrite renderer (Section 1.3).
- Allocation-free render loop: no LINQ, closures or boxing per frame. Pre-allocate the transform and mutate `.Y`.

### 4.3 Line, paragraph and section positions

- After layout, enumerate visual lines: `p = doc.ContentStart.GetLineStartPosition(0)`, then loop `p = p.GetLineStartPosition(1, out moved)` until null or moved == 0. Record `p.GetCharacterRect(LogicalDirection.Forward).Top` [https://learn.microsoft.com/dotnet/api/system.windows.documents.textpointer.getlinestartposition].
  - This requires a viewer with valid layout (a read-only RichTextBox or FlowDocumentScrollViewer at the output width).
  - Cost is O(lines). Rebuild after each re-layout, off the per-frame path.
- Paragraphs: iterate `doc.Blocks` and take `block.ContentStart.GetCharacterRect(...)`. Cues: same, filtered to cue paragraphs.
- Store the tables as sorted `double[]` and binary-search for "current line at reading line" to drive previous/next.
- To keep position stable across edits, anchor to a **character offset** at the reading line (`TextPointer.GetOffsetToPosition` from ContentStart), rebuild, then re-resolve and set position.

### 4.4 Output and preview sync

- **One document snapshot, two renders.** On edit (debounced ~250 ms), serialise the editor FlowDocument (`TextRange.Save`, `DataFormats.Xaml`). Each of the output thread and the preview loads its own FlowDocument instance; they must be separate because FlowDocument is thread-affine.
- Both lay out at the **same logical width**: `outputWidthDIP − marginL − marginR`, where `outputWidthDIP = outputPixels / outputDpiScale`. The preview applies a `ScaleTransform(s)` with `s = previewWidth / outputWidthDIP` as a **RenderTransform**, not a LayoutTransform, so layout is identical.
- The scroll engine owns `position` (in output DIPs). The output thread runs the integrator. The preview reads a lock-free published `position` (a `volatile double` or `Interlocked`) on its own Rendering tick and applies `position × s`.
- Margins and the reading line are drawn in the preview as overlays at the same scale. Letterbox the preview to the output's aspect ratio.

### 4.5 Margins and font units

- Store margins as **fractions of output width/height** (resolution-independent, survives a monitor swap). Show and edit them in px of the current output, with a % readout. A "link" toggle edits all four together (or L=R and T=B).
- Font size: store as **lines visible** (or % of output height), then derive `fontPx = usableHeight / (linesVisible × lineSpacing)`. Per-run size formatting in the script is relative (e.g. 1.5×) to this base.
- Speed: store in **lines per minute** (or WPM-like). Internally `px/s = lpm/60 × lineHeightPx`, so changing font size keeps reading pace, as QPrompt does.

---

## 5. Script storage format

| Format | Pros | Cons |
|---|---|---|
| RTF | Universal; WPF loads/saves natively; pastes from Word | No semantic markers; fidelity loss on round-trip through FlowDocument [https://developer.mescius.com/forums/ar-dev/microsoft-rtf-to-activeviewer-rtf] |
| FlowDocument XAML (`TextRange.Save` Xaml/XamlPackage) | Lossless for everything the editor can do | WPF-specific. **Never `XamlReader.Load` an untrusted file** (arbitrary type instantiation); only load via `TextRange.Load` [inferred; verify] |
| Markdown | Human-readable, diff-able | Can't express colour, size or highlight without HTML |
| Custom JSON | Versionable, holds settings and metadata | Must embed rich text somehow |

**Recommendation:** native file **`.ilup` = UTF-8 JSON** `{ formatVersion, title, documentXaml (TextRange Xaml string), perScriptOverrides{...} }`. Import .txt, .rtf (`TextRange.Load` Rtf) and .docx via **DocumentFormat.OpenXml** (MIT, official): map paragraphs and runs for b/i/u/strike/highlight/colour/size, and map Heading styles to cues. Pasting from Word works for free through RTF. Optionally import .md via **Markdig** (BSD-2). Export .rtf/.txt.

**Section/cue markers:** use an **in-text convention**. A paragraph starting with `##` (configurable) is a cue, and the rest of the line is its name. The editor highlights cue paragraphs, and an "Insert cue" button adds one. The output builder strips the `##` and renders a styled cue banner. This survives RTF, DOCX, TXT and copy-paste, unlike custom inline elements or `Tag` properties, which RichTextBox editing and RTF drop.

---

## 6. Risks and pitfalls

| Risk | Mitigation |
|---|---|
| **.NET 8 EOL 10 Nov 2026** | Target net10.0-windows from day one |
| Per-monitor DPI; window lands on the wrong monitor or at the wrong size | PerMonitorV2 manifest. Place the output with `SetWindowPos` in **physical pixels** from `EnumDisplayMonitors`/`GetMonitorInfo`, then borderless (`WindowStyle=None`, `ResizeMode=NoResize`, Topmost). Handle `WM_DPICHANGED` |
| Monitor identity changes (ordering, hotplug) | Persist the monitor by stable ID (`QueryDisplayConfig` target device path / EDID name), not index. On `SystemEvents.DisplaySettingsChanged`, re-resolve. If the display is missing, **never** go fullscreen over the operator screen: show a windowed output plus a banner |
| Output window steals focus or shows the cursor | `WS_EX_NOACTIVATE`, `ShowActivated=false`, cursor hidden over the output, no taskbar entry |
| Notifications or toasts on the prompter display | Turn on Do Not Disturb / Focus during shows (document in the runbook) |
| Display sleep / screensaver | While prompting, `SetThreadExecutionState(ES_CONTINUOUS \| ES_DISPLAY_REQUIRED \| ES_SYSTEM_REQUIRED)`, cleared on stop. It **doesn't** suppress a GPO-enforced screensaver or lock, which is an IT policy question on a managed laptop |
| Controller disconnect mid-show | Fail-safe ramp to stop, banner, auto-reconnect (poll or `WM_DEVICECHANGE`). Keyboard and mouse always remain as backup |
| GC pauses | Zero per-frame allocation. `GCSettings.LatencyMode = SustainedLowLatency` while prompting. Snapshot rebuilds on a background thread where possible |
| Timer resolution | Use Rendering and `RenderingTime` only. Never use `DispatcherTimer`, `Thread.Sleep` or `timeBeginPeriod` for motion |
| Mixed refresh rates (60 Hz + 120 Hz) | Measure. Prefer matched rates, or make the prompter display primary if WPF pacing follows primary |
| UI-thread stalls from editing | Separate output Dispatcher thread (Section 1.2) |
| Large-font render cost | Phase 0 benchmark; tile or D2D fallback |
| Corporate controls (AppLocker/WDAC/SmartScreen) on an unsigned exe | Prefer a **self-contained folder** over single-file self-extract (extracting native DLLs to %TEMP% is more likely to be blocked). Code-sign if possible |
| EDR (Defender/Entra) flags on input capture | Avoid keyboard hooks and global keyboard INPUTSINK. Use mouse/HID-only Raw Input. **Ask before testing any keyboard capture** |
| Firewall prompt for the OSC/HTTP listener | Localhost by default; request an IT rule for LAN |
| RTF/DOCX fidelity | Test highlight/strike/colour round-trips early; the native `.ilup` is lossless |

---

## 7. Recommendation

### 7.1 Stack

- **WPF, .NET 10 (LTS), `net10.0-windows`**, x64, `SelfContained=true`, folder publish (single-file optional after the corporate-policy test). PerMonitorV2 manifest.

### 7.2 Packages

| Package | Use | Licence |
|---|---|---|
| Microsoft.Windows.CsWin32 | P/Invoke source generation (Raw Input, SetWindowPos, monitors, SetThreadExecutionState) | MIT |
| RawInput.Sharp (optional, instead of hand P/Invoke) | Raw Input mouse/HID with device handles | zlib |
| Vortice.XInput (3.6.2 for net8 / 3.8.x for net9+) | Xbox controller | MIT |
| HidSharp | Contour Shuttle, X-keys | Apache-2.0 (confirm LICENSE.txt) |
| Melanchall.DryWetMidi or NAudio (Phase 3) | MIDI controllers | MIT |
| DocumentFormat.OpenXml | .docx import | MIT |
| Markdig (optional) | .md import | BSD-2-Clause |
| CommunityToolkit.Mvvm | MVVM plumbing | MIT |
| Rug.Osc or CoreOSC, plus ASP.NET Core minimal API (Phase 3) | Companion control | MIT (verify the chosen OSC library) |
| Vortice.Direct2D1 / DXGI (only if fallback is needed) | D2D/DirectWrite output renderer | MIT |

### 7.3 Architecture

```
ILU.Prompter.Core        (no WPF dependency where possible)
  ScriptModel            snapshot = Xaml string + cue list; serialisation (.ilup JSON), importers (rtf/docx/txt/md)
  ScrollEngine           state machine {Stopped, Playing, Paused, LineStep, Seeking}; velocity model
                         v = base·|s|^γ·sign(s) for stepped inputs; analog curve for axes;
                         integrates pos += v·Δt; line/paragraph/cue tables; anchors; publishes position atomically
  Commands               one verb set: Play, Pause, Stop, Reverse, SetVelocity, NudgeVelocity, NudgePosition,
                         LineNext/Prev, ParaNext/Prev, CueNext/Prev, CueGoto(name), Top, FF/RW(hold), ToggleMode
  Settings               JSON in %APPDATA%\ILU Prompter (margins as fractions, linesVisible, speed lpm,
                         reading-line %, colours, monitor ID, controller bindings, keymap)
ILU.Prompter.Input       IInputProvider -> Commands/axis values
  RawMouseProvider       wheel (dial | direct), device-filtered, INPUTSINK
  XInputProvider         poll, deadzone+curve, disconnect fail-safe
  ShuttleProvider        HidSharp, Contour 5-byte report; X-keys later
  KeyboardProvider       focused-window keymap; clicker presets
  MidiProvider, OscHttpProvider (later)
ILU.Prompter.App (WPF)
  ControlWindow (UI thread A)  RichTextBox editor + format toolbar; cue list; transport; speed;
                               live preview (2nd render, ScaleTransform) with margins + reading line overlay;
                               settings dialogs (controller picker, margins with link toggle, display picker)
  OutputWindow  (UI thread B)  borderless, topmost, no-activate, on chosen monitor; FlowDocument at output width;
                               TranslateTransform driven by CompositionTarget.Rendering; reading-line marker
  IOutputRenderer              WpfFlowRenderer (default) | D2DRenderer (fallback)
```

Data flow: editor change → debounce → snapshot → (output thread, preview) rebuild layout → line/cue tables → engine re-anchors. Input providers → Commands → engine → position → both renders.

### 7.4 Phased MVP

- **Phase 0, spike (1–2 days):** WPF output window on a second UI thread on the secondary monitor. Scroll a 20-page script at 100/200 px fonts. Log Rendering intervals and dropped frames at 60 and 120 Hz, with sub-pixel vs snapped motion. Run an XInput background-input check. Test a self-contained folder exe under corporate policy. **Go/no-go for the WPF renderer.**
- **Phase 1, core prompter:** control and output windows, display picker with hotplug safety, RichTextBox editing (b/i/u/strike/highlight/colour/size), .ilup save/load and .rtf/.txt import, margins (independent plus link) and lines-visible font sizing, live scaled preview with margins, display scrolling with speed slider and keyboard, reading-line marker, jump to top, `##` cues with previous/next cue, previous/next line and paragraph, display-sleep suppression.
- **Phase 2, controllers:** mouse wheel (dial/direct, dedicated device via Raw Input), Xbox controller (curve/deadzone UI), Contour ShuttleXpress/PRO, line-scrolling mode (manual and auto-step), edit while prompting with anchor preservation, controller binding UI, .docx import.
- **Phase 3, venue integration:** OSC/HTTP API for Companion (Stream Deck+ dials via rotary actions), feedback, MIDI encoders, X-keys, timer and time-to-end, progress bar, optional flip, and a D2D renderer only if Phase 0 demanded it.
