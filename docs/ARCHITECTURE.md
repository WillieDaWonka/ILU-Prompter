# ILU Prompter: Architecture and build contract

Date: 19 Oct 2026. Status: **binding**. This is the build contract for the scaffold commit and for the five parallel
worktree streams (A to E). Read it with `docs/RESEARCH.md` (technical rationale) and `DESIGN.md` (visual and wording
system, binding for every pixel and word of UI). Where this file and DESIGN.md disagree on UI, DESIGN.md wins; where they
disagree on code structure, threading or contracts, this file wins. Deviations from either are listed in §0.3.

Verification: the scaffold in §5 and every contract in §2 were compiled with the .NET 8.0.301 SDK on this machine
(`dotnet build` 0 warnings, `dotnet test` 8/8 passing). The scaffold app launched, and a self-contained folder publish
(153 MB) was produced. Package versions in §1.4 were confirmed on nuget.org on 19 Oct 2026.

**Reading guide.**
- Scaffold agent: §1, §5, then copy §2 and Appendix A verbatim.
- Stream agents: §0, §1, §2.1, your contracts in §2, §2.15, §3, your stream in §4, and §7.
- Integration agent: §4.F and §6.

---

## 0. Summary

### 0.1 What we are building

A WPF app with two windows. The **control window** holds the script editor, sections, live preview, transport and
settings. The **output window** is borderless and fullscreen on the presenter's display (extended, never mirrored), and
shows the script and nothing else. Scrolling is driven by a time-integrated engine (`pos += v·dt` on
`CompositionTarget.Rendering`). It is controlled by keyboard (in-window only), the mouse wheel over the preview or a
dedicated Raw Input mouse, an Xbox pad (XInput) or a Contour ShuttleXpress/PRO (HID).

### 0.2 Key decisions

| # | Decision | Source |
|---|---|---|
| D1 | WPF; target framework is **one property** (`IluTargetFramework`) in `Directory.Build.props`. Start at `net8.0-windows`, switch to `net10.0-windows` with no code change. `LangVersion` pinned to 12, and no API newer than .NET 8 | RESEARCH §1.3, user constraint |
| D2 | Output window on its **own STA/Dispatcher thread**. The preview is a **second render** of the same immutable snapshot, never a VisualBrush | RESEARCH §1.2, §4.4 |
| D3 | One scroll engine (Core, no WPF) owns position, in output DIPs. Thread-safe: commands are enqueued, ticked by the output thread, and position is published atomically | RESEARCH §4.4 |
| D4 | The speed the operator sees is a **unitless −10…+10 scale** (DESIGN D4). Internally `lpm = 80·(|s|/10)^1.3`, so 4.5 ≈ 28 lpm ≈ 140 wpm, and `px/s = lpm/60 × lineHeight`. Changing font size keeps reading pace | DESIGN Speed control + RESEARCH §4.5 |
| D5 | Margins are stored as fractions of the output display and shown in px. Font size comes from **lines visible**. Script run sizes are relative (80/100/125/150 %) | RESEARCH §4.5, DESIGN Output defaults |
| D6 | Snapshot = flattened FlowDocument XAML + section list + counts. Positions that survive edits are `DocumentAnchor(paragraphIndex, charOffset)` in **canonical paragraph order** | RESEARCH §4.3/§4.4 |
| D7 | Sections use the `##` in-text convention. Native file `.ilup` = UTF-8 JSON. Imports: RTF, TXT, DOCX (OpenXML SDK) | RESEARCH §5 |
| D8 | Providers report **raw normalised input** (axis/relative/button). The Core `InputRouter` applies bindings, deadzones, curves and mode from settings | RESEARCH §3, §2.2 |
| D9 | No keyboard hooks, no global keyboard Raw Input, no `RegisterHotKey`. Keys work only when an app window is focused | user constraint, RESEARCH §3.4 |
| D10 | DI with `Microsoft.Extensions.DependencyInjection`. Each project has one `AddIluXxx()` extension, owned by its stream. MVVM via CommunityToolkit.Mvvm (App only) | — |
| D11 | Tokens live only in `src/IluPrompter.Theme/Themes/IluTokens.xaml`, generated from DESIGN.md front matter. No hex literal anywhere else (test-enforced). Settings hold token names (`"ink-white"`), never hex | DESIGN "WPF resource dictionary" |
| D12 | Fonts and logo stay once at the repo root `assets/` and are **linked** into `IluPrompter.Theme` as WPF `Resource` items by wildcard. Dropping `Poppins-Bold.ttf` / `Poppins-*Italic.ttf` into `assets/fonts/` later needs no code or csproj change | coordinator note |

### 0.3 Deviations and reconciliations (read before building)

1. **Font pack URI.** DESIGN shows `pack://application:,,,/Fonts/#Poppins`. Fonts live in the Theme assembly, so the URI
   is `pack://application:,,,/IluPrompter.Theme;component/Fonts/#Poppins`. `ThemeResources.UiFont` and the
   `Ilu.Font.Ui` key encapsulate this.
2. **No windowed output fallback.** RESEARCH §6 suggested a windowed output when the display is missing. DESIGN forbids
   the output on the laptop screen, so the output stays closed or hidden. The engine keeps working because the preview
   ticks it when the output is not rendering (`TickIfStale`, §3).
3. **Section markers in the editor** stay real `## Name` paragraphs, styled (muted label + rule) by
   `IScriptFormatter.StyleSectionParagraphs`. DESIGN's non-editable "§ 2 Sponsors" row would break RTF/paste round-trips.
   It is a later enhancement (adorner), not v1.
4. **Xbox mapping** follows DESIGN (RT forward, LT reverse), not RESEARCH (left stick). The left stick is bound but
   `AxisRole.None` by default.
5. **No Stop command** (DESIGN). The fail-safe on controller loss ramps to 0 and pauses.
6. **ShuttleXpress has 5 buttons**, but DESIGN's table implies 6 (centre + 1–5). Default mapping used: B1 previous
   section, B2 previous paragraph, **B3 (centre) play/pause**, B4 next paragraph, B5 next section. B6–B8 (ShuttlePRO
   only) = Blank, Jump to start, toggle mode. → open question for the user.
7. **Wheel.** By default the wheel drives the prompt only over the preview (DESIGN). In addition, a *dedicated* mouse can
   be selected (Raw Input, works unfocused). While a dedicated device is set, the over-preview wheel is ignored, to avoid
   double counting.
8. **Highlight "0.1 em wider than the run"** (DESIGN Output › Rendering) is not achievable with FlowDocument
   backgrounds. v1 uses the plain run background. Noted for a future renderer.

### 0.4 Open questions for the user (do not block the build)

ShuttleXpress button mapping (§0.3.6). Approval to download Poppins Bold/Italic (the app falls back to synthetic
bold/italic until then). DESIGN's own four open questions (lines visible default, editing while live, section names on
output, app icon).

---

## 1. Solution and project layout

### 1.1 Folder tree (after the scaffold commit)

```
ILU Prompter/
├─ .gitignore                      (exists)
├─ DESIGN.md                       (exists; visual system)
├─ Directory.Build.props           TFM switch + shared compiler settings
├─ Directory.Packages.props        central package versions (CPM)
├─ IluPrompter.sln
├─ assets/                         (exists) fonts/*.ttf + OFL-*.txt, branding/wall-logo.png
├─ docs/
│  ├─ RESEARCH.md                  (exists)
│  ├─ ARCHITECTURE.md              this file
│  └─ streams/                     one notes file per stream (A-core.md … E-documents.md), created by each stream
├─ samples/                        stream E: sample-short.txt, sample-20-pages.txt (Phase-0 benchmark script)
├─ src/
│  ├─ IluPrompter.Core/            net8.0-windows, NO WPF. Contracts + engine + router + settings + maths
│  │  ├─ Contracts/                FROZEN: Commands, Input, Engine, Geometry, Document, Output, Settings, Diagnostics
│  │  ├─ Engine/                   ScrollEngine (A)
│  │  ├─ Input/                    InputRouter (A), KeyModeState (frozen, complete)
│  │  ├─ Layout/                   LayoutMath: MarginMath, TypographyMath, SpeedCurve, AxisCurve, TimeEstimator, MotionTimings (A)
│  │  ├─ Settings/                 JsonSettingsService, DefaultBindings (A)
│  │  ├─ Documents/                PromptDocumentService (frozen, complete)
│  │  ├─ Diagnostics/              FileAppLog (frozen, complete)
│  │  └─ CoreServiceCollectionExtensions.cs (A)
│  ├─ IluPrompter.Theme/           WPF class library: IluTokens.xaml (generated), IluControls.xaml, ThemeResources, linked fonts + logo (D)
│  ├─ IluPrompter.Documents/       WPF class library: .ilup, importers, snapshot, navigator, output document, geometry, formatter (E)
│  │  ├─ Contracts/                FROZEN: DocumentContracts.cs
│  │  ├─ Storage/ Import/ Layout/ Editing/
│  │  └─ DocumentsServiceCollectionExtensions.cs
│  ├─ IluPrompter.Input/           WPF class library: Raw Input wheel, XInput, Contour HID, in-window bridge (B)
│  │  ├─ Contracts/                FROZEN: IAppInputBridge.cs
│  │  ├─ RawInput/ XInput/ Shuttle/ AppWindow/
│  │  └─ InputServiceCollectionExtensions.cs
│  ├─ IluPrompter.Rendering/       WPF class library: output thread/window, monitors, preview, shared prompter surface (C)
│  │  ├─ Output/ Monitors/ Preview/ Surface/ Diagnostics/
│  │  ├─ NativeMethods.txt         CsWin32 input
│  │  └─ RenderingServiceCollectionExtensions.cs
│  └─ IluPrompter.App/             WPF exe (ILUPrompter.exe): composition root, control window, settings window, dialogs (D)
│     ├─ App.xaml(.cs) app.manifest
│     ├─ Composition/ Views/ ViewModels/ Editing/ Interop/
└─ tests/
   ├─ IluPrompter.Core.Tests/      (A) pure xUnit
   ├─ IluPrompter.Input.Tests/     (B) parsing, chord formatting, normalisation
   ├─ IluPrompter.Documents.Tests/ (E) xUnit + Xunit.StaFact [WpfFact] for FlowDocument work
   ├─ IluPrompter.Rendering.Tests/ (C) monitor resolution, layout key, preview scale maths
   └─ IluPrompter.App.Tests/       (D) token/hex-literal test, view-model tests
```

### 1.2 Projects and dependencies

```
Core       → (nothing)                      no WPF
Theme      → (nothing)                      WPF resources only
Documents  → Core, Theme
Input      → Core
Rendering  → Core, Documents, Theme
App        → Core, Theme, Documents, Input, Rendering
Tests      → the project under test (+ Core; Rendering/App tests also Theme)
```

| Project | SDK flags | References | Owner |
|---|---|---|---|
| IluPrompter.Core | no `UseWPF` | M.E.DI.Abstractions | A (+ frozen files) |
| IluPrompter.Theme | `UseWPF` | — | D |
| IluPrompter.Documents | `UseWPF` | Core, Theme; OpenXml; M.E.DI.Abstractions | E |
| IluPrompter.Input | `UseWPF`, `AllowUnsafeBlocks` | Core; Vortice.XInput, HidSharp, RawInput.Sharp; M.E.DI.Abstractions | B |
| IluPrompter.Rendering | `UseWPF`, `AllowUnsafeBlocks` | Core, Documents, Theme; CsWin32; M.E.DI.Abstractions | C |
| IluPrompter.App | `WinExe`, `UseWPF`, `AllowUnsafeBlocks`, RID win-x64 | all; M.E.DI; CommunityToolkit.Mvvm | D |
| tests/* | `UseWPF` except Core.Tests | xunit 2.9.3, runner 2.8.2, Test.Sdk 17.11.1, Xunit.StaFact 1.2.69 (Documents/Rendering/App) | per stream |

Core is `net8.0-windows` (not `net8.0`) only so that the single TFM property applies everywhere. It must not reference
WPF or WinForms.

### 1.3 Build, test, publish (from the repo root)

```powershell
dotnet build IluPrompter.sln
dotnet test IluPrompter.sln
dotnet run --project src/IluPrompter.App -- --diag          # --diag = frame timing + input trace in the log
dotnet publish src/IluPrompter.App -c Release -p:SelfContained=true -o publish/ILUPrompter
```

Publish is a self-contained **folder** (not single-file, see RESEARCH §6 on AppLocker/%TEMP% extraction). Logs go to
`%LOCALAPPDATA%\ILU Prompter\logs\`, settings to `%APPDATA%\ILU Prompter\settings.json`, recovery copies to
`%LOCALAPPDATA%\ILU Prompter\recovery\`. No admin, no installer.

### 1.4 NuGet packages (central versions; all support net8.0)

| Package | Version | Used by | Licence | Note |
|---|---|---|---|---|
| Microsoft.Extensions.DependencyInjection | 8.0.1 | App | MIT | |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | libraries | MIT | |
| CommunityToolkit.Mvvm | 8.3.2 | App | MIT | 8.3.x chosen over 8.4 to stay on the Roslyn shipped with SDK 8.0.301; use field-based `[ObservableProperty]` |
| Vortice.XInput | 3.6.2 | Input | MIT | net8.0 lib; still loads on net10 (3.8.x optional after the switch) |
| HidSharp | 2.6.4 | Input | Apache-2.0 (confirmed in package LICENSE.txt) | netstandard2.0 |
| RawInput.Sharp | 0.1.3 | Input | zlib | net7.0 lib, runs on net8+ |
| Microsoft.Windows.CsWin32 | 0.3.106 | Rendering | MIT | build-time only (`PrivateAssets=all`) |
| DocumentFormat.OpenXml | 3.1.1 | Documents | MIT | |
| xunit / xunit.runner.visualstudio | 2.9.3 / 2.8.2 | tests | Apache-2.0 | |
| Microsoft.NET.Test.Sdk | 17.11.1 | tests | MIT | |
| Xunit.StaFact | 1.2.69 | WPF tests | MS-PL | `[WpfFact]` runs on an STA thread with a Dispatcher |

Packages are added only through `Directory.Packages.props` (scaffold). A stream that believes it needs a new package
must stop and raise a contract request (§7). Never add MIDI/OSC/ASP.NET in these streams (Phase 3).

### 1.5 Switching to .NET 10 later

1. Install the .NET 10 SDK per user (`dotnet-install.ps1 -Channel 10.0 -InstallDir $env:LOCALAPPDATA\dotnet`).
2. Change `<IluTargetFramework>net8.0-windows</IluTargetFramework>` to `net10.0-windows` in `Directory.Build.props`.
3. `dotnet build && dotnet test`. Optionally bump Vortice.XInput to 3.8.x and M.E.DI to 10.x. No source changes are
   expected, because no API newer than .NET 8 may be used and `LangVersion` stays 12.

---

## 2. Contracts

All contract files below go into the scaffold commit **verbatim** and are then **frozen** (§7). Namespaces:
`IluPrompter.Core` for everything in Core, and `IluPrompter.Documents`, `IluPrompter.Input`,
`IluPrompter.Rendering`, `IluPrompter.Theme` for the rest.

### 2.1 Coordinate system and units (normative)

- **Output DIP** = output-display pixels ÷ output DpiScale. All layout, geometry and engine positions are in output
  DIPs, at the output's logical size. If no display has ever been seen, the size is 1920 × 1080 @ 1.0.
- **Text area** = the output minus margins. `TypographyMath.ComputeTextArea` gives Left/Top/Width/Height, the
  reading-line Y (from the text-area top, = `ReadingLineFraction × Height`), base font size
  `Height / (LinesVisible × LineSpacing)` and base line height `= size × LineSpacing`.
- **Document Y** = y inside the laid-out output FlowDocument (PagePadding 0, y = 0 at the top of the first block).
- **Position P** (engine) = the document Y shown at the top of the text area. The reading line shows document Y `P + R`.
  The renderer applies `TranslateTransform.Y = −P` to the hosted document inside the clipped text area.
- **Bounds**: `Pmin = LineCentre(0) − R` (DESIGN: the first line starts on the reading line, black above).
  `Pmax = EndTop + BaseLineHeight/2 − R` (END's centre on the reading line).
- **Line on the reading line** = `FindLineAt(P + R)`. Every alignment target puts a line's **centre** on the reading line.
- **Speed** = UI units −10…+10. `SpeedCurve` converts to lpm and DIP/s using `geometry.BaseLineHeight`.
- **Time** = `Stopwatch.GetTimestamp()` ticks everywhere (engine, input timestamps).

### 2.2 Core: commands

`src/IluPrompter.Core/Contracts/Commands.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>The two scrolling modes (DESIGN.md glossary: "Display scrolling", "Line scrolling").</summary>
public enum ScrollMode
{
    Display = 0,
    Line = 1,
}

/// <summary>
/// Every operator verb that a key, controller button or transport button can trigger.
/// There is deliberately no Stop: pausing is always enough (DESIGN.md, Keyboard shortcuts).
/// </summary>
public enum PrompterCommand
{
    None = 0,
    PlayPause,
    Play,
    Pause,
    SpeedUp,            // + Scroll.SpeedStep (0.5)
    SpeedDown,          // - Scroll.SpeedStep
    SpeedUpFine,        // + Scroll.FineSpeedStep (0.1)
    SpeedDownFine,      // - Scroll.FineSpeedStep
    SpeedReset,         // back to Scroll.StartSpeed
    LineNext,
    LinePrevious,
    ParagraphNext,
    ParagraphPrevious,
    SectionNext,        // a cut; leaves the transport paused
    SectionPrevious,    // a cut; leaves the transport paused
    JumpToStart,        // a cut; leaves the transport paused
    JumpToEnd,          // a cut; leaves the transport paused
    ToggleScrollMode,
    SetDisplayScrolling,
    SetLineScrolling,
    ToggleBlank,        // handled by the router via IOutputSurface.SetBlank
    ToggleOutput,       // raised as IInputRouter.AppCommandRequested (the shell may confirm before closing)
}

/// <summary>
/// What a key or controller button does. <see cref="LineMode"/> replaces <see cref="Display"/>
/// while in Line scrolling; null means "same as Display".
/// </summary>
public sealed record BindingAction(PrompterCommand Display, PrompterCommand? LineMode = null)
{
    public PrompterCommand For(ScrollMode mode) =>
        mode == ScrollMode.Line && LineMode is { } line ? line : Display;
}
```

### 2.3 Core: input events, providers, router

`src/IluPrompter.Core/Contracts/Input.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>The controller families. Keyboard is always active; exactly one of the others is "the controller".</summary>
public enum ControllerKind
{
    Keyboard = 0,
    MouseWheel = 1,
    Xbox = 2,
    JogShuttle = 3,
}

/// <summary>DeviceIds used by the in-window bridge (IAppInputBridge).</summary>
public static class AppSources
{
    /// <summary>Keyboard while keys are with the transport, and the wheel/buttons over the preview.</summary>
    public const string App = "app";

    /// <summary>Keyboard while focus is in any text input (editor, number box, settings field):
    /// only Global bindings apply, whatever IKeyModeState says.</summary>
    public const string TextInput = "app:text";
}

/// <summary>Identifies one physical device. DeviceId is provider-defined and stable across restarts where possible
/// (Raw Input / HID device path, XInput "xinput:0".."xinput:3", keyboard "app").</summary>
public readonly record struct InputSourceId(ControllerKind Kind, string DeviceId);

/// <summary>
/// Raw, normalised controller input. Providers do NOT apply deadzones, curves, inversion or bindings;
/// the router (Core) does all of that from settings.
/// Timestamp = <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> when the input was read.
/// </summary>
public abstract record InputEvent(InputSourceId Source, string ControlId, long Timestamp);

/// <summary>Absolute axis position. Sticks and the shuttle ring: -1..+1 (positive = up / clockwise).
/// Triggers: 0..+1. Raised on change only (change greater than 0.002), plus once on connect.</summary>
public sealed record AxisInput(InputSourceId Source, string ControlId, long Timestamp, double Value)
    : InputEvent(Source, ControlId, Timestamp);

/// <summary>Relative movement. Mouse wheel: notches (WM delta / 120, fractional for high-resolution wheels;
/// positive = away from the user). Jog wheel: detents (signed, positive = clockwise).</summary>
public sealed record RelativeInput(InputSourceId Source, string ControlId, long Timestamp, double Amount)
    : InputEvent(Source, ControlId, Timestamp);

/// <summary>A button or key edge. For the keyboard, ControlId is a key chord (see <see cref="ControlIds"/>).</summary>
public sealed record ButtonInput(InputSourceId Source, string ControlId, long Timestamp, bool IsPressed, bool IsRepeat = false)
    : InputEvent(Source, ControlId, Timestamp);

/// <summary>Canonical control ids. Providers MUST use exactly these strings; bindings in settings refer to them.</summary>
public static class ControlIds
{
    // Mouse (Raw Input dedicated device, or the WPF wheel over the preview)
    public const string Wheel = "Wheel";
    public const string MiddleButton = "Middle";
    public const string XButton1 = "XButton1";
    public const string XButton2 = "XButton2";

    // Xbox (names match Vortice.XInput GamepadButtons where one exists)
    public const string LeftTrigger = "LeftTrigger";
    public const string RightTrigger = "RightTrigger";
    public const string LeftThumbX = "LeftThumbX";
    public const string LeftThumbY = "LeftThumbY";
    public const string RightThumbX = "RightThumbX";
    public const string RightThumbY = "RightThumbY";
    public const string A = "A";
    public const string B = "B";
    public const string X = "X";
    public const string Y = "Y";
    public const string DPadUp = "DPadUp";
    public const string DPadDown = "DPadDown";
    public const string DPadLeft = "DPadLeft";
    public const string DPadRight = "DPadRight";
    public const string LeftShoulder = "LeftShoulder";
    public const string RightShoulder = "RightShoulder";
    public const string View = "Back";     // the View button is XINPUT_GAMEPAD_BACK
    public const string Menu = "Start";    // the Menu button is XINPUT_GAMEPAD_START
    public const string LeftThumbPress = "LeftThumb";
    public const string RightThumbPress = "RightThumb";

    // Contour ShuttleXpress / ShuttlePRO
    public const string ShuttleRing = "Ring";
    public const string JogWheel = "Jog";
    /// <summary>Contour buttons are numbered from 1 in bit order of the 16-bit button mask (bit 0 = Button1).</summary>
    public static string ShuttleButton(int oneBased) => "Button" + oneBased.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Keyboard chord format: [Ctrl+][Alt+][Shift+]Key, modifiers always in that order, Key = the
    /// System.Windows.Input.Key enum name ("Space", "PageDown", "F5", "Escape", "Home", "Up", "D0", "B",
    /// "OemComma", "OemQuestion"). Example: "Ctrl+Shift+PageDown". Windows key is never used.
    /// </summary>
    public static string KeyChord(bool ctrl, bool alt, bool shift, string key) =>
        (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + key;
}

public enum DeviceConnection
{
    Connected = 0,
    Disconnected = 1,   // was connected, now lost (fail-safe applies)
    NotFound = 2,       // never seen since Start
}

public enum BatteryLevel
{
    Unknown = 0,
    Empty,
    Low,
    Medium,
    Full,
    Wired,
}

/// <summary>Connection state of a device, reported by providers through <see cref="IInputSink.ReportStatus"/>.</summary>
public sealed record DeviceStatus(
    InputSourceId Source,
    string DisplayName,
    DeviceConnection Connection,
    BatteryLevel Battery = BatteryLevel.Unknown,
    string? Detail = null);

/// <summary>A selectable device (Settings › Controller).</summary>
public sealed record InputDeviceInfo(ControllerKind Kind, string DeviceId, string DisplayName, bool IsConnected);

/// <summary>Implemented by the router. Thread-safe; providers call it from their own threads.</summary>
public interface IInputSink
{
    /// <summary>Non-blocking. Returns true when the input is bound to an action in the current context
    /// (the keyboard bridge uses this to set KeyEventArgs.Handled).</summary>
    bool Submit(InputEvent input);

    void ReportStatus(DeviceStatus status);
}

/// <summary>
/// A background controller source (Raw Input wheel, XInput, Contour HID). Providers own their threads,
/// never touch WPF UI objects, and never throw out of Start/Stop (they report problems via DeviceStatus).
/// </summary>
public interface IScrollInputProvider : IDisposable
{
    ControllerKind Kind { get; }

    bool IsRunning { get; }

    /// <summary>Enumerate currently attached devices of this kind. Safe to call while running.</summary>
    IReadOnlyList<InputDeviceInfo> EnumerateDevices();

    /// <summary>Start reading. deviceId null = first available device. Idempotent: calling Start again
    /// with a different deviceId switches device. Reports NotFound/Connected via the sink.</summary>
    void Start(IInputSink sink, string? deviceId);

    /// <summary>Stop reading and release the device. Idempotent.</summary>
    void Stop();
}

/// <summary>Who has the keys (DESIGN.md "Two key modes"). Typing = caret in the script.</summary>
public enum KeyMode
{
    Transport = 0,
    Typing = 1,
}

public interface IKeyModeState
{
    KeyMode Mode { get; set; }

    /// <summary>Raised on the thread that set Mode (the control UI thread).</summary>
    event EventHandler<KeyMode>? Changed;
}

/// <summary>Analog axis behaviour. Speed = bidirectional stick or shuttle ring;
/// SpeedForward / SpeedReverse = triggers (0..1 mapped to +speed / -speed).</summary>
public enum AxisRole
{
    None = 0,
    Speed,
    SpeedForward,
    SpeedReverse,
}

/// <summary>speed = sign(v) * MaxSpeed * ((|v| - Deadzone) / (1 - Deadzone))^Exponent, zero inside the deadzone.
/// Speed is in UI speed units (-10..+10, see SpeedCurveSettings).</summary>
public sealed record AxisBinding(
    AxisRole Role,
    double Deadzone = 0.1,
    double Exponent = 2.0,
    double MaxSpeed = 10.0,
    bool Invert = false);

/// <summary>What a relative control does. Speed = nudge the set speed; Lines = step lines (fractional
/// amounts accumulate until a whole line); Pixels = scroll directly by output DIPs.</summary>
public enum RelativeRole
{
    None = 0,
    Speed,
    Lines,
    Pixels,
}

public sealed record RelativeBinding(
    RelativeRole DisplayRole,
    RelativeRole LineRole,
    double SpeedPerUnit = 0.5,
    double LinesPerUnit = 1.0,
    double PixelsPerUnit = 40.0,
    bool Invert = false)
{
    public RelativeRole For(ScrollMode mode) => mode == ScrollMode.Line ? LineRole : DisplayRole;
}

/// <summary>
/// Core's input hub: owns the providers, applies settings (selected controller, bindings, curves),
/// and turns input into engine commands. Implemented in Core (InputRouter), registered as a singleton
/// that also implements <see cref="IInputSink"/>.
/// </summary>
public interface IInputRouter
{
    /// <summary>Start the provider for Settings.Input.SelectedController (and re-apply on every settings change).</summary>
    void Start();

    void Stop();

    /// <summary>Run a command as if a bound button was pressed (transport buttons in the UI call this).
    /// Mode-aware. Thread-safe. Returns false if the command does not apply (e.g. Play at end).</summary>
    bool Execute(PrompterCommand command);

    /// <summary>Latest status per device seen since Start (keyboard excluded).</summary>
    IReadOnlyList<DeviceStatus> DeviceStatuses { get; }

    /// <summary>Raised on the provider's thread. UI must marshal.</summary>
    event EventHandler<DeviceStatus>? DeviceStatusChanged;

    /// <summary>Every input seen (bound or not), for the Settings "Test: move the controller" readout. Any thread.</summary>
    event EventHandler<InputEvent>? InputObserved;

    /// <summary>Commands the shell must perform itself (currently only ToggleOutput). Any thread.</summary>
    event EventHandler<PrompterCommand>? AppCommandRequested;
}
```

**Input normalisation rules (providers, stream B):**

| Source | Control ids | Event | Normalisation |
|---|---|---|---|
| In-window keyboard | key chord | `ButtonInput` press only (IsRepeat from WPF) | DeviceId `app`, or `app:text` when focus is in a text input |
| Wheel over preview | `Wheel`, `Middle`, `XButton1/2` | Relative / Button (both edges) | DeviceId `app`; Amount = `e.Delta/120.0` |
| Dedicated Raw Input mouse | same | same | DeviceId = device path; Amount = `(short)usButtonData/120.0` (fractional kept) |
| Xbox (XInput) | `LeftTrigger`… | Axis (sticks −1…1 = raw/32767 clamped; triggers 0…1 = raw/255), Button edges | DeviceId `xinput:N`; axis events on change > 0.002, plus once on connect |
| Contour | `Ring`, `Jog`, `ButtonN` | Ring axis = k/7 (k = int8 −7…7); Jog relative = wrapped counter diff; Button edges | DeviceId = HID path; first report after open = baseline (no jog event) |

**Router rules (stream A, `InputRouter`):**

- `Start()` starts only the provider for `Settings.Input.SelectedController`. MouseWheel starts `RawMouseWheelProvider`
  only when `DedicatedDeviceId` is set. Xbox starts with `deviceId = UserIndex >= 0 ? "xinput:N" : null`. Shuttle starts
  with `DeviceId`. All others are stopped. Re-apply when `Settings.Input` changes (reference inequality).
- Ignore input from a controller kind that is not selected (keyboard always accepted). Ignore `app` wheel/buttons unless
  MouseWheel is selected and `DedicatedDeviceId` is null.
- Keyboard context: **Typing** if `IKeyModeState.Mode == Typing` or DeviceId is `app:text`, otherwise **Transport**.
  Typing uses `Keyboard.Global` only. Transport uses `Global`, then `Transport`. The action is `binding.For(engine.Status.Mode)`.
  Return true (handled) when a binding exists, even if the command is a no-op right now.
- Repeats (`IsRepeat`) act only for SpeedUp/Down(Fine), Line*, Paragraph*. Other commands ignore repeats but still
  return true.
- Buttons act on press. Axes: `AxisCurve.Apply` → `EngineCommand.SetAxisSpeed(slot, speed)`. A slot is assigned per
  (source, controlId) on first sight, max 8, and freed on disconnect.
- Relative: forward = **towards the user for the wheel** (`−Amount`) and **clockwise for the jog** (`+Amount`), negated
  by `Invert`. Role per mode: `Speed` → `NudgeSpeed(units × SpeedPerUnit)`. `Lines` → accumulate `units × LinesPerUnit`
  per source and post `StepLines(n)` for each whole line. `Pixels` → `ScrollPixels(units × PixelsPerUnit)`.
- `Execute(cmd)`:
  - PlayPause → TogglePlayPause (returns false at end); Play/Pause.
  - SpeedUp/Down(Fine) → `NudgeSpeed(±step)`; SpeedReset.
  - Line/Paragraph/Section → `Step*(±1)`; JumpToStart/End.
  - Mode commands → `SetMode`/`ToggleMode`.
  - ToggleBlank → `IOutputSurface.SetBlank(!State.IsBlank)`.
  - ToggleOutput → raise `AppCommandRequested`.
- `ReportStatus`: store; raise `DeviceStatusChanged`. `Disconnected` on the selected controller → `FailSafeStop` and
  clear its slots and accumulators. `InputObserved` is raised for every Submit.

### 2.4 Core: scroll engine

`src/IluPrompter.Core/Contracts/Engine.cs`

```csharp
namespace IluPrompter.Core;

public enum TransportState
{
    Paused = 0,
    Playing = 1,
    AtEnd = 2,      // END reached the reading line; Play is refused until the position moves back
}

public enum EngineCommandKind
{
    Play,
    Pause,
    TogglePlayPause,
    SetSpeed,           // Value = set speed in UI units (-10..+10), clamped to Scroll.MinSpeed..MaxSpeed
    NudgeSpeed,         // Value = signed delta in UI units; may cross zero (reverses)
    ResetSpeed,         // set speed = Scroll.StartSpeed
    SetAxisSpeed,       // Index = axis slot 0..MaxAxisSlots-1, Value = speed units (0 = released)
    FailSafeStop,       // controller lost: clear all axis slots, ramp to 0 over 300 ms, then Paused
    StepLines,          // Index = signed count; animated (line-step 220 ms); keeps play state
    StepParagraphs,     // Index = signed count; animated (paragraph-step 320 ms); keeps play state
    StepSections,       // Index = signed count; a cut; then Paused
    JumpToStart,        // a cut; then Paused
    JumpToEnd,          // a cut; then AtEnd
    GoToSection,        // Index = section index; a cut; then Paused
    SeekToAnchor,       // Index = paragraph index, Value = char offset; a cut; then Paused ("Jump the prompt here")
    SeekToOffset,       // Value = position in output DIPs; a cut; keeps play state (margin/preview scrubbing)
    ScrollPixels,       // Value = signed output DIPs, applied immediately (direct wheel); keeps play state
    SetMode,            // Index = (int)ScrollMode
    ToggleMode,
}

/// <summary>Allocation-free command posted to the engine from any thread.</summary>
public readonly record struct EngineCommand(EngineCommandKind Kind, double Value = 0, int Index = 0)
{
    public const int MaxAxisSlots = 8;

    public static EngineCommand Play() => new(EngineCommandKind.Play);
    public static EngineCommand Pause() => new(EngineCommandKind.Pause);
    public static EngineCommand TogglePlayPause() => new(EngineCommandKind.TogglePlayPause);
    public static EngineCommand SetSpeed(double speed) => new(EngineCommandKind.SetSpeed, speed);
    public static EngineCommand NudgeSpeed(double delta) => new(EngineCommandKind.NudgeSpeed, delta);
    public static EngineCommand ResetSpeed() => new(EngineCommandKind.ResetSpeed);
    public static EngineCommand SetAxisSpeed(int slot, double speed) => new(EngineCommandKind.SetAxisSpeed, speed, slot);
    public static EngineCommand FailSafeStop() => new(EngineCommandKind.FailSafeStop);
    public static EngineCommand StepLines(int count) => new(EngineCommandKind.StepLines, 0, count);
    public static EngineCommand StepParagraphs(int count) => new(EngineCommandKind.StepParagraphs, 0, count);
    public static EngineCommand StepSections(int count) => new(EngineCommandKind.StepSections, 0, count);
    public static EngineCommand JumpToStart() => new(EngineCommandKind.JumpToStart);
    public static EngineCommand JumpToEnd() => new(EngineCommandKind.JumpToEnd);
    public static EngineCommand GoToSection(int index) => new(EngineCommandKind.GoToSection, 0, index);
    public static EngineCommand SeekToAnchor(DocumentAnchor anchor) => new(EngineCommandKind.SeekToAnchor, anchor.CharOffset, anchor.ParagraphIndex);
    public static EngineCommand SeekToOffset(double positionDip) => new(EngineCommandKind.SeekToOffset, positionDip);
    public static EngineCommand ScrollPixels(double deltaDip) => new(EngineCommandKind.ScrollPixels, deltaDip);
    public static EngineCommand SetMode(ScrollMode mode) => new(EngineCommandKind.SetMode, 0, (int)mode);
    public static EngineCommand ToggleMode() => new(EngineCommandKind.ToggleMode);
}

/// <summary>
/// The text area on the output, in output DIPs. ReadingLineY is measured from the top of the text area
/// (= Output.ReadingLineFraction * TextAreaHeight).
/// </summary>
public readonly record struct ViewportMetrics(double TextAreaWidth, double TextAreaHeight, double ReadingLineY);

/// <summary>
/// Immutable engine state for UI readouts, replaced (new instance) only when a field changes —
/// never per frame unless LineIndex/SectionIndex actually changed.
/// </summary>
public sealed record EngineStatus
{
    public TransportState Transport { get; init; } = TransportState.Paused;
    public ScrollMode Mode { get; init; } = ScrollMode.Display;

    /// <summary>The operator's set speed, UI units -10..+10.</summary>
    public double SetSpeed { get; init; }

    /// <summary>The speed actually applied now (ramps and axis input included), UI units.</summary>
    public double EffectiveSpeed { get; init; }

    public double LinesPerMinute { get; init; }
    public double WordsPerMinute { get; init; }

    /// <summary>Line scrolling with Play: seconds between steps (60 / lpm). 0 in Display scrolling.</summary>
    public double StepIntervalSeconds { get; init; }

    public bool IsAxisActive { get; init; }

    public int LineIndex { get; init; }          // line on the reading line, 0-based; -1 if no lines
    public int LineCount { get; init; }
    public int ParagraphIndex { get; init; }
    public int ParagraphCount { get; init; }
    public int SectionIndex { get; init; } = -1; // -1 = before the first section marker
    public int SectionCount { get; init; }

    public DocumentAnchor ReadingAnchor { get; init; }
    public bool IsAtStart { get; init; } = true;
    public bool IsAtEnd { get; init; }

    /// <summary>Estimated time from the reading line to END at |SetSpeed| (not effective speed).</summary>
    public double SecondsRemaining { get; init; }

    public string LayoutKey { get; init; } = "";
}

/// <summary>
/// The scroll engine: single source of truth for position (output DIPs, document coordinates of the
/// top of the text area). Thread-safe. Ticked by the output thread every frame; the preview ticks it
/// only when the output is not rendering (TickIfStale).
/// </summary>
public interface IScrollEngine
{
    /// <summary>Enqueue a command (any thread, lock-free, allocation-free). Applied on the next Tick.</summary>
    void Post(EngineCommand command);

    /// <summary>Advance to the given time (Stopwatch ticks). Integrates pos += v * dt, runs ramps and
    /// step animations, drains the command queue, publishes position. Ignores timestamps not newer than
    /// the last tick. Must not allocate on the steady-state path.</summary>
    void Tick(long timestamp);

    /// <summary>Tick only if no Tick happened for staleAfterSeconds. Returns true if it ticked.</summary>
    bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05);

    /// <summary>Install a new layout (any thread). If geometry.LayoutKey equals the current key, only the
    /// viewport is updated. Otherwise the engine re-anchors: the DocumentAnchor that was on the reading line
    /// stays on the reading line. A viewport change keeps the same document Y on the reading line.</summary>
    void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport);

    /// <summary>Latest published position in output DIPs (lock-free atomic read, any thread).
    /// The text area shows document Y in [pos, pos + TextAreaHeight).</summary>
    double ReadPosition();

    /// <summary>Latest status (volatile reference read, any thread).</summary>
    EngineStatus Status { get; }

    /// <summary>The geometry in use (immutable, safe to read from any thread). Null until the first SetLayout.</summary>
    LayoutGeometry? Geometry { get; }

    ViewportMetrics Viewport { get; }

    /// <summary>Raised on the ticking thread when Status is replaced. UI subscribers must marshal and coalesce
    /// (or simply poll Status on a 10 Hz DispatcherTimer).</summary>
    event EventHandler<EngineStatus>? StatusChanged;
}
```

**Engine behaviour (normative, stream A):**

| Topic | Rule |
|---|---|
| Integration | `P += v·dt`, with `dt` from consecutive Tick timestamps, clamped to ≤ 0.1 s. A timestamp not newer than the last is ignored. Position is published via `Interlocked.Exchange` on the double's bits |
| Speed ramps (Display) | Target speed = `Playing ? SetSpeed : 0`. Applied speed moves linearly to the target: 0 → set over `RampStart` 0.4 s, set → 0 over `RampStop` 0.2 s, set → set′ over `RampChange(Acceleration)`. The slope is fixed when the target changes |
| Axis input | If any axis slot ≠ 0, effective speed = clamp(Σ slots, ±MaxSpeed), applied directly (no ramp), and `IsAxisActive`. When all slots return to 0, ramp (RampStop) back to the transport target. Axis input works whether Playing or Paused |
| Line steps | `StepLines(n)`: target line = clamp(current + n). It animates from the current animated offset to `LineCentre(target) − R` over `LineStep` 0.22 s, cubic ease-out, **superposed** on continuous motion. A new step during an animation retargets from the previous *target* line (chaining, DESIGN "never queue"). Keeps the play state |
| Paragraph steps | Next = first line of paragraph (current + 1). Previous = first line of the current paragraph if the reading line is below its first line by more than half a line, otherwise paragraph − 1. `ParagraphStep` 0.32 s |
| Sections | Cut (no animation) to the first line whose top ≥ `Section.Top`, then Paused (speed ramp cancelled, applied speed 0). Previous rule as for paragraphs. `GoToSection(i)` the same. Before the first section, "previous" = start |
| Jumps | JumpToStart → `Pmin`, Paused. JumpToEnd → `Pmax`, AtEnd. SeekToAnchor → line of the anchor centred, Paused. SeekToOffset → `P = value` (clamped), keeps play state. ScrollPixels → `P += value` (clamped), keeps play state |
| End | Moving forward and reaching `Pmax` → clamp, `AtEnd`, applied speed 0, SetSpeed kept. Play/TogglePlayPause refused while AtEnd. Any backward move (step, jump, negative axis) → Paused |
| Start | Moving backward and reaching `Pmin` → clamp, Paused |
| Line mode | Display rules still apply to jumps and steps. **Play** = auto-step one line every `60 / lpm(|SetSpeed|)` s in the sign of SetSpeed (each step is a LineStep animation); `StepIntervalSeconds` reports it. Axis speed in Line mode sets the auto-step rate instead of velocity. Switching mode keeps the position; Display → Line snaps to the nearest line centre with a LineStep animation |
| Pause at sections | If `Scroll.PauseAtSections` and the reading line crosses a section target while Playing forward → clamp exactly to the target and Pause |
| Fail-safe | `FailSafeStop`: clear axis slots and ramp the applied speed to 0 over 0.3 s, then Paused |
| Layout change | `SetLayout` with a new LayoutKey: anchor = `old.GetAnchor(P+R)` plus the fractional position inside that line, then `P = newLineTop + f·newLineHeight − R`. Running step animations snap to their target first. Same key: viewport only, keep `P + R_old − R_new`. Before any geometry, use `LayoutGeometry.Empty` and viewport (1600, 952, 314) |
| Status | Replace `Status` only when a rounded field changes: EffectiveSpeed/LinesPerMinute/WordsPerMinute to 0.1, SecondsRemaining to 1 s; indices exact. `SecondsRemaining = SecondsForLines(LineCount−1−LineIndex, SetSpeed)` |
| Performance | Steady-state `Tick` allocates **0 bytes** (test with `GC.GetAllocatedBytesForCurrentThread`). No LINQ, closures or boxing in Tick. One lock (`_gate`), never held while raising events |

### 2.5 Core: layout geometry

`src/IluPrompter.Core/Contracts/Geometry.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>
/// A stable position in the script, independent of layout: the N-th paragraph in canonical order
/// (every Paragraph in document order, descending into List/ListItem/Section/Table cells; see
/// Documents.IDocumentNavigator) and a character offset within that paragraph's plain text.
/// </summary>
public readonly record struct DocumentAnchor(int ParagraphIndex, int CharOffset)
{
    public static DocumentAnchor Start => new(0, 0);
}

/// <summary>A section marker as laid out on the output.</summary>
public sealed record SectionMarker(int Index, string Name, int ParagraphIndex, double Top);

/// <summary>
/// Immutable layout tables for one layout of one snapshot, in output DIPs, document coordinates
/// (y = 0 is the top of the first block; the host uses PagePadding 0). Built on a WPF thread by
/// Documents.ILayoutGeometryBuilder, then shared read-only with any thread.
/// Arrays are owned by this instance and must not be mutated after construction.
/// </summary>
public sealed class LayoutGeometry
{
    private readonly double[] _lineTops;
    private readonly double[] _lineHeights;
    private readonly int[] _lineParagraphs;
    private readonly int[] _lineCharOffsets;
    private readonly double[] _paragraphTops;
    private readonly double[] _sectionTops;

    public LayoutGeometry(
        string layoutKey,
        long snapshotVersion,
        double baseLineHeight,
        double documentHeight,
        double endTop,
        double[] lineTops,
        double[] lineHeights,
        int[] lineParagraphs,
        int[] lineCharOffsets,
        double[] paragraphTops,
        IReadOnlyList<SectionMarker> sections,
        int wordCount)
    {
        ArgumentNullException.ThrowIfNull(layoutKey);
        if (lineHeights.Length != lineTops.Length || lineParagraphs.Length != lineTops.Length || lineCharOffsets.Length != lineTops.Length)
            throw new ArgumentException("Line arrays must have equal length.");

        LayoutKey = layoutKey;
        SnapshotVersion = snapshotVersion;
        BaseLineHeight = baseLineHeight;
        DocumentHeight = documentHeight;
        EndTop = endTop;
        _lineTops = lineTops;
        _lineHeights = lineHeights;
        _lineParagraphs = lineParagraphs;
        _lineCharOffsets = lineCharOffsets;
        _paragraphTops = paragraphTops;
        Sections = sections;
        _sectionTops = new double[sections.Count];
        for (var i = 0; i < sections.Count; i++) _sectionTops[i] = sections[i].Top;
        WordCount = wordCount;
    }

    /// <summary>Identity of the layout inputs (snapshot version, width, font, spacing...). Equal keys = equal layout.</summary>
    public string LayoutKey { get; }
    public long SnapshotVersion { get; }

    /// <summary>Height of one Normal-size line (base font size * line spacing). Used for lpm to px/s.</summary>
    public double BaseLineHeight { get; }

    /// <summary>Total height including the END block and nothing after it.</summary>
    public double DocumentHeight { get; }

    /// <summary>Top of the END marker line. Scrolling stops when the END line's centre reaches the reading line.</summary>
    public double EndTop { get; }

    public IReadOnlyList<double> LineTops => _lineTops;
    public IReadOnlyList<double> LineHeights => _lineHeights;
    /// <summary>Paragraph index (canonical order) of each line.</summary>
    public IReadOnlyList<int> LineParagraphs => _lineParagraphs;
    /// <summary>Char offset within its paragraph at which each line starts.</summary>
    public IReadOnlyList<int> LineCharOffsets => _lineCharOffsets;
    /// <summary>Top of each paragraph, indexed by canonical paragraph index.</summary>
    public IReadOnlyList<double> ParagraphTops => _paragraphTops;
    public IReadOnlyList<SectionMarker> Sections { get; }
    public int WordCount { get; }

    public int LineCount => _lineTops.Length;
    public int ParagraphCount => _paragraphTops.Length;

    /// <summary>Index of the line whose [top, top+height) contains y; nearest line if y is outside all lines; -1 if no lines.</summary>
    public int FindLineAt(double y)
    {
        if (_lineTops.Length == 0) return -1;
        var i = LastAtOrBefore(_lineTops, y);
        return i < 0 ? 0 : i;
    }

    /// <summary>Index of the last paragraph whose top is at or above y (0 if y is above the first); -1 if none.</summary>
    public int FindParagraphAt(double y)
    {
        if (_paragraphTops.Length == 0) return -1;
        var i = LastAtOrBefore(_paragraphTops, y);
        return i < 0 ? 0 : i;
    }

    /// <summary>Index of the last section whose top is at or above y; -1 if y is before the first section.</summary>
    public int FindSectionAt(double y) => _sectionTops.Length == 0 ? -1 : LastAtOrBefore(_sectionTops, y);

    /// <summary>Vertical centre of line i.</summary>
    public double LineCentre(int i) => _lineTops[i] + (_lineHeights[i] / 2);

    /// <summary>The anchor at the start of the line containing y.</summary>
    public DocumentAnchor GetAnchor(double y)
    {
        var i = FindLineAt(y);
        return i < 0 ? DocumentAnchor.Start : new DocumentAnchor(_lineParagraphs[i], _lineCharOffsets[i]);
    }

    /// <summary>Index of the line containing the anchor (last line in that paragraph starting at or before CharOffset).
    /// Clamps to the nearest existing paragraph.</summary>
    public int FindLineForAnchor(DocumentAnchor anchor)
    {
        if (_lineTops.Length == 0) return -1;
        int lo = 0, hi = _lineTops.Length - 1, best = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            var cmp = _lineParagraphs[mid] != anchor.ParagraphIndex
                ? _lineParagraphs[mid].CompareTo(anchor.ParagraphIndex)
                : _lineCharOffsets[mid].CompareTo(anchor.CharOffset);
            if (cmp <= 0) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
        }
        return best;
    }

    public static LayoutGeometry Empty(string layoutKey = "empty", double baseLineHeight = 100) =>
        new(layoutKey, 0, baseLineHeight, 0, 0, [], [], [], [], [], Array.Empty<SectionMarker>(), 0);

    private static int LastAtOrBefore(double[] sorted, double y)
    {
        var i = Array.BinarySearch(sorted, y);
        if (i >= 0)
        {
            while (i + 1 < sorted.Length && sorted[i + 1] == y) i++;
            return i;
        }
        return ~i - 1;
    }
}
```

### 2.6 Core: document snapshot service

`src/IluPrompter.Core/Contracts/Document.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>A section marker paragraph ("## Name") found in the script.</summary>
public sealed record SectionInfo(int Index, string Name, int ParagraphIndex);

/// <summary>
/// An immutable, thread-agnostic copy of the script: FlowDocument XAML as produced by
/// TextRange.Save(DataFormats.Xaml) over a flattened document (paragraphs only, canonical order).
/// Each WPF thread that renders it loads its own FlowDocument from this string.
/// </summary>
public sealed record PromptDocumentSnapshot(
    long Version,
    string FlowDocumentXaml,
    double EditorBaseFontSize,
    int ParagraphCount,
    IReadOnlyList<SectionInfo> Sections,
    int WordCount)
{
    public static PromptDocumentSnapshot Empty { get; } =
        new(0, "", 20.0, 0, Array.Empty<SectionInfo>(), 0);
}

/// <summary>
/// Holds the current snapshot and file metadata. The editor (App) owns the live FlowDocument and
/// publishes snapshots here (debounced 250 ms); renderers subscribe.
/// </summary>
public interface IPromptDocumentService
{
    PromptDocumentSnapshot Current { get; }

    string? FilePath { get; }
    string Title { get; }
    bool IsDirty { get; }
    DateTimeOffset? LastSaved { get; }

    /// <summary>Monotonic version source for snapshots (thread-safe).</summary>
    long NextVersion();

    /// <summary>Replace the current snapshot (any thread). Snapshots with Version not greater than the
    /// current one are ignored. Raises SnapshotPublished synchronously on the calling thread.</summary>
    void Publish(PromptDocumentSnapshot snapshot);

    void SetFileInfo(string? filePath, string title, bool isDirty, DateTimeOffset? lastSaved);

    /// <summary>Raised on the publishing thread. Subscribers on other threads marshal and coalesce
    /// (only the newest snapshot matters).</summary>
    event EventHandler<PromptDocumentSnapshot>? SnapshotPublished;

    /// <summary>Raised on the calling thread of SetFileInfo.</summary>
    event EventHandler? FileInfoChanged;
}
```

### 2.7 Core: output surface and monitors

`src/IluPrompter.Core/Contracts/Output.cs`

```csharp
namespace IluPrompter.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public readonly record struct PixelSize(int Width, int Height);

/// <summary>A connected display. StableId = the QueryDisplayConfig target device path
/// (survives reordering and reboots); DisplayNumber = the number Windows shows in Settings › Display.</summary>
public sealed record MonitorInfo(
    string StableId,
    int DisplayNumber,
    string FriendlyName,
    PixelRect BoundsPx,
    PixelRect WorkAreaPx,
    double DpiScale,
    double RefreshHz,
    bool IsPrimary,
    bool IsDuplicated)
{
    /// <summary>"Display 2 · LG 55UH5" (DESIGN.md wording).</summary>
    public string Label => FriendlyName.Length == 0 ? $"Display {DisplayNumber}" : $"Display {DisplayNumber} · {FriendlyName}";
}

public interface IMonitorService
{
    /// <summary>Snapshot of connected displays (any thread).</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>The saved display if connected; else the first non-primary, non-duplicated display;
    /// else null. Never returns the primary (operator) display.</summary>
    MonitorInfo? ResolveOutputMonitor(string? savedStableId);

    /// <summary>Raised (debounced 500 ms, any thread) when displays are added, removed or reconfigured.</summary>
    event EventHandler? MonitorsChanged;
}

public enum OutputDisplayStatus
{
    Ready = 0,          // a usable output display exists
    NoSecondDisplay,    // only the operator display is connected
    Duplicated,         // Windows is mirroring; no extended display
    Missing,            // the chosen display vanished while the output was open (window hidden, waiting)
}

/// <summary>Size of the output display. Layout always uses DIPs = px / DpiScale of the output display.</summary>
public readonly record struct OutputGeometry(int WidthPx, int HeightPx, double DpiScale, double RefreshHz)
{
    public double WidthDip => WidthPx / DpiScale;
    public double HeightDip => HeightPx / DpiScale;
    public PixelSize SizePx => new(WidthPx, HeightPx);

    /// <summary>Used for layout when no output display has ever been seen.</summary>
    public static OutputGeometry Default1080 => new(1920, 1080, 1.0, 60.0);
}

/// <summary>Output window state. Geometry is the current display's, or the last known one when no display.</summary>
public sealed record OutputState(
    bool IsOpen,
    bool IsBlank,
    OutputDisplayStatus Display,
    MonitorInfo? Monitor,
    OutputGeometry Geometry)
{
    /// <summary>The presenter can see the script now (drives the LIVE chip and the green preview frame).</summary>
    public bool IsLive => IsOpen && !IsBlank && Display == OutputDisplayStatus.Ready;

    public static OutputState Initial { get; } =
        new(false, false, OutputDisplayStatus.NoSecondDisplay, null, OutputGeometry.Default1080);
}

/// <summary>
/// The output window, which lives on its own STA/Dispatcher thread. All methods are callable from any
/// thread; they marshal internally. Never shows the window on the primary display.
/// </summary>
public interface IOutputSurface
{
    OutputState State { get; }

    /// <summary>Raised on the output thread. UI must marshal.</summary>
    event EventHandler<OutputState>? StateChanged;

    /// <summary>Start the output thread and its Dispatcher (window created but not shown). Call once at startup.</summary>
    Task InitializeAsync();

    /// <summary>Show fullscreen on the resolved display. If none, stays closed and State.Display says why.</summary>
    Task ShowAsync();

    Task HideAsync();

    /// <summary>Blank = the presenter sees black; scrolling and the preview continue.</summary>
    void SetBlank(bool blank);

    /// <summary>Close the window, stop the Dispatcher and join the thread.</summary>
    Task ShutdownAsync();
}
```

### 2.8 Core: settings and persistence

`src/IluPrompter.Core/Contracts/Settings.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IluPrompter.Core;

/// <summary>
/// All persisted settings. Immutable; change via ISettingsService.Update(s => s with { ... }).
/// Stored as camelCase JSON at %APPDATA%\ILU Prompter\settings.json. Colours are DESIGN.md token
/// names (e.g. "ink-white"), never hex: hex lives only in Themes/IluTokens.xaml.
/// Record equality on collections is by reference; compare settings via JSON when testing.
/// </summary>
public sealed record PrompterSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public OutputSettings Output { get; init; } = new();
    public MarginSettings Margins { get; init; } = new();
    public TextSettings Text { get; init; } = new();
    public ScrollSettings Scroll { get; init; } = new();
    public InputSettings Input { get; init; } = new();
    public EditorSettings Editor { get; init; } = new();
    public WindowSettings ControlWindow { get; init; } = new();
    public IReadOnlyList<string> RecentFiles { get; init; } = Array.Empty<string>();

    public static PrompterSettings Default { get; } = new();
}

public enum TextAlignmentSetting
{
    Left = 0,
    Centre = 1,
}

public sealed record OutputSettings
{
    /// <summary>MonitorInfo.StableId of the presenter's display; null = auto (first non-primary).</summary>
    public string? MonitorStableId { get; init; }

    /// <summary>Last output display size, used for layout and the preview when no display is connected.</summary>
    public int LastWidthPx { get; init; } = 1920;
    public int LastHeightPx { get; init; } = 1080;
    public double LastDpiScale { get; init; } = 1.0;
    public double LastRefreshHz { get; init; } = 60.0;

    /// <summary>Reading line position as a fraction of the text area height (0.10..0.60).</summary>
    public double ReadingLineFraction { get; init; } = 0.33;
    public bool ShowReadingMarker { get; init; } = true;
    public bool DimAboveReadingLine { get; init; }
    public bool ShowSectionNames { get; init; }
    public TextAlignmentSetting Alignment { get; init; } = TextAlignmentSetting.Left;

    [JsonIgnore]
    public OutputGeometry LastGeometry => new(LastWidthPx, LastHeightPx, LastDpiScale, LastRefreshHz);
}

public enum MarginEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

/// <summary>Margins as fractions: Left/Right of output width, Top/Bottom of output height (each 0..0.40).
/// Defaults are DESIGN.md's 160/160/64/64 px on 1920 x 1080.</summary>
public sealed record MarginSettings
{
    public double Left { get; init; } = 160.0 / 1920.0;
    public double Top { get; init; } = 64.0 / 1080.0;
    public double Right { get; init; } = 160.0 / 1920.0;
    public double Bottom { get; init; } = 64.0 / 1080.0;

    /// <summary>When true, editing any edge sets all four to the same pixel value (MarginMath).</summary>
    public bool IsLinked { get; init; }

    /// <summary>The edge last edited; turning the link on copies this edge's pixel value to all four.</summary>
    public MarginEdge LastEdited { get; init; } = MarginEdge.Left;
}

public sealed record TextSettings
{
    /// <summary>"Poppins" (bundled), "Segoe UI", "Bahnschrift", "Arial", or any installed family name.</summary>
    public string FontFace { get; init; } = "Poppins";

    /// <summary>OpenType weight of Normal text: 400 or 500. Bold renders 700.</summary>
    public int FontWeight { get; init; } = 500;

    /// <summary>Normal-size lines in the text area (4..16).</summary>
    public double LinesVisible { get; init; } = 8;

    /// <summary>Line height as a multiple of font size (1.0..2.0).</summary>
    public double LineSpacing { get; init; } = 1.3;

    /// <summary>Space after each paragraph, in lines.</summary>
    public double ParagraphGapLines { get; init; } = 0.5;

    /// <summary>Default ink token for runs without an explicit colour.</summary>
    public string DefaultInk { get; init; } = "ink-white";
}

public enum Acceleration
{
    Gentle = 0,   // ramp-change 600 ms
    Normal = 1,   // ramp-change 300 ms
    Quick = 2,    // ramp-change 150 ms
}

/// <summary>Maps UI speed units to lines per minute:
/// lpm = sign(s) * LinesPerMinuteAtMax * (|s| / MaxSpeed)^Exponent. Defaults put 4.5 at about 28 lpm (~140 wpm at 5 words/line).</summary>
public sealed record SpeedCurveSettings
{
    public double LinesPerMinuteAtMax { get; init; } = 80.0;
    public double Exponent { get; init; } = 1.3;
}

public sealed record ScrollSettings
{
    public ScrollMode DefaultMode { get; init; } = ScrollMode.Display;
    public double StartSpeed { get; init; } = 4.5;
    public double SpeedStep { get; init; } = 0.5;
    public double FineSpeedStep { get; init; } = 0.1;
    public double MinSpeed { get; init; } = -10.0;
    public double MaxSpeed { get; init; } = 10.0;
    public Acceleration Acceleration { get; init; } = Acceleration.Normal;
    public bool PauseAtSections { get; init; }
    public SpeedCurveSettings Curve { get; init; } = new();
}

public sealed record WheelSettings
{
    /// <summary>Raw Input device path of a dedicated prompter mouse; null = wheel over the preview only.</summary>
    public string? DedicatedDeviceId { get; init; }
    public RelativeBinding Wheel { get; init; } = DefaultBindings.Wheel;
    public IReadOnlyDictionary<string, BindingAction> Buttons { get; init; } = DefaultBindings.WheelButtons;
}

public sealed record XboxSettings
{
    /// <summary>XInput user index 0..3, or -1 = first connected pad.</summary>
    public int UserIndex { get; init; } = -1;
    public IReadOnlyDictionary<string, AxisBinding> Axes { get; init; } = DefaultBindings.XboxAxes;
    public IReadOnlyDictionary<string, BindingAction> Buttons { get; init; } = DefaultBindings.XboxButtons;
}

public sealed record ShuttleSettings
{
    /// <summary>HID device path; null = first Contour device found.</summary>
    public string? DeviceId { get; init; }
    public IReadOnlyDictionary<string, AxisBinding> Axes { get; init; } = DefaultBindings.ShuttleAxes;
    public RelativeBinding Jog { get; init; } = DefaultBindings.Jog;
    public IReadOnlyDictionary<string, BindingAction> Buttons { get; init; } = DefaultBindings.ShuttleButtons;
}

public sealed record KeyboardSettings
{
    /// <summary>Work in both key modes (typing and transport).</summary>
    public IReadOnlyDictionary<string, BindingAction> Global { get; init; } = DefaultBindings.KeyboardGlobal;

    /// <summary>Work only while the keys are with the transport.</summary>
    public IReadOnlyDictionary<string, BindingAction> Transport { get; init; } = DefaultBindings.KeyboardTransport;
}

public sealed record InputSettings
{
    /// <summary>The one active controller (Settings › Controller cards). Keyboard is always on in addition.</summary>
    public ControllerKind SelectedController { get; init; } = ControllerKind.MouseWheel;
    public WheelSettings Wheel { get; init; } = new();
    public XboxSettings Xbox { get; init; } = new();
    public ShuttleSettings Shuttle { get; init; } = new();
    public KeyboardSettings Keyboard { get; init; } = new();
}

public sealed record EditorSettings
{
    /// <summary>Paragraph prefix that marks a section ("## Name").</summary>
    public string SectionMarker { get; init; } = "##";

    /// <summary>Editor Normal font size in DIP at 100 % zoom (DESIGN.md `editor`: 20).</summary>
    public double BaseFontSize { get; init; } = 20.0;

    public int ZoomPercent { get; init; } = 100;

    /// <summary>Seconds between recovery copies of an unsaved script (0 = off).</summary>
    public int RecoverySeconds { get; init; } = 60;
}

public sealed record WindowSettings
{
    public double Left { get; init; } = double.NaN;
    public double Top { get; init; } = double.NaN;
    public double Width { get; init; } = 1280;
    public double Height { get; init; } = 800;
    public bool IsMaximized { get; init; } = true;
}

public sealed record SettingsChangedEventArgs(PrompterSettings Old, PrompterSettings New);

public interface ISettingsService
{
    PrompterSettings Current { get; }

    /// <summary>Full path of settings.json.</summary>
    string SettingsPath { get; }

    /// <summary>Load from disk (sync, call once at startup). Missing file = defaults. Unreadable or invalid file:
    /// rename to settings.json.bad-yyyyMMddHHmmss, use defaults, log a warning. Newer SchemaVersion: defaults
    /// for unknown fields, keep the file until the next save.</summary>
    void Load();

    /// <summary>Atomically replace Current (thread-safe, under a lock), raise Changed on the calling thread,
    /// and schedule a debounced save (500 ms). No-op if the result is reference-equal to Current.</summary>
    void Update(Func<PrompterSettings, PrompterSettings> change);

    /// <summary>Write now: serialise to settings.json.tmp, then File.Replace / File.Move over settings.json.</summary>
    Task SaveAsync();

    event EventHandler<SettingsChangedEventArgs>? Changed;
}

/// <summary>Shared JSON options for settings and .ilup files.</summary>
public static class IluJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }
}
```

`JsonSettingsService` (stream A) obligations:
- Atomic save: write `settings.json.tmp`, then `File.Replace(tmp, path, path + ".bak")` (or `File.Move(tmp, path)` when
  there is no file yet).
- Debounce: 500 ms after the last `Update`. `SaveAsync` on exit flushes.
- Corrupt file → `settings.json.bad-yyyyMMddHHmmss`, defaults, `Warn` log.
- Unknown JSON fields are ignored and missing fields take defaults (default `init` values).
- `Update` is thread-safe, and `Changed` is raised outside the lock.

### 2.9 Core: diagnostics

`src/IluPrompter.Core/Contracts/Diagnostics.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>
/// Minimal thread-safe log. File: %LOCALAPPDATA%\ILU Prompter\logs\ilu-prompter-yyyyMMdd.log.
/// Never logs script text or file contents beyond the file name.
/// </summary>
public interface IAppLog
{
    /// <summary>True when started with --diag: enables frame-timing and input trace logs.</summary>
    bool DiagnosticsEnabled { get; }

    string LogDirectory { get; }

    void Info(string area, string message);

    void Warn(string area, string message);

    void Error(string area, string message, Exception? exception = null);

    /// <summary>Only written when DiagnosticsEnabled.</summary>
    void Trace(string area, string message);
}
```

### 2.10 Core: frozen helper signatures (bodies owned by stream A)

`DefaultBindings` (complete in the scaffold, from DESIGN.md's tables) and the layout maths. The scaffold ships naive
**non-throwing** bodies marked `// STUB`. Stream A replaces the bodies, keeping every public signature.

`src/IluPrompter.Core/Layout/LayoutMath.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>Margins in pixels of the output display (what the margin editor shows).</summary>
public readonly record struct MarginPixels(double Left, double Top, double Right, double Bottom);

/// <summary>The text area on the output in DIPs (relative to the output window's top-left).</summary>
public readonly record struct TextArea(double Left, double Top, double Width, double Height, double ReadingLineY, double BaseFontSize, double BaseLineHeight)
{
    public ViewportMetrics ToViewport() => new(Width, Height, ReadingLineY);
}

/// <summary>Margin conversion and link behaviour. Signatures frozen; stream A owns the bodies and tests.</summary>
public static class MarginMath
{
    public const double MaxFraction = 0.40;

    public static MarginPixels ToPixels(MarginSettings m, PixelSize output) =>
        new(m.Left * output.Width, m.Top * output.Height, m.Right * output.Width, m.Bottom * output.Height);

    /// <summary>Set one edge in output pixels. Clamps to 0..MaxFraction of its dimension. When m.IsLinked, all four
    /// edges get the same pixel value (each clamped in its own dimension). Records LastEdited.</summary>
    public static MarginSettings SetEdgePixels(MarginSettings m, MarginEdge edge, double pixels, PixelSize output) =>
        edge switch // STUB (stream A replaces): no clamping, ignores IsLinked
        {
            MarginEdge.Left => m with { Left = pixels / output.Width, LastEdited = edge },
            MarginEdge.Right => m with { Right = pixels / output.Width, LastEdited = edge },
            MarginEdge.Top => m with { Top = pixels / output.Height, LastEdited = edge },
            _ => m with { Bottom = pixels / output.Height, LastEdited = edge },
        };

    /// <summary>Turn the link on or off. Turning it on copies LastEdited's pixel value to all four edges.</summary>
    public static MarginSettings SetLinked(MarginSettings m, bool linked, PixelSize output) =>
        m with { IsLinked = linked }; // STUB (stream A replaces): does not equalise edges
}

/// <summary>Font size from "lines visible" and the text area. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TypographyMath
{
    /// <summary>size = textAreaHeight / (linesVisible * lineSpacing). (1080-64-64)/(8*1.3) = 91.54.</summary>
    public static double BaseFontSize(double textAreaHeightDip, double linesVisible, double lineSpacing) =>
        textAreaHeightDip / (linesVisible * lineSpacing);

    /// <summary>Compute the text area for an output display. Margins are fractions of the display; DIPs = px / DpiScale.</summary>
    public static TextArea ComputeTextArea(OutputGeometry output, MarginSettings margins, TextSettings text, OutputSettings outputSettings)
    {
        var px = MarginMath.ToPixels(margins, output.SizePx);
        var left = px.Left / output.DpiScale;
        var top = px.Top / output.DpiScale;
        var width = Math.Max(1, output.WidthDip - left - (px.Right / output.DpiScale));
        var height = Math.Max(1, output.HeightDip - top - (px.Bottom / output.DpiScale));
        var size = BaseFontSize(height, text.LinesVisible, text.LineSpacing);
        return new TextArea(left, top, width, height, height * outputSettings.ReadingLineFraction, size, size * text.LineSpacing);
    }
}

/// <summary>Speed units to lines per minute and DIPs per second. Signatures frozen; stream A owns bodies and tests.</summary>
public static class SpeedCurve
{
    public static double ToLinesPerMinute(double speed, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        Math.Sign(speed) * curve.LinesPerMinuteAtMax * Math.Pow(Math.Min(Math.Abs(speed), maxSpeed) / maxSpeed, curve.Exponent);

    public static double FromLinesPerMinute(double linesPerMinute, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        Math.Sign(linesPerMinute) * maxSpeed * Math.Pow(Math.Min(Math.Abs(linesPerMinute) / curve.LinesPerMinuteAtMax, 1.0), 1.0 / curve.Exponent);

    public static double ToDipsPerSecond(double speed, double baseLineHeightDip, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        ToLinesPerMinute(speed, curve, maxSpeed) / 60.0 * baseLineHeightDip;
}

/// <summary>Analog axis to speed units. Signature frozen; stream A owns body and tests.</summary>
public static class AxisCurve
{
    /// <summary>Returns speed units for a normalised axis value per the binding's role, deadzone, exponent, max and invert.
    /// Triggers (0..1) with SpeedReverse return negative speed.</summary>
    public static double Apply(double value, AxisBinding binding) =>
        binding.Role switch // STUB (stream A replaces): linear, no deadzone/exponent/invert
        {
            AxisRole.Speed or AxisRole.SpeedForward => value * binding.MaxSpeed,
            AxisRole.SpeedReverse => -value * binding.MaxSpeed,
            _ => 0.0,
        };
}

/// <summary>Durations and reading pace. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TimeEstimator
{
    public static double SecondsForLines(int lines, double speed, SpeedCurveSettings curve)
    {
        var lpm = Math.Abs(SpeedCurve.ToLinesPerMinute(speed, curve));
        return lpm < 1e-6 ? double.PositiveInfinity : lines * 60.0 / lpm;
    }

    /// <summary>Estimated duration of each section (lines from its marker to the next marker or END) at the given speed.</summary>
    public static IReadOnlyList<TimeSpan> SectionDurations(LayoutGeometry geometry, double speed, SpeedCurveSettings curve) =>
        geometry.Sections.Select(_ => TimeSpan.Zero).ToArray(); // STUB (stream A replaces)

    public static double WordsPerMinute(LayoutGeometry geometry, double linesPerMinute) =>
        geometry.LineCount == 0 ? 0 : Math.Abs(linesPerMinute) * geometry.WordCount / geometry.LineCount;

    /// <summary>House format: "6m:42s" under an hour, "1h:05m" from an hour; "--" for infinity/NaN.</summary>
    public static string Format(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes}m:{duration.Seconds:00}s"; // STUB (stream A replaces): no hours, no infinity
}

/// <summary>DESIGN.md Motion tokens (seconds). Ramps are linear in velocity; steps are cubic ease-out in position.</summary>
public static class MotionTimings
{
    public const double RampStart = 0.400;
    public const double RampStop = 0.200;
    public const double LineStep = 0.220;
    public const double ParagraphStep = 0.320;
    public const double FailSafeStop = 0.300;

    public static double RampChange(Acceleration a) => a switch
    {
        Acceleration.Gentle => 0.600,
        Acceleration.Quick => 0.150,
        _ => 0.300,
    };
}
```

`src/IluPrompter.Core/Settings/DefaultBindings.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>Factory defaults from DESIGN.md "Keyboard shortcuts" and "Controller defaults". Owned by stream A.</summary>
public static class DefaultBindings
{
    private static BindingAction Act(PrompterCommand display, PrompterCommand? line = null) => new(display, line);

    public static RelativeBinding Wheel { get; } =
        new(RelativeRole.Speed, RelativeRole.Lines, SpeedPerUnit: 0.5, LinesPerUnit: 1.0);

    public static IReadOnlyDictionary<string, BindingAction> WheelButtons { get; } = new Dictionary<string, BindingAction>
    {
        [ControlIds.MiddleButton] = Act(PrompterCommand.PlayPause),
    };

    public static IReadOnlyDictionary<string, AxisBinding> XboxAxes { get; } = new Dictionary<string, AxisBinding>
    {
        [ControlIds.RightTrigger] = new(AxisRole.SpeedForward, Deadzone: 0.12, Exponent: 2.0, MaxSpeed: 10.0),
        [ControlIds.LeftTrigger] = new(AxisRole.SpeedReverse, Deadzone: 0.12, Exponent: 2.0, MaxSpeed: 10.0),
        [ControlIds.LeftThumbY] = new(AxisRole.None, Deadzone: 0.24, Exponent: 2.5, MaxSpeed: 10.0),
    };

    public static IReadOnlyDictionary<string, BindingAction> XboxButtons { get; } = new Dictionary<string, BindingAction>
    {
        [ControlIds.A] = Act(PrompterCommand.PlayPause),
        [ControlIds.DPadUp] = Act(PrompterCommand.LinePrevious),
        [ControlIds.DPadDown] = Act(PrompterCommand.LineNext),
        [ControlIds.LeftShoulder] = Act(PrompterCommand.ParagraphPrevious),
        [ControlIds.RightShoulder] = Act(PrompterCommand.ParagraphNext),
        [ControlIds.DPadLeft] = Act(PrompterCommand.SectionPrevious),
        [ControlIds.DPadRight] = Act(PrompterCommand.SectionNext),
        [ControlIds.View] = Act(PrompterCommand.ToggleBlank),
    };

    /// <summary>Ring value is k/7 for k in -7..+7; deadzone 0 keeps position 1 usable.</summary>
    public static IReadOnlyDictionary<string, AxisBinding> ShuttleAxes { get; } = new Dictionary<string, AxisBinding>
    {
        [ControlIds.ShuttleRing] = new(AxisRole.Speed, Deadzone: 0.0, Exponent: 1.5, MaxSpeed: 10.0),
    };

    public static RelativeBinding Jog { get; } =
        new(RelativeRole.Lines, RelativeRole.Lines, LinesPerUnit: 1.0);

    /// <summary>Logical buttons numbered left to right, top to bottom (the provider normalises per model).
    /// ShuttleXpress has 5; ShuttlePRO v2 has 15.</summary>
    public static IReadOnlyDictionary<string, BindingAction> ShuttleButtons { get; } = new Dictionary<string, BindingAction>
    {
        [ControlIds.ShuttleButton(1)] = Act(PrompterCommand.SectionPrevious),
        [ControlIds.ShuttleButton(2)] = Act(PrompterCommand.ParagraphPrevious),
        [ControlIds.ShuttleButton(3)] = Act(PrompterCommand.PlayPause),
        [ControlIds.ShuttleButton(4)] = Act(PrompterCommand.ParagraphNext),
        [ControlIds.ShuttleButton(5)] = Act(PrompterCommand.SectionNext),
        [ControlIds.ShuttleButton(6)] = Act(PrompterCommand.ToggleBlank),
        [ControlIds.ShuttleButton(7)] = Act(PrompterCommand.JumpToStart),
        [ControlIds.ShuttleButton(8)] = Act(PrompterCommand.ToggleScrollMode),
    };

    public static IReadOnlyDictionary<string, BindingAction> KeyboardGlobal { get; } = new Dictionary<string, BindingAction>
    {
        ["F5"] = Act(PrompterCommand.PlayPause),
        ["F8"] = Act(PrompterCommand.ToggleBlank),
        ["F11"] = Act(PrompterCommand.ToggleOutput),
    };

    public static IReadOnlyDictionary<string, BindingAction> KeyboardTransport { get; } = new Dictionary<string, BindingAction>
    {
        ["Space"] = Act(PrompterCommand.PlayPause),
        ["Escape"] = Act(PrompterCommand.Pause),
        ["Down"] = Act(PrompterCommand.SpeedUp, PrompterCommand.LineNext),
        ["Up"] = Act(PrompterCommand.SpeedDown, PrompterCommand.LinePrevious),
        ["Shift+Down"] = Act(PrompterCommand.SpeedUpFine, PrompterCommand.LineNext),
        ["Shift+Up"] = Act(PrompterCommand.SpeedDownFine, PrompterCommand.LinePrevious),
        ["Right"] = Act(PrompterCommand.LineNext),
        ["Left"] = Act(PrompterCommand.LinePrevious),
        ["PageDown"] = Act(PrompterCommand.ParagraphNext),
        ["PageUp"] = Act(PrompterCommand.ParagraphPrevious),
        ["Ctrl+Down"] = Act(PrompterCommand.SectionNext),
        ["Ctrl+Up"] = Act(PrompterCommand.SectionPrevious),
        ["Home"] = Act(PrompterCommand.JumpToStart),
        ["B"] = Act(PrompterCommand.ToggleBlank),
        ["M"] = Act(PrompterCommand.ToggleScrollMode),
        ["D0"] = Act(PrompterCommand.SpeedReset, PrompterCommand.None),
        ["NumPad0"] = Act(PrompterCommand.SpeedReset, PrompterCommand.None),
    };
}
```

`MarginMath` rules: values are output pixels. Each edge is clamped to `0…MaxFraction` of its own dimension, and the text
area must keep ≥ 20 % of width and height. `SetEdgePixels` when linked sets all four to the same px (each clamped).
`SetLinked(true)` copies `LastEdited`'s px to all four. Results are rounded to whole px before converting to fractions.

### 2.11 Documents contracts (stream E implements)

`src/IluPrompter.Documents/Contracts/DocumentContracts.cs`

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>The native .ilup file (UTF-8 JSON via IluJson.Options).</summary>
public sealed record ScriptFile
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public string Title { get; init; } = "Untitled";

    /// <summary>TextRange.Save(DataFormats.Xaml) of the editor document. Loaded ONLY via TextRange.Load,
    /// never XamlReader.Load (untrusted input).</summary>
    public string DocumentXaml { get; init; } = "";

    /// <summary>Editor Normal size when saved; run sizes are relative to it (Small 0.8, Normal 1, Large 1.25, Huge 1.5).</summary>
    public double EditorBaseFontSize { get; init; } = 20.0;

    public ScriptOverrides? Overrides { get; init; }
}

/// <summary>Optional per-script layout that overrides app settings when the script is opened (all nullable).</summary>
public sealed record ScriptOverrides
{
    public double? LinesVisible { get; init; }
    public double? LineSpacing { get; init; }
    public MarginSettings? Margins { get; init; }
    public double? StartSpeed { get; init; }
}

public sealed class ScriptFileException : Exception
{
    public ScriptFileException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Open/save/import/export. All members require an STA thread with a Dispatcher (they use FlowDocument
/// and TextRange); call them on the control UI thread.
/// </summary>
public interface IScriptFileService
{
    /// <summary>".ilup", ".rtf", ".txt", ".docx" (lower case, with dot).</summary>
    IReadOnlyList<string> OpenExtensions { get; }

    /// <summary>".rtf", ".txt".</summary>
    IReadOnlyList<string> ExportExtensions { get; }

    /// <summary>Load any supported file. Imports (rtf/txt/docx) are converted to a flattened, canonical
    /// FlowDocument XAML with Heading-styled paragraphs turned into "## Name" section paragraphs.
    /// Throws ScriptFileException with a user-presentable message on failure.</summary>
    ScriptFile Load(string path);

    /// <summary>Save .ilup atomically (write .tmp then replace). Throws ScriptFileException.</summary>
    void Save(string path, ScriptFile file);

    /// <summary>Export the editor document to .rtf or .txt (by extension). Throws ScriptFileException.</summary>
    void Export(string path, FlowDocument document);
}

/// <summary>Canonical paragraph order and anchors. Call on the thread that owns the document.</summary>
public interface IDocumentNavigator
{
    /// <summary>Every Paragraph in document order, descending into List/ListItem/Section/Table/TableCell.
    /// BlockUIContainers and paragraphs tagged as generated (END marker) are skipped.</summary>
    IReadOnlyList<Paragraph> GetParagraphs(FlowDocument document);

    /// <summary>Plain text of a paragraph (TextRange.Text without the trailing newline).</summary>
    string GetText(Paragraph paragraph);

    DocumentAnchor GetAnchor(FlowDocument document, TextPointer pointer);

    /// <summary>Clamps to the nearest valid paragraph/offset.</summary>
    TextPointer GetPointer(FlowDocument document, DocumentAnchor anchor);
}

/// <summary>Turns the live editor document into an immutable snapshot and back. Control UI thread.</summary>
public interface ISnapshotBuilder
{
    /// <summary>Flatten to canonical paragraphs, save as XAML, parse sections, count words.</summary>
    PromptDocumentSnapshot Build(FlowDocument editorDocument, long version, double editorBaseFontSize, string sectionMarker);

    ScriptFile ToScriptFile(FlowDocument editorDocument, string title, double editorBaseFontSize, ScriptOverrides? overrides);

    /// <summary>Replace the content of target with documentXaml via TextRange.Load(DataFormats.Xaml).
    /// Empty string = one empty paragraph.</summary>
    void LoadInto(FlowDocument target, string documentXaml);
}

/// <summary>Section-marker parsing ("## Name"). Pure.</summary>
public interface ISectionParser
{
    /// <summary>True if the text (after leading whitespace) starts with the marker. Name = the rest with
    /// leading '#' and whitespace trimmed; empty name becomes "" (caller numbers it "Section n").</summary>
    bool TryParse(string paragraphText, string marker, out string name);

    IReadOnlyList<SectionInfo> Parse(IReadOnlyList<string> paragraphTexts, string marker);
}

/// <summary>Everything the output document needs, already resolved to WPF values by the renderer.
/// Brushes/colours must be usable on the calling thread (frozen or created there).</summary>
public sealed record OutputTextStyle(
    FontFamily FontFamily,
    FontWeight FontWeight,
    double BaseFontSize,
    double LineSpacing,
    double ParagraphGapLines,
    TextAlignment Alignment,
    Color Ink,
    Color SectionInk,
    Color EndInk,
    Color EndRule,
    bool ShowSectionNames,
    string SectionMarker,
    IReadOnlyDictionary<Color, Color> HighlightInks);

/// <summary>Builds the read-only output/preview FlowDocument from a snapshot.</summary>
public interface IOutputDocumentFactory
{
    /// <summary>
    /// Call on the thread that will own and display the document. Rules:
    /// PagePadding 0; FontFamily/FontWeight/Foreground/LineHeight (BlockLineHeight) from style;
    /// every run FontSize scaled by style.BaseFontSize / snapshot.EditorBaseFontSize; paragraph Margin bottom
    /// = ParagraphGapLines * BaseFontSize * LineSpacing; text inside a known highlight takes HighlightInks[bg];
    /// section paragraphs are kept (1:1 paragraph indices) but restyled: ShowSectionNames ? 0.4 x size,
    /// SectionInk, capitals, marker stripped : text hidden, height 0.5 line; then a generated END block
    /// (blank line + centred "END" at 0.6 x size in EndInk with 2 px EndRule rules at 30 % width), tagged
    /// so IDocumentNavigator skips it.
    /// </summary>
    FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style);
}

public sealed record GeometryBuildOptions(string LayoutKey, PromptDocumentSnapshot Snapshot, double BaseLineHeight);

/// <summary>Builds line/paragraph/section tables from a laid-out output document.</summary>
public interface ILayoutGeometryBuilder
{
    /// <summary>
    /// Precondition: the document is hosted (read-only RichTextBox or FlowDocumentScrollViewer, no scrolling,
    /// measured with unconstrained height) and layout has completed at the output text width (host.UpdateLayout()),
    /// with PagePadding 0 so y = 0 is the top of the first block (TextPointer.GetCharacterRect coordinates).
    /// Enumerates visual lines via TextPointer.GetLineStartPosition, records tops/heights/paragraph/char offsets,
    /// paragraph tops (canonical order), section tops and the END block top. O(lines). Owner thread only.
    /// </summary>
    LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options);
}

/// <summary>Relative script sizes (DESIGN.md: the editor size menu offers only these).</summary>
public enum ScriptSize
{
    Small = 0,   // 0.80
    Normal = 1,  // 1.00
    Large = 2,   // 1.25
    Huge = 3,    // 1.50
}

/// <summary>Formatting helpers for the editor toolbar. Control UI thread. Bold/italic/underline use the
/// built-in EditingCommands; these cover what WPF lacks.</summary>
public interface IScriptFormatter
{
    double Factor(ScriptSize size);

    void ApplySize(TextRange range, ScriptSize size, double editorBaseFontSize);

    /// <summary>Null when the range mixes sizes.</summary>
    ScriptSize? GetSize(TextRange range, double editorBaseFontSize);

    void ToggleStrikethrough(TextRange range);

    /// <summary>Null when mixed.</summary>
    bool? GetStrikethrough(TextRange range);

    /// <summary>ink null = remove explicit colour (falls back to the default ink).</summary>
    void ApplyInk(TextRange range, Color? ink);

    /// <summary>highlight null = remove. A highlight carries its own ink (DESIGN.md): highlightInk is applied too.</summary>
    void ApplyHighlight(TextRange range, Color? highlight, Color? highlightInk);

    /// <summary>Remove size, ink, highlight, bold, italic, underline, strikethrough.</summary>
    void ClearFormatting(TextRange range);

    /// <summary>Insert a new paragraph "marker name" before the caret's paragraph (or split at the caret)
    /// and return it.</summary>
    Paragraph InsertSectionMarker(RichTextBox editor, string marker, string name);

    /// <summary>Editor-only look for section paragraphs (muted label + bottom rule). Paragraph-level properties
    /// only; the snapshot builder and output factory ignore/override them.</summary>
    void StyleSectionParagraphs(FlowDocument document, string marker, Brush labelBrush, Brush ruleBrush);
}
```

### 2.12 Input contract (stream B implements, stream D calls)

`src/IluPrompter.Input/Contracts/IAppInputBridge.cs`

```csharp
using System.Windows;

namespace IluPrompter.Input;

/// <summary>
/// In-window input only (no hooks, no global keyboard capture). Converts WPF keyboard and preview-wheel
/// events into Core InputEvents and submits them to IInputSink on the UI thread.
/// </summary>
public interface IAppInputBridge
{
    /// <summary>Handle PreviewKeyDown/PreviewKeyUp at window level. Each key becomes a ButtonInput with
    /// Source (Keyboard, "app") and ControlId = ControlIds.KeyChord(...); e.Handled = sink.Submit(...).
    /// Ignores pure modifier keys and the Windows key. When keyboard focus is in a TextBoxBase, PasswordBox
    /// or editable ComboBox, the event is submitted as Typing context (only Global bindings apply) even if
    /// IKeyModeState.Mode is Transport.</summary>
    void AttachWindow(Window window);

    void DetachWindow(Window window);

    /// <summary>Mouse wheel and buttons over this element (the preview) become input from (MouseWheel, "app"):
    /// RelativeInput Wheel (Delta / 120) and ButtonInput Middle/XButton1/XButton2. e.Handled = sink.Submit(...).</summary>
    void AttachWheelArea(UIElement element);

    void DetachWheelArea(UIElement element);
}
```

### 2.13 Rendering public surface (stream C implements, stream D places)

The public members of `PrompterPreview` and `PreviewServices` below are frozen. Stream C replaces the bodies and adds
internal types freely. `OutputHost` and `MonitorService` are public classes whose **constructor signatures** are frozen
(DI). Their behaviour is defined by `IOutputSurface` / `IMonitorService`.

`src/IluPrompter.Rendering/Preview/PrompterPreview.cs`

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>Everything the preview needs; registered by AddIluRendering, resolved by the shell.</summary>
public sealed record PreviewServices(
    IScrollEngine Engine,
    IPromptDocumentService Documents,
    ISettingsService Settings,
    IOutputSurface Output,
    IOutputDocumentFactory DocumentFactory,
    ILayoutGeometryBuilder GeometryBuilder,
    IAppLog Log);

/// <summary>
/// The live preview (DESIGN.md D2): the output laid out at the output's logical size, scaled, letterboxed
/// in `backdrop`, with preview-only overlays (margin tint, reading line, BLANK dimming) and margin /
/// reading-line drag handles. The shell places it in XAML and calls Attach once.
/// Public surface frozen; stream C owns the implementation.
/// STUB: black box that ticks the engine when the output is not rendering.
/// </summary>
public sealed class PrompterPreview : Control
{
    private PreviewServices? _services;

    public PrompterPreview()
    {
        Background = Brushes.Black;
        Focusable = true;
    }

    /// <summary>True while the pointer is over the scaled script (the shell shows "Wheel drives the prompt here").</summary>
    public static readonly DependencyProperty IsPointerOverScriptProperty = DependencyProperty.Register(
        nameof(IsPointerOverScript), typeof(bool), typeof(PrompterPreview), new PropertyMetadata(false));

    public bool IsPointerOverScript
    {
        get => (bool)GetValue(IsPointerOverScriptProperty);
        private set => SetValue(IsPointerOverScriptProperty, value);
    }

    public void Attach(PreviewServices services)
    {
        _services = services;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Detach()
    {
        CompositionTarget.Rendering -= OnRendering;
        _services = null;
    }

    protected override void OnRender(DrawingContext drawingContext) =>
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

    private void OnRendering(object? sender, EventArgs e) => _services?.Engine.TickIfStale(Stopwatch.GetTimestamp());
}
```

### 2.14 Theme API (stream D owns; C and E consume)

D may **add** members but must not change or remove these. `IluTokens.xaml` is regenerated from DESIGN.md front matter
(§5.4), never hand-edited for values.

`src/IluPrompter.Theme/ThemeResources.cs`

```csharp
using System.Windows;
using System.Windows.Media;

namespace IluPrompter.Theme;

/// <summary>A script ink or highlight offered in the editor menus (DESIGN.md "Script colours").</summary>
public sealed record ScriptColourOption(string Token, string Name, string? InkToken = null);

/// <summary>
/// Thread-safe access to design tokens from code (any WPF thread, including the output thread).
/// Colours are read once from Themes/IluTokens.xaml into an immutable map; no hex literals in code.
/// Complete in the scaffold; stream D owns later changes.
/// </summary>
public static class ThemeResources
{
    // Must stay first: touching PackUriHelper registers the pack:// scheme, which is otherwise only
    // registered once a WPF Application exists (unit tests, early startup).
    private static readonly string PackScheme = System.IO.Packaging.PackUriHelper.UriSchemePack;

    public static readonly Uri TokensUri = new("pack://application:,,,/IluPrompter.Theme;component/Themes/IluTokens.xaml", UriKind.Absolute);
    public static readonly Uri ControlsUri = new("pack://application:,,,/IluPrompter.Theme;component/Themes/IluControls.xaml", UriKind.Absolute);
    public static readonly Uri LogoUri = new("pack://application:,,,/IluPrompter.Theme;component/Images/wall-logo.png", UriKind.Absolute);

    private const string BundledFontBase = "pack://application:,,,/IluPrompter.Theme;component/Fonts/";
    private static readonly object Gate = new();
    private static IReadOnlyDictionary<string, Color>? _colors;

    /// <summary>Menu order and names from DESIGN.md.</summary>
    public static IReadOnlyList<ScriptColourOption> Inks { get; } =
    [
        new("ink-white", "White"), new("ink-yellow", "Yellow"), new("ink-amber", "Amber"), new("ink-green", "Green"),
        new("ink-cyan", "Cyan"), new("ink-pink", "Pink"), new("ink-red", "Red"), new("ink-grey", "Grey"),
    ];

    /// <summary>Each highlight carries its own ink (InkToken).</summary>
    public static IReadOnlyList<ScriptColourOption> Highlights { get; } =
    [
        new("highlight-yellow", "Yellow", "canvas"), new("highlight-blue", "Blue", "ink-white"),
        new("highlight-green", "Green", "ink-white"), new("highlight-magenta", "Magenta", "ink-white"),
        new("highlight-grey", "Grey", "ink-white"),
    ];

    public static FontFamily UiFont { get; } = new(new Uri(BundledFontBase), "./#Poppins, Segoe UI");
    public static FontFamily MonoFont { get; } = new(new Uri(BundledFontBase), "./#DM Mono, Cascadia Mono, Consolas");

    /// <summary>"Poppins" resolves to the bundled family; anything else to the installed family by name.</summary>
    public static FontFamily ScriptFont(string face) =>
        string.Equals(face, "Poppins", StringComparison.OrdinalIgnoreCase) ? UiFont : new FontFamily(face + ", Segoe UI");

    /// <summary>Token name ("ink-white", "script-paper") or key ("Ilu.Color.InkWhite").</summary>
    public static Color GetColor(string token) =>
        TryGetColor(token, out var c) ? c : throw new KeyNotFoundException("Unknown colour token: " + token);

    public static bool TryGetColor(string token, out Color color)
    {
        var map = Colors();
        var key = token.StartsWith("Ilu.Color.", StringComparison.Ordinal) ? token : "Ilu.Color." + ToPascal(token);
        return map.TryGetValue(key, out color);
    }

    /// <summary>Frozen brush for a token, created on the calling thread.</summary>
    public static SolidColorBrush GetBrush(string token)
    {
        var b = new SolidColorBrush(GetColor(token));
        b.Freeze();
        return b;
    }

    private static IReadOnlyDictionary<string, Color> Colors()
    {
        var existing = Volatile.Read(ref _colors);
        if (existing is not null) return existing;
        lock (Gate)
        {
            if (_colors is not null) return _colors;
            // LoadComponent works with or without an Application instance (unit tests, output thread).
            var dictionary = (ResourceDictionary)Application.LoadComponent(
                new Uri("/IluPrompter.Theme;component/Themes/IluTokens.xaml", UriKind.Relative));
            var map = new Dictionary<string, Color>(StringComparer.Ordinal);
            foreach (var key in dictionary.Keys)
            {
                if (key is string s && dictionary[key] is Color c) map[s] = c;
            }

            Volatile.Write(ref _colors, map);
            return map;
        }
    }

    private static string ToPascal(string token) =>
        string.Concat(token.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
```

### 2.15 Composition and MVVM structure

- **DI.** `App.OnStartup` builds one `ServiceProvider`:
  `AddIluCore(diag).AddIluDocuments().AddIluInput().AddIluRendering().AddIluApp()`. Everything is a singleton. Each
  `AddIluXxx` lives in its own project and is owned by that stream, so streams replace stub registrations without
  touching shared files.
- **Startup order (frozen):** settings `Load()` → `IOutputSurface.InitializeAsync()` → resolve and `Show()` MainWindow
  (attach `IAppInputBridge` to the window and the preview, call `PrompterPreview.Attach`) → `IInputRouter.Start()` →
  open the last script. Shutdown: router `Stop()` → output `ShutdownAsync()` → settings `SaveAsync()`.
- **"Message bus".** There is deliberately no generic bus. Cross-module traffic is these typed channels, all on Core
  interfaces:

| Channel | Producer → consumer | Thread raised on | Consumer rule |
|---|---|---|---|
| `IPromptDocumentService.SnapshotPublished` | editor (D) → output host + preview (C) | UI thread | marshal to own Dispatcher, coalesce to newest version |
| `ISettingsService.Changed` | anyone → engine, router, C, D | caller's thread | marshal; compare sub-records by reference to skip no-ops |
| `IScrollEngine.Post(EngineCommand)` | router, D (slider, sections, gutter) → engine | any | lock-free enqueue |
| `IScrollEngine.ReadPosition()` / `Status` | engine → renderers (per frame) / UI (10 Hz poll) | any | lock-free reads |
| `IInputSink.Submit/ReportStatus` | providers, bridge (B) → router (A) | provider thread | router is thread-safe |
| `IInputRouter.DeviceStatusChanged/InputObserved/AppCommandRequested` | router → D | provider thread | marshal to UI |
| `IOutputSurface.StateChanged` | C → D (chips, preview frame) | output thread | marshal to UI |
| `IKeyModeState.Changed` | D → router, status bar | UI thread | — |

- **MVVM (App only).** CommunityToolkit.Mvvm `ObservableObject` + `[ObservableProperty]` (field-based) +
  `[RelayCommand]`. View models get Core services by constructor injection and never touch other threads' objects. The
  RichTextBox editor is code-behind plus an `EditorController` class: WPF's RichTextBox does not bind well, and that is
  accepted.

---

## 3. Threading model

```
 Control UI thread (STA #1, Application Dispatcher)          Output thread "ILU Output" (STA #2, own Dispatcher)
 ───────────────────────────────────────────────            ─────────────────────────────────────────────────────
 MainWindow, editor RichTextBox (live FlowDocument)          OutputWindow (borderless, topmost, no-activate)
 EditorController: TextChanged → 250 ms debounce               PrompterSurface #2: own FlowDocument from snapshot XAML
   → ISnapshotBuilder.Build → IPromptDocumentService.Publish     rebuild (coalesced) → layout → geometry → engine.SetLayout
 PrompterPreview: PrompterSurface #1 (own FlowDocument),      CompositionTarget.Rendering (per frame):
   rebuild on snapshot/settings → engine.SetLayout             dedupe RenderingTime → engine.Tick(now) → Y = −ReadPosition()
   output live: repaint on FramePulse ◄──────────────────────  FramePulse after each moved frame (+ 250 ms heartbeat)
   output not rendering: own Rendering → TickIfStale(now,     DisplaySleepGuard, FrameStats (--diag)
     0.001) → Y = −pos (preview is the only ticker)
 MainWindow polls engine.Status at 10 Hz (DispatcherTimer)
            │                                                            │
            └──────────── IScrollEngine (Core, lock + lock-free publish) ┘
                               ▲ Post(EngineCommand) from any thread
 Provider threads: "ILU RawInput" (STA, message-only HwndSource), "ILU XInput" (poll), "ILU Shuttle" (HID read loop)
   → IInputSink.Submit (InputRouter, thread-safe) → engine.Post / output.SetBlank / AppCommandRequested
 Render thread (WPF, one per process) composes both windows.
```

Rules:

1. **Thread affinity.** Every FlowDocument, Brush (unless frozen), Window and control belongs to the thread that created
   it. Only these cross threads: immutable records (`PromptDocumentSnapshot`, `LayoutGeometry`, `EngineStatus`,
   `PrompterSettings`, `OutputState`), the snapshot XAML string, `EngineCommand` structs and the published position.
   `ThemeResources` colours are structs; its `FontFamily` objects are not DispatcherObjects and are shared.
2. **Snapshot flow.** The editor publishes on the UI thread. `OutputHost` handles `SnapshotPublished` by storing the
   snapshot in a field (`Interlocked.Exchange`) and `BeginInvoke`-ing one rebuild at `DispatcherPriority.Background` on
   the output Dispatcher (skip if one is already pending). The rebuild reads the latest snapshot, so intermediate
   versions are dropped. The preview does the same on the UI Dispatcher. Settings and output-geometry changes trigger the
   same coalesced rebuild.
3. **Identical layout on both surfaces.** The output size for layout is always `IOutputSurface.State.Geometry` (the
   current display, or the last known one from settings, or 1920 × 1080). Both surfaces lay out at the same DIP width with `TextOptions.TextFormattingMode="Ideal"`,
   the same style and the same snapshot, so line breaks are identical. The preview scales with a `RenderTransform`
   `ScaleTransform(s)` (or `Viewbox`), never by laying out at a smaller width. Phase-0 check S7 verifies this.
4. **Who supplies geometry.** Both surfaces call `engine.SetLayout(geometry, viewport)` after each rebuild. The engine
   ignores a geometry whose `LayoutKey` equals the current one (viewport still updated). So the engine always has a
   geometry, whether or not the output is open.
5. **Who ticks.** (Amended at integration, 19 Oct 2026, from stream C's CONTRACT-REQUEST.) The output window's Rendering
   handler calls `engine.Tick(t)` once per *new* `RenderingEventArgs.RenderingTime` (mapped to Stopwatch ticks by
   `FrameClock`). Then:
   - **Output rendering** (`State.IsOpen && Display == Ready`): the preview has **no** `CompositionTarget.Rendering`
     subscription. `OutputHost` implements the internal `IFramePulse` and raises `FramePulse` on the output thread after
     each frame in which the position changed, plus a 250 ms heartbeat. The preview coalesces pulses into one
     `Dispatcher.BeginInvoke` that reads `ReadPosition()` and sets its transform. A 250 ms UI watchdog falls back to the
     Rendering mode below if pulses stop for 500 ms, and returns to pulses when they resume.
   - **Output not rendering** (closed, hidden, Missing, Duplicated, or an `IOutputSurface` without a pulse): the preview
     subscribes to Rendering and is the only ticker, calling `engine.TickIfStale(now, 0.001)` every frame (with the
     default 0.05 the engine would only advance at 20 Hz), then reads `ReadPosition()`.

   Why: measured on this laptop, two UI threads that each have a `Rendering` subscription both drop to 30 fps (each frame
   waits for the single WPF render thread twice); pulse-driven painting at up to 60 Hz leaves the output at 60 fps. The
   preview follows the output within one frame (DESIGN D2).
6. **Input threads** never touch WPF objects. They call the thread-safe router, which only posts engine commands,
   calls `IOutputSurface.SetBlank` (which marshals internally) or raises events for D to marshal.
7. **Shutdown.** `OutputHost.ShutdownAsync` closes the window via `Dispatcher.InvokeAsync`, calls
   `Dispatcher.InvokeShutdown()` and joins the thread (2 s timeout, then log). Provider threads are background threads,
   stopped by `Stop()`/`Dispose()`.
8. **GC and timers.** `GCSettings.LatencyMode = SustainedLowLatency` while Playing (set and restored by C). No
   `DispatcherTimer`, `Thread.Sleep` or `timeBeginPeriod` for motion. `DispatcherTimer` is fine for the 250 ms editor
   debounce and the 10 Hz status poll.
9. **Exceptions.** An exception on the output thread is caught in its Dispatcher's `UnhandledException`, logged, marked
   handled and the frame skipped. The output thread must never take the app down. Provider exceptions → `DeviceStatus`
   with `Detail`, logged.
10. **No per-frame UI-thread work in the control window while the output is live** (added at integration): no
    `CompositionTarget.Rendering` subscriptions (other than the preview's own fallback in rule 5, which is only active
    while the output is not rendering), no looping or continuous storyboards/animations (spinners, pulsing chips,
    marquees, indeterminate progress bars), no per-frame timers. Occasional updates are fine and were measured harmless:
    the 10 Hz status poll (a `DispatcherTimer` that only sets properties whose value changed), typing, caret blink.
    Checked at integration: stream D has no Rendering subscription, storyboard or animation; its only timers are the
    10 Hz poll, the 250 ms editor debounce and the recovery timer.
11. **Rebuilds are never on the per-frame path.** A rebuild (new snapshot or a layout setting) runs once per change at
    `DispatcherPriority.Background` on each surface's thread. On the output thread it blocks frames for its duration
    (about 100 to 150 ms for the 20-page sample on this laptop, mostly `TextRange.Load` and WPF layout; the geometry
    tables take about 10 ms), so an edit while LIVE costs a short hitch on the output once the 250 ms debounce expires.

---

## 4. Workstreams (five parallel worktrees)

Branches: `stream/a-core`, `stream/b-input`, `stream/c-rendering`, `stream/d-shell`, `stream/e-documents`, all from the
scaffold commit on `main`. **Zero file overlap**: each stream edits only the paths in its "Owns" list. Frozen files
(§7.1) are edited by nobody. Every stream may read everything. Each stream also writes `docs/streams/<X>.md` (its own
notes: decisions, how to test, known gaps), so the documentation is kept current as work proceeds.

Each stream's stubs compile against the others' stubs, so every worktree builds and runs from day one. Streams code
only against contracts, never against another stream's internals.

### 4.A Core: engine, speed curves, router, settings

- **Owns:**
  - `src/IluPrompter.Core/Engine/**`, `Input/InputRouter.cs`, `Layout/**`, `Settings/**`
  - `CoreServiceCollectionExtensions.cs`
  - `tests/IluPrompter.Core.Tests/**`, `docs/streams/A-core.md`
- **Implements:**
  - `ScrollEngine` (§2.4, every rule in the behaviour table)
  - `InputRouter` (§2.3 router rules)
  - `JsonSettingsService` (§2.8)
  - Bodies of `MarginMath`, `AxisCurve`, `TimeEstimator.SectionDurations/Format` (house format `6m:42s`, `1h:05m`, `--`
    for ∞/NaN)
  - Hardening of `SpeedCurve`/`TypographyMath` (guards for 0, NaN, negative)
- **Must not:** reference WPF/WinForms; change `Contracts/**`.
- **Acceptance:** all tests below pass. A 10-minute simulated run (60 Hz ticks, random commands) never produces NaN, never
  leaves [Pmin, Pmax] and allocates 0 bytes in steady state.
- **Tests (xUnit, deterministic, no real time):** drive `Tick` with synthetic timestamps (`Stopwatch.Frequency`
  multiples) and synthetic `LayoutGeometry` (e.g. 100 lines × 119.6 DIP).

| Area | Cases |
|---|---|
| Speed curve | 4.5 → 28.3 ± 0.3 lpm; monotonic; odd symmetry; `FromLinesPerMinute(ToLinesPerMinute(s)) ≈ s`; 0 → 0; clamps beyond ±10 |
| Axis curve | inside deadzone = 0; exponent shape at 0.5; invert; trigger forward/reverse signs; max at 1.0 |
| Margins | link-on copies LastEdited px; linked edit sets all four; clamp to 40 %; 20 % minimum text area; fractions survive a 1920→3840 resolution change (same %) |
| Typography | (1080−64−64)/(8×1.3) = 91.54; DPI 1.5 halves nothing (DIPs) |
| Time | `Format`: 402 s → `6m:42s`, 3900 s → `1h:05m`, ∞ → `--`; section durations sum ≈ total |
| Engine motion | play ramp reaches set speed at 0.4 s; distance over 10 s ≈ ∫v; pause ramp 0.2 s; speed change ramp per Acceleration; dt clamp after 1 s stall; duplicate timestamp ignored |
| Engine steps | StepLines(+1) lands exactly on the next line centre at 0.22 s; three quick steps chain to +3 with one animation; paragraph and section previous/next rules; sections cut + pause; JumpToStart = Pmin; AtEnd refuses Play; backward from AtEnd → Paused |
| Engine modes | Line-mode auto-step interval = 60/lpm; axis in line mode changes step rate; mode switch snaps to nearest line |
| Engine axis | axis overrides while active; release ramps back; FailSafeStop ramps to 0 in 0.3 s and pauses |
| Engine layout | new geometry (font change, lines halve) keeps the same anchor on the reading line; viewport-only change keeps the document Y on the reading line; same key ignored |
| Engine threading | `Post` from 4 threads while ticking: no lost commands; `TickIfStale` ticks only after 50 ms; 0 bytes allocated across 1,000 steady ticks |
| Router | Typing vs Transport vs `app:text`; mode-dependent bindings (Down = speed/line); repeats filtered; wheel sign and fractional accumulation; axis slot assignment; disconnect → FailSafeStop; provider start/stop follows SelectedController (fake providers); ToggleBlank calls SetBlank; ToggleOutput raises the event |
| Settings | default round-trip byte-identical; missing file → defaults; corrupt file → `.bad-*` + defaults; unknown field ignored; missing field → default; atomic replace leaves `.bak`; debounce coalesces 10 updates into 1 write |

### 4.B Input providers

- **Owns:**
  - `src/IluPrompter.Input/**` except `Contracts/**` and the csproj
  - `tests/IluPrompter.Input.Tests/**`, `docs/streams/B-input.md`
  - Delete `Providers.Stubs.cs` and replace it with `RawInput/RawMouseWheelProvider.cs`, `XInput/XInputProvider.cs`,
    `Shuttle/ContourShuttleProvider.cs` (+ helpers)
- **Implements:**
  - **`AppInputBridge`** (contract §2.12):
    - Window `PreviewKeyDown` (Key.System → SystemKey; ignore Ctrl/Alt/Shift/Win alone; ImeProcessed/DeadCharProcessed
      ignored) → chord via `ControlIds.KeyChord`.
    - Typing-context detection (`Keyboard.FocusedElement` is TextBoxBase, PasswordBox or editable ComboBox → DeviceId
      `app:text`). `e.Handled = sink.Submit(...)`.
    - Wheel-area `PreviewMouseWheel` → `RelativeInput(Wheel, Delta/120)`. Middle/XButtons down/up → ButtonInput edges.
      `e.Handled = Submit(...)`.
  - **`RawMouseWheelProvider`:**
    - Dedicated STA thread "ILU RawInput" with `Dispatcher.Run()` and an `HwndSource` message-only window
      (`ParentWindow = new IntPtr(-3)`).
    - `RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink | RawInputDeviceFlags.DevNotify, hwnd)`.
    - `WM_INPUT` (0x00FF) → `RawInputData.FromHandle(lParam)`. Only `RawInputMouseData` whose `Header.DeviceHandle`
      maps to the selected device path is used: wheel `(short)ButtonData/120.0`, middle/X button edges.
    - `WM_INPUT_DEVICE_CHANGE` remaps handles and reports Connected/Disconnected. Horizontal wheel is ignored.
    - `EnumerateDevices` lists mice with product names (or VID/PID).
    - If Phase-0 S4b shows no `WM_INPUT` while unfocused with a message-only window, switch to a never-shown top-level
      tool window (`WS_EX_TOOLWINDOW`, size 0, not shown).
  - **`XInputProvider`:**
    - Background thread "ILU XInput". Polls `Vortice.XInput.XInput.GetState(i, out var state)` with `Thread.Sleep(8)`
      (≈ 60–125 Hz; never call `timeBeginPeriod`).
    - UserIndex −1 = first connected pad. Empty slots are probed at most once per second (probing disconnected slots is
      slow).
    - Normalise per §2.3. Emit axes on change, button edges for every `GamepadButtons` flag (names per `ControlIds`).
    - Battery via `XInput.GetBatteryInformation` every 30 s → `BatteryLevel`. Disconnect → `Disconnected` status.
  - **`ContourShuttleProvider`:**
    - HidSharp `DeviceList.Local.GetHidDevices()`, filtered to VID/PID 0B33:0020 (ShuttleXpress), 0B33:0030 (ShuttlePRO v2)
      and 05F3:0240 (ShuttlePRO v1).
    - `TryOpen` → `HidStream` with `ReadTimeout = Timeout.Infinite`, read loop on thread "ILU Shuttle".
    - `DeviceList.Local.Changed` → reconnect. Open failure → status Detail "Close the Contour ShuttlePro software, then
      reconnect".
    - Pure helpers (internal, tested):
      - `ContourShuttleReportParser.TryParse(ReadOnlySpan<byte> report, out ShuttleReport r)`, accepting 5 bytes or
        6+ with a leading report id 0x00. `ShuttleReport(sbyte Ring, byte Jog, ushort Buttons)`, with Buttons little-endian
        from bytes 3–4.
      - `JogDelta(byte prev, byte cur) = (sbyte)(cur − prev)`.
      - `ContourModels` table: per PID, raw button bit → logical number (left to right, top to bottom). Verify on the
        physical device. Unknown bits pass through as bit+1.
- **Must not:** use `SetWindowsHookEx`, keyboard Raw Input, `RegisterHotKey`, Windows.Gaming.Input or GameInput; touch
  any WPF control outside the bridge's attached elements.
- **Acceptance:**
  - Unit tests pass.
  - With hardware present and the app unfocused (Notepad focused): XInput triggers, the dedicated wheel and the shuttle
    ring/jog each move the preview (Phase-0 S4).
  - Unplugging a controller mid-scroll ramps to a stop within 0.3 s and shows MISSING.
  - **Before the first background-input run, ask the user** (Defender rule): explain that Raw Input mouse INPUTSINK, HID
    reads and XInput polling run while unfocused. None of them capture the keyboard.
- **Tests:**
  - Parser: Xpress/PRO samples incl. report-id prefix; ring −7…7 → k/7; jog wrap 255→0 = +1, 0→255 = −1; first-report
    baseline; button edge diffing and logical mapping.
  - XInput normalisation (−32768 → −1, 32767 → 1, triggers /255), change threshold, button flag → id names.
  - Chord formatting incl. `Key.System` handling (pure helper taking Key + ModifierKeys).
  - Wheel delta fraction accumulation is the router's job: assert the bridge does **not** round.

### 4.C Output window, monitors, preview, prompter surface

- **Owns:**
  - `src/IluPrompter.Rendering/**` except the csproj and the frozen public members of `PrompterPreview`/`PreviewServices`
  - `tests/IluPrompter.Rendering.Tests/**`, `docs/streams/C-rendering.md`
- **Implements:**
  - **`Surface/PrompterSurface`** (internal UserControl, one instance per thread):
    - Black (`script-paper`) Grid. Text-area Canvas at `TextArea` (ClipToBounds) hosting a read-only RichTextBox:
      IsReadOnly, Focusable=false, IsHitTestVisible=false, BorderThickness 0, Padding 0, scrollbars Disabled,
      `Width = TextArea.Width`, unconstrained height.
    - `RenderTransform = TranslateTransform` (pre-allocated; mutate `.Y`). `TextFormattingMode=Ideal`,
      `TextRenderingMode=Grayscale`, `TextHintingMode=Animated` while moving.
    - When the position is unchanged for 2 frames: snap Y to whole device pixels and switch to `TextHintingMode=Fixed`.
    - Reading-line markers (DESIGN Output: ▶ ◀ in the margins, 0.5 × line height, `reading-marker`; 6 px edge bar if the
      margin < 0.6 × line height). Dim-above overlay (black 55 % above the reading line when on).
    - Rebuild pipeline: TextArea via `TypographyMath.ComputeTextArea`; `OutputTextStyle` from settings + `ThemeResources`
      (ScriptFont(FontFace), weight, ink = DefaultInk token, section ink `ink-grey`, END ink `end-marker`, END rule
      `line-strong`, highlight inks from `ThemeResources.Highlights`).
    - Then `factory.Create` → host.Document → `UpdateLayout()` → `geometryBuilder.Build` → `engine.SetLayout`. Log
      rebuilds > 50 ms.
    - `LayoutKey = $"{snapshot.Version}|{width:R}|{fontSize:R}|{spacing:R}|{face}|{weight}|{gap:R}|{alignment}|{showSections}"`
      (invariant culture).
  - **`Output/OutputHost` + `OutputWindow`:**
    - Thread, Dispatcher and lifecycle per §3.
    - Window: `WindowStyle=None`, `ResizeMode=NoResize`, `AllowsTransparency=False`, Topmost, `ShowInTaskbar=False`,
      `ShowActivated=False`, `Cursor=None`, Background `script-paper` set before first show. `WS_EX_NOACTIVATE |
      WS_EX_TOOLWINDOW` in `SourceInitialized`. `SetWindowPos` to the monitor's **physical** `BoundsPx` (not the work
      area); re-place on `WM_DPICHANGED`.
    - Show: resolve via `IMonitorService.ResolveOutputMonitor(settings.Output.MonitorStableId)`; no display →
      `NoSecondDisplay`; mirrored → `Duplicated`.
    - On show, persist `MonitorStableId` (if it was null) and `Last*` geometry via `ISettingsService.Update`.
    - Monitor vanished while open → hide the window, `Display=Missing`, IsOpen stays true. It reappears on the same
      StableId and **never** moves to the primary display.
    - Blank = black overlay over the surface (scrolling continues).
    - `DisplaySleepGuard`: `SetThreadExecutionState(ES_CONTINUOUS|ES_DISPLAY_REQUIRED|ES_SYSTEM_REQUIRED)` while Playing
      and open; cleared otherwise. GC latency mode per §3.
    - Dev-only flag `--dev-output-on-primary` opens the output as a 960 × 540 normal window on the primary display (for
      single-monitor testing; logs a warning; never the default).
  - **`Monitors/MonitorService`:**
    - `EnumDisplayMonitors` + `GetMonitorInfo` (MONITORINFOEXW) + `GetDpiForMonitor` (effective) + `QueryDisplayConfig`
      (QDC_ONLY_ACTIVE_PATHS).
    - From QueryDisplayConfig: StableId = target device path; FriendlyName = `monitorFriendlyDeviceName`; refresh rate;
      IsDuplicated = two active paths sharing one source. DisplayNumber is parsed from `\\.\DISPLAYn`.
    - `SystemEvents.DisplaySettingsChanged`, debounced 500 ms → `MonitorsChanged`.
    - Pure `MonitorResolver.Resolve(IReadOnlyList<MonitorInfo>, string? savedId)`, tested.
  - **`Preview/PrompterPreview`:**
    - Letterbox in `backdrop` at the output aspect ratio; `PrompterSurface` at output DIP size, scaled `s = min(w/W, h/H)`.
    - Overlays (preview only): margin tint `margin-overlay` + 1 px dashed `focus` inner edge; reading line 1 px dashed
      `text-muted` across the margins + "Reading line" caption; BLANK → script at 35 % + `chip-active` "■ BLANK · the
      presenter sees black".
    - Margin edges: hover shows a 4 px `action-pressed` handle and resize cursor; drag → `MarginMath.SetEdgePixels`
      (Shift snaps to 8 px). Reading line drag → `ReadingLineFraction` 0.10–0.60.
    - `IsPointerOverScript`. Driving the picture (amended at integration, see §3 rule 5): while the output renders, repaint
      on the output's `FramePulse` (no Rendering subscription on the UI thread); otherwise subscribe to Rendering, call
      `TickIfStale(now, 0.001)` and set `Y = −pos`.
  - **`Diagnostics/FrameStats`** (with `--diag`): per surface, a ring buffer of frame intervals. Every 10 s log
    mean/p95/p99/max and the count > 1.5 × nominal (nominal = 1/RefreshHz). Also log both surfaces' first 50 LineTops
    per LayoutKey (S7).
  - Internal `IOutputRenderer` seam for the D2D fallback (RESEARCH §1.3). Not implemented in v1.
- **Must not:** put any UI chrome, text or cursor on the output; create brushes from hex; call `XamlReader.Load`.
- **Acceptance:**
  - With a second extended display: F11 shows the output exactly on its bounds, with focus kept in the control window.
  - Scroll smoothness per S2. Preview within one frame of the output. Hot-unplug and replug behave as specified.
  - Duplicated mode is detected. Rebuild of a 20-page script ≤ 150 ms on this laptop. No allocations per frame in the
    render handlers (check with `--diag` GC counts).
- **Tests:**
  - `MonitorResolver` (saved present / saved missing / only primary / duplicated / ordering change).
  - Preview scale and letterbox maths. LayoutKey changes for each input and is stable otherwise.
  - Marker placement rule (bar when the margin is narrow).
  - `[WpfFact]` PrompterSurface rebuild with stub documents produces a geometry and calls `SetLayout` (fake engine).

### 4.D Control window shell, theme, settings UI

- **Owns:**
  - `src/IluPrompter.App/**` except the csproj
  - `src/IluPrompter.Theme/**` except the csproj and frozen ThemeResources members (additions allowed)
  - `tests/IluPrompter.App.Tests/**`, `docs/streams/D-shell.md`
  - **D owns `App.xaml`, `App.xaml.cs` (startup order frozen, §2.15), `MainWindow`, all DI wiring in
    `Composition/AppServiceCollectionExtensions.cs`.**
- **Implements (all per DESIGN.md, including wording):**
  - **Theme:** `IluControls.xaml` with every `Ilu.Text.*` style and dark templates for Button (primary, secondary,
    transport, Play paused/scrolling), toolbar toggle, segmented switch, inputs, ComboBox, CheckBox/Radio, Menu/ContextMenu,
    ToolTip, ScrollBar, ListBox rows and state chips. Focus ring per DESIGN States.
  - **Main window layout:** regions A–G and the Wide/Compact/Narrow tiers (DESIGN Layout). Dark title bar via
    `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE = 20)` with `[DllImport]` in `Interop/`, on every window. The
    window opens maximised and remembers bounds (`Settings.ControlWindow`).
  - **Editor:**
    - `Editing/EditorController`: RichTextBox on `editor-paper`, base `Editor.BaseFontSize`, zoom = `LayoutTransform`
      (never font size).
    - TextChanged → 250 ms debounce → `ISnapshotBuilder.Build(doc, documents.NextVersion(), base, marker)` → `Publish`.
    - GotKeyboardFocus → `KeyMode.Typing`. Esc in the editor → `KeyMode.Transport` and focus the preview.
    - Gutter ▶ + `raised` band at `Status.ReadingAnchor` (via `IDocumentNavigator.GetPointer`). Double-click gutter →
      `SeekToAnchor`. LIVE edit bar and caption.
    - `StyleSectionParagraphs` after edits; recovery copy every `RecoverySeconds`.
  - **Toolbar (E):** Undo/Redo, Size ▾ (4 sizes), B/I/U (EditingCommands), S (`ToggleStrikethrough`), ink ▾ and
    highlight ▾ (from `ThemeResources.Inks/Highlights`, colours via `GetColor`), Clear, §+ (`InsertSectionMarker`),
    Find, Zoom. Reflects the caret's formatting. Script keys per DESIGN.
  - **Sections panel:** rows from `Documents.Current.Sections`, durations via `TimeEstimator.SectionDurations(engine.Geometry, …)`,
    current = `Status.SectionIndex`. Click shows the section in the editor; double-click/Enter → `GoToSection`; context
    menu Rename/Delete marker.
  - **Live column:**
    - Chips (output: from `OutputState` + "user pressed Show" flag → DESIGN chip table; controller: from
      `DeviceStatusChanged` + LIVE → `fault`/`attention` rule).
    - Preview frame (1 px `line-strong`, 2 px `live` when `IsLive`) + caption; wheel hint when
      `IsPointerOverScript && SelectedController == MouseWheel && DedicatedDeviceId == null`.
    - Transport bar → `IInputRouter.Execute`.
    - Speed control (−10…10 track centred on 0, ±0.5 / Shift ±0.1, `readout`, ≈ wpm) → `EngineCommand.SetSpeed`.
    - Mode switch, Blank toggle, readouts (10 Hz `Status` poll).
    - D7 output layout: lines visible, spacing, margin editor using `MarginMath` with the link toggle.
  - **App bar, status bar, menus;** file New/Open/Save/Save As/Export (WPF OpenFileDialog/SaveFileDialog,
    `IScriptFileService`, `ISnapshotBuilder.LoadInto/ToScriptFile`, `IPromptDocumentService.SetFileInfo`); recent files.
    InputBindings: Ctrl+N/O/S/Shift+S, Ctrl+, (Settings), Ctrl+/ (shortcuts), F2 (go to script).
    `AppCommandRequested(ToggleOutput)` → Show/Hide output, with the "Close the output?" dialog while LIVE.
  - **Settings window** (non-modal, owned, applies live): pages Controller (3 cards, device pickers from
    `IScrollInputProvider.EnumerateDevices()`, read-only mapping table, test readout from `InputObserved`), Output
    (display picker from `IMonitorService`, margin editor, lines, spacing, reading line, toggles), Text, Scrolling,
    Shortcuts, About (logo via `ThemeResources.LogoUri`, licences). The app's own dialog skin; never `MessageBox`.
- **Must not:** contain hex colours or `#RRGGBB` anywhere outside `IluTokens.xaml`; use the mouse wheel to change values;
  animate the control window; hand-edit token values.
- **Acceptance:**
  - Every DESIGN.md component exists and looks as specified at 1920×1080 and 1366×768 (100 % and 125 %).
  - Keyboard-only operation of the live column. No white flash on startup.
  - Edit → preview update ≤ 300 ms after typing stops. Open/save .ilup round-trips formatting.
- **Tests:**
  - `ThemeTokenTests`: every DESIGN.md front-matter colour exists in IluTokens.xaml with the same value. A scan of
    `src/**/*.cs|*.xaml` (excluding `bin/obj` and IluTokens.xaml) finds no `#[0-9A-Fa-f]{6}` or `{8}` literal.
  - View-model tests: speed step/readout formatting, chip state mapping table (all rows of DESIGN's two chip tables),
    margin editor link behaviour through `MarginMath`, responsive tier selection by width.

### 4.E Document model and storage

- **Owns:**
  - `src/IluPrompter.Documents/**` except `Contracts/**` and the csproj
  - `tests/IluPrompter.Documents.Tests/**`, `samples/**`, `docs/streams/E-documents.md`
- **Implements:**
  - **`DocumentNavigator`:** canonical order (contract), skipping blocks with `Tag == "ilu:generated"`; char offsets via
    `TextRange(...).Text.Length`.
  - **`SnapshotBuilder`:**
    - Flatten a copy (List items → paragraphs prefixed "• " or "n. ", keeping inline formatting; table cells in row
      order; Sections unwrapped; BlockUIContainer/InlineUIContainer/images removed), so editor canonical index ==
      snapshot index.
    - XAML via `TextRange.Save(DataFormats.Xaml)`. Sections via the parser. Word count over non-section paragraphs.
    - `LoadInto` via the sanitiser then `TextRange.Load`.
  - **XAML sanitiser (security):** before any `TextRange.Load(Xaml)` of file content, parse with `XmlReader`
    (DtdProcessing.Prohibit, no resolver). Allow only FlowDocument, Section, Paragraph, Run, Span, Bold, Italic,
    Underline, LineBreak, List, ListItem, Table, TableRowGroup, TableRow, TableCell, TableColumn and known text-formatting
    attributes. Reject markup extensions (`{`-prefixed attribute values) and `x:`/`clr-namespace` content. On violation →
    `ScriptFileException`.
  - **`ScriptFileService`:**
    - `.ilup` JSON (IluJson.Options), atomic `File.Replace` with `.bak`.
    - `.txt`: UTF-8 (BOM-aware, fallback `Encoding.Latin1`); one paragraph per line, empty lines kept as empty paragraphs.
    - `.rtf`: `TextRange.Load(Rtf)` into a temp FlowDocument, then normalise.
    - `.docx`: OpenXML SDK walk of paragraphs and runs: b, i, u, strike, highlight, colour, sz. Title/Heading 1–3 styles
      → `## text`, lists → bullet prefix, tables → paragraphs, images skipped.
    - Normalise on import: colours to the nearest of the 8 inks, highlights to the nearest of the 5 (RGB distance, via
      `ThemeResources`); sizes to the nearest of the 4 relative sizes against the document's dominant size;
      FontFamily/FontWeight ≤ 500 cleared (inherit).
    - Export `.rtf` (TextRange.Save Rtf) and `.txt` (paragraphs joined by CRLF).
  - **`SectionParser`:** contract rules. `###` counts (name has the `#` trimmed). Whitespace before the marker allowed.
    The marker is configurable.
  - **`OutputDocumentFactory`:** every rule in the `IOutputDocumentFactory` doc comment.
    - Run weights ≤ 500 → style weight, ≥ 600 → 700.
    - Strike/underline as `TextDecoration` with `PenThicknessUnit=FontRenderingEmSize`, thickness 0.06, run ink.
    - Text in a known highlight takes its ink. Unknown foreground colours keep their value.
    - Section paragraphs and the generated END block per the doc comment (END tagged `ilu:generated`).
  - **`LayoutGeometryBuilder`:**
    - Line walk via `GetLineStartPosition(1, out moved)`. Top and height from
      `GetCharacterRect(LogicalDirection.Forward)`. Paragraph index from the `TextPointer.Paragraph` → index map
      (navigator order). Char offset within the paragraph.
    - ParagraphTops, Sections (marker paragraph top), EndTop (END block top), DocumentHeight.
    - Target ≤ 30 ms for 1,000 lines.
    - (Integration, 19 Oct 2026.) Tops are **line-box** tops, not `GetCharacterRect` glyph boxes (at 91.5 DIP Poppins
      the raw rectangle is 13.4 DIP above the line box; the walk corrects it via the font's baseline ratio). The builder
      first reads WPF's laid-out line visuals (one per line, offset to its line-box top; start offsets from each line's
      first glyph run): about 7 ms per 1,000 lines. The line walk above is the checked fallback (45–120 ms per 1,000
      lines) and gives identical tables.
  - **`ScriptFormatter`:** contract. Sizes as `editorBase × factor`; `GetSize` maps to the nearest factor and returns null
    if the range is mixed; highlight applies its ink; ClearFormatting resets size to Normal (not inherit-less).
    `InsertSectionMarker` splits at the caret paragraph and selects the name.
  - **`samples/`:** `sample-short.txt` (3 sections) and `sample-20-pages.txt` (≈ 6,000 words, 10 `##` sections, plain
    English, no copyrighted text) for Phase-0 S2.
- **Must not:** use `XamlReader.Load`/`XamlReader.Parse` on file data; keep state between calls (services are singletons
  used from one thread, but must be stateless).
- **Acceptance:**
  - Import of RTF/DOCX keeps b/i/u/strike/ink/highlight/size and turns headings into sections.
  - `.ilup` round-trip is lossless. Malicious XAML is rejected. The anchor of any caret position resolves to the same
    text in the snapshot document.
- **Tests (`[WpfFact]`):**
  - Parser cases.
  - Flatten: list/table → paragraph count and anchors equal between editor and snapshot.
  - `.ilup` round-trip (formatting XAML equal).
  - TXT encodings. RTF fixture (string in test). DOCX fixture generated in-test with OpenXML SDK (heading, bold, highlight,
    colour, size, list).
  - Sanitiser rejects `<Button/>`, `ObjectDataProvider`, `{x:Static}`, DTD.
  - Factory: Large run at editor base 20 → 1.25 × output base; highlight ink enforced; section hidden/shown; END present
    and skipped by the navigator.
  - Geometry: host a RichTextBox in an off-screen borderless Window (Left −10000, `ShowActivated=false`), lay out 50
    paragraphs, assert line count > paragraph count when wrapping, tops increasing, END after the last line, anchors map
    back to the right line.

### 4.F Integration and merge step (after all five report done)

Run by one integration agent on `main`:

1. For each stream branch: `git diff --name-only main...stream/x`. **Fail** if any path is outside that stream's "Owns"
   list or is a frozen file (§7.1). Contract requests in the stream reports are applied here, by the integrator only.
2. Merge in the order **A → E → B → C → D** (no conflicts are expected; the order puts dependencies first for any
   behavioural fixes). After each merge: `dotnet build IluPrompter.sln` and `dotnet test IluPrompter.sln` must pass.
3. Confirm no `STUB` markers remain (`git grep -n "STUB"` over `src/`) and that `Providers.Stubs.cs` is gone.
4. Run `ILUPrompter.exe --diag` (Debug), open `samples/sample-20-pages.txt` and walk through Phase-0 S1–S8 (§6). Record
   results in `docs/streams/INTEGRATION.md`.
5. Fix integration bugs on an `integration/*` branch. Fixes inside a stream's area keep that area's contracts.
6. Update `docs/ARCHITECTURE.md` (this file) for any applied contract change, plus memory/notes per the user's
   documentation rule. Commit with the attribution trailer.
7. Publish the self-contained folder (§1.3) and run it once from `publish/ILUPrompter` (corporate policy check, S1).

---

## 5. Scaffold commit specification

One commit on `main`, made by one agent, **before** any stream starts. It is mechanical: create exactly the files below,
with the content given (§2 for contracts, Appendix A for everything else), then run the verification. Message:
`Scaffold ILU Prompter solution: projects, frozen contracts, compiling stubs, test projects`.

### 5.1 Files

| Path | Content source | Frozen after scaffold? | Later owner |
|---|---|---|---|
| `Directory.Build.props` | Appendix A | yes | — |
| `Directory.Packages.props` | Appendix A | yes | — |
| `IluPrompter.sln` | generated by commands in §5.3 | yes | — |
| `src/IluPrompter.Core/IluPrompter.Core.csproj` | Appendix A | yes | — |
| `src/IluPrompter.Core/Contracts/{Commands,Input,Engine,Geometry,Document,Output,Settings,Diagnostics}.cs` | §2.2–§2.9 | **yes** | — |
| `src/IluPrompter.Core/Layout/LayoutMath.cs` | §2.10 | signatures | A |
| `src/IluPrompter.Core/Settings/DefaultBindings.cs` | §2.10 | signatures | A |
| `src/IluPrompter.Core/Engine/ScrollEngine.cs` (stub) | Appendix A | ctor signature | A |
| `src/IluPrompter.Core/Input/InputRouter.cs` (stub) | Appendix A | ctor signature | A |
| `src/IluPrompter.Core/Settings/JsonSettingsService.cs` (stub) | Appendix A | ctor signatures | A |
| `src/IluPrompter.Core/Input/KeyModeState.cs` | Appendix A | **yes** (complete) | — |
| `src/IluPrompter.Core/Documents/PromptDocumentService.cs` | Appendix A | **yes** (complete) | — |
| `src/IluPrompter.Core/Diagnostics/FileAppLog.cs` | Appendix A | **yes** (complete) | — |
| `src/IluPrompter.Core/CoreServiceCollectionExtensions.cs` | Appendix A | method signature | A |
| `src/IluPrompter.Theme/IluPrompter.Theme.csproj` | Appendix A | yes | — |
| `src/IluPrompter.Theme/Themes/IluTokens.xaml` | generated, §5.4 (listing in Appendix A) | values = DESIGN.md | D |
| `src/IluPrompter.Theme/Themes/IluControls.xaml` | Appendix A | no | D |
| `src/IluPrompter.Theme/ThemeResources.cs` | §2.14 | public members | D |
| `src/IluPrompter.Documents/IluPrompter.Documents.csproj` | Appendix A | yes | — |
| `src/IluPrompter.Documents/Contracts/DocumentContracts.cs` | §2.11 | **yes** | — |
| `src/IluPrompter.Documents/{Layout/DocumentNavigator,Layout/SectionParser,Layout/OutputDocumentFactory,Layout/LayoutGeometryBuilder,Storage/SnapshotBuilder,Storage/ScriptFileService,Editing/ScriptFormatter}.cs` (stubs) | Appendix A | class names + ctors | E |
| `src/IluPrompter.Documents/DocumentsServiceCollectionExtensions.cs` | Appendix A | method signature | E |
| `src/IluPrompter.Input/IluPrompter.Input.csproj` | Appendix A | yes | — |
| `src/IluPrompter.Input/Contracts/IAppInputBridge.cs` | §2.12 | **yes** | — |
| `src/IluPrompter.Input/AppWindow/AppInputBridge.cs` (stub) | Appendix A | ctor | B |
| `src/IluPrompter.Input/Providers.Stubs.cs` (stubs) | Appendix A | class names + ctors | B (deletes it) |
| `src/IluPrompter.Input/InputServiceCollectionExtensions.cs` | Appendix A | method signature | B |
| `src/IluPrompter.Rendering/IluPrompter.Rendering.csproj` | Appendix A | yes | — |
| `src/IluPrompter.Rendering/NativeMethods.txt` | Appendix A | no | C |
| `src/IluPrompter.Rendering/Preview/PrompterPreview.cs` (stub) | §2.13 | public members | C |
| `src/IluPrompter.Rendering/Output/OutputHost.cs` (stub) | Appendix A | ctor | C |
| `src/IluPrompter.Rendering/Monitors/MonitorService.cs` (stub) | Appendix A | ctor | C |
| `src/IluPrompter.Rendering/RenderingServiceCollectionExtensions.cs` | Appendix A | method signature | C |
| `src/IluPrompter.App/IluPrompter.App.csproj` | Appendix A | yes | — |
| `src/IluPrompter.App/app.manifest` | Appendix A | no | D |
| `src/IluPrompter.App/App.xaml`, `App.xaml.cs` | Appendix A | startup order | D |
| `src/IluPrompter.App/Composition/AppServiceCollectionExtensions.cs` | Appendix A | method signature | D |
| `src/IluPrompter.App/Views/MainWindow.xaml(.cs)` (placeholder) | Appendix A | no | D |
| `tests/IluPrompter.{Core,Input,Documents,Rendering,App}.Tests/*.csproj` | Appendix A | yes | — |
| `tests/IluPrompter.*.Tests/ScaffoldSmokeTests.cs` | Appendix A | no | each stream |

Not in the scaffold: `assets/` (already committed), `docs/streams/*`, `samples/*` (created by streams).

### 5.2 Stub rules

Stubs compile, **never throw** from normal calls and do the simplest useful thing. For example, the stub engine moves at
the set speed, the stub snapshot builder really saves XAML and the stub preview ticks the engine. So the app launches in
every worktree. Each stub class carries a `STUB (stream X replaces …)` doc comment. Integration fails if any remain.

### 5.3 Commands (run from the repo root, after writing the files)

```powershell
dotnet new sln -n IluPrompter
Get-ChildItem src -Recurse -Filter *.csproj | ForEach-Object { dotnet sln IluPrompter.sln add --solution-folder src $_.FullName }
Get-ChildItem tests -Recurse -Filter *.csproj | ForEach-Object { dotnet sln IluPrompter.sln add --solution-folder tests $_.FullName }
dotnet build IluPrompter.sln     # expect: Build succeeded, 0 warnings
dotnet test IluPrompter.sln      # expect: 8 passed (Core 3, Input 1, Documents 2, Rendering 1, App 1)
git add -A; git commit           # with the attribution trailer
```

Do not run the app as part of the scaffold step. It is safe to run, but the integration step owns run checks.

### 5.4 Regenerating IluTokens.xaml

Mechanical rule (the listing in Appendix A is its output for today's DESIGN.md):
- Each `colors:` entry `name: "#hex"` → `<Color x:Key="Ilu.Color.{Pascal(name)}">#HEX</Color>` and
  `<SolidColorBrush x:Key="Ilu.Brush.{Pascal}" Color="{StaticResource Ilu.Color.{Pascal}}"/>`.
- Each `spacing:` `name: Npx` → `<sys:Double x:Key="Ilu.Size.{Pascal}">N</sys:Double>`.
- Each `rounded:` → `<CornerRadius x:Key="Ilu.Radius.{Pascal}">N</CornerRadius>`.
- Then the fixed font keys, `Ilu.Pad.*` and the `Ilu.Motion.*` durations from DESIGN's Motion table.
- `Pascal("line-strong") = "LineStrong"`, `Pascal("s1") = "S1"`, and `xmlns:sys="clr-namespace:System;assembly=mscorlib"`.

---

## 6. Phase-0 smoke checks (run at integration on this machine)

Before any run that reads controllers while unfocused (S4), **tell the user what will run and get a yes** (CLAUDE.md
Defender rule). No check involves keyboard capture, network access or scanning. Changing Windows display settings (scale,
refresh, Win+P) is done **by the user**, not by an agent.

| # | Check | How to verify on this laptop | Pass |
|---|---|---|---|
| S1 | Self-contained folder runs under corporate policy | `dotnet publish … -o publish/ILUPrompter`; run `publish\ILUPrompter\ILUPrompter.exe` from Explorer | Starts with no SmartScreen/AppLocker block. If blocked, report to the user; never work around it |
| S2 | Scroll smoothness, 92 and ≈ 200 DIP fonts | Second display extended (or `--dev-output-on-primary` if none). Run with `--diag`, open `samples/sample-20-pages.txt`, Show output, play 60 s each at speed 2, 4.5, 8 and 10 with lines visible 8, then 4. Repeat at 4.5 while typing continuously in the editor | Log FrameStats per run: p99 ≤ 1.5 × nominal interval, < 0.5 % frames > 1.5 × nominal, max < 50 ms. User watches from reading distance: no visible judder at 4.5. Typing causes no change in output stats (thread isolation). If it fails: try snapped mode, then the RESEARCH §4.2 fallback ladder |
| S3 | Mixed refresh rates | If the displays differ (e.g. 60 vs 120 Hz; Settings › Display › Advanced display shows each), repeat S2 at 4.5 and compare output vs preview FrameStats | Output intervals match the output panel's rate. If not, record and recommend matching rates or making the prompter display primary |
| S4a | XInput works unfocused | Settings › Controller › Xbox. Focus Notepad. Pull RT gradually, press A, D-pad | Preview/output move. Log shows `input` traces. Unplug mid-scroll → stops within 0.3 s, chip MISSING |
| S4b | Dedicated Raw Input wheel unfocused | Plug in a second mouse, select it as the dedicated device, focus Notepad, roll its wheel | Speed changes (Display) or lines step (Line). The other mouse's wheel does nothing to the prompt |
| S4c | ShuttleXpress unfocused (if available) | Select Jog-shuttle, focus Notepad, turn ring and jog, press buttons | Ring = speed, jog = lines, buttons per mapping. Logical button numbering confirmed and recorded in `docs/streams/B-input.md` |
| S5 | Second-monitor placement | F11 with the display extended. Compare the logged window rect (GetWindowRect) with MonitorInfo.BoundsPx. Type in the editor while showing. Alt+Tab. Move the mouse onto the output. Then the user sets the output display to 150 % scale, unplugs and replugs it, and presses Win+P › Duplicate | Rect == bounds; focus stays in the control window; not in Alt+Tab/taskbar; cursor hidden. At 150 %, still 8 lines and fills the screen. Unplug while LIVE → NO DISPLAY chip, nothing on the laptop screen; replug → returns. Duplicate → DUPLICATED |
| S6 | Bundled fonts resolve | `--diag` logs, for UI and script at weights 400/500/600, the resolved `GlyphTypeface.FontUri` | URIs point into `IluPrompter.Theme;component/Fonts/`. If `#Poppins` doesn't group weights, switch keys to the per-weight family names ("Poppins Medium") as DESIGN anticipates (D's Theme change) |
| S7 | Preview/output layout parity | `--diag` logs LineTops from both surfaces per LayoutKey | Same line count; max |Δtop| ≤ 0.5 DIP |
| S8 | Display stays awake | Set a 1-minute screen timeout (user) and play for 3 minutes | The display never sleeps while playing; it sleeps normally when paused (unless GPO enforces a lock, which is an IT question) |

---

## 7. Rules for every agent

1. **Frozen files** (nobody edits after the scaffold): `Directory.Build.props`, `Directory.Packages.props`,
   `IluPrompter.sln`, every `*.csproj`, `src/IluPrompter.Core/Contracts/**`,
   `src/IluPrompter.Documents/Contracts/**`, `src/IluPrompter.Input/Contracts/**`, `KeyModeState.cs`,
   `PromptDocumentService.cs`, `FileAppLog.cs`, the public members of `PrompterPreview`/`PreviewServices`/
   `ThemeResources`, the public signatures in `LayoutMath.cs`/`DefaultBindings.cs`, and constructor signatures of DI
   classes. If a contract seems wrong, **don't edit it**: work around it and put a `CONTRACT-REQUEST:` paragraph in your
   final report and commit body (what, why, exact proposed code). The integrator decides.
2. **Ownership:** edit only your stream's paths (§4). Read anything.
3. **Security and IT:** no keyboard hooks, no global keyboard capture, no `RegisterHotKey`, no admin, no installers, no
   network listeners, no network scans of any kind (CLAUDE.md). Ask the user before the first unfocused-input test (S4).
   Never `XamlReader.Load` file content. Never send script content anywhere.
4. **Design:** UI follows DESIGN.md exactly (tokens, sizes, words, British spelling). No hex outside IluTokens.xaml.
5. **Compatibility:** no API newer than .NET 8; C# 12; no `field` keyword, no C# 13+ features; nullable on.
6. **Quality bar:** `dotnet build` with 0 warnings in your projects, `dotnet test` green, no per-frame allocations in
   render or Tick paths, and the app still launches.
7. **Docs and commits:** keep `docs/streams/<X>.md` current after each step, and commit in small steps on your branch.
   End commit messages with the attribution trailer you were given.
8. **Tools:** prefer reputable packages over custom code, but only those in §1.4 without a contract request.

---

## Appendix A: scaffold sources (non-contract files, verbatim)

These compiled together with §2 on 19 Oct 2026 (SDK 8.0.301). Create them exactly. Line endings: CRLF or LF are both
fine.

`Directory.Build.props`

```xml
<Project>
  <!-- THE ONLY PLACE THE FRAMEWORK IS NAMED. Switch to net10.0-windows when the .NET 10 SDK is installed. -->
  <PropertyGroup>
    <IluTargetFramework>net8.0-windows</IluTargetFramework>
  </PropertyGroup>
  <PropertyGroup>
    <TargetFramework>$(IluTargetFramework)</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>$(WarningsAsErrors);nullable</WarningsAsErrors>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
    <Company>ILUMINA</Company>
    <Product>ILU Prompter</Product>
    <Version>0.1.0</Version>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageVersion Include="Vortice.XInput" Version="3.6.2" />
    <PackageVersion Include="HidSharp" Version="2.6.4" />
    <PackageVersion Include="RawInput.Sharp" Version="0.1.3" />
    <PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.106" />
    <PackageVersion Include="DocumentFormat.OpenXml" Version="3.1.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Xunit.StaFact" Version="1.2.69" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Core/IluPrompter.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- No WPF, no WinForms. Pure logic + contracts. -->
  <PropertyGroup>
    <RootNamespace>IluPrompter.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.Core.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Core/Engine/ScrollEngine.cs`

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;

namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signature).
/// Naive: Play/Pause/TogglePlayPause/SetSpeed/NudgeSpeed/JumpToStart/SeekToOffset/ScrollPixels only,
/// no ramps, no steps, no re-anchoring. Enough for streams C and D to see motion.
/// </summary>
public sealed class ScrollEngine : IScrollEngine
{
    private readonly ISettingsService _settings;
    private readonly ConcurrentQueue<EngineCommand> _queue = new();
    private readonly object _gate = new();
    private long _positionBits;
    private long _lastTick;
    private double _setSpeed;
    private bool _playing;
    private LayoutGeometry? _geometry;
    private ViewportMetrics _viewport = new(1600, 952, 314);
    private EngineStatus _status;

    public ScrollEngine(ISettingsService settings)
    {
        _settings = settings;
        _setSpeed = settings.Current.Scroll.StartSpeed;
        _status = new EngineStatus { SetSpeed = _setSpeed };
    }

    public EngineStatus Status => Volatile.Read(ref _status);
    public LayoutGeometry? Geometry => Volatile.Read(ref _geometry);
    public ViewportMetrics Viewport { get { lock (_gate) { return _viewport; } } }
    public event EventHandler<EngineStatus>? StatusChanged;

    public void Post(EngineCommand command) => _queue.Enqueue(command);

    public double ReadPosition() => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _positionBits));

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
        lock (_gate) { Volatile.Write(ref _geometry, geometry); _viewport = viewport; }
    }

    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05)
    {
        if (timestamp - Interlocked.Read(ref _lastTick) < staleAfterSeconds * Stopwatch.Frequency) return false;
        Tick(timestamp);
        return true;
    }

    public void Tick(long timestamp)
    {
        EngineStatus? changed = null;
        lock (_gate)
        {
            var last = Interlocked.Read(ref _lastTick);
            if (timestamp <= last) return;
            Interlocked.Exchange(ref _lastTick, timestamp);
            var dt = last == 0 ? 0 : Math.Min(0.1, (timestamp - last) / (double)Stopwatch.Frequency);
            var pos = ReadPosition();
            while (_queue.TryDequeue(out var c))
            {
                switch (c.Kind)
                {
                    case EngineCommandKind.Play: _playing = true; break;
                    case EngineCommandKind.Pause: _playing = false; break;
                    case EngineCommandKind.TogglePlayPause: _playing = !_playing; break;
                    case EngineCommandKind.SetSpeed: _setSpeed = c.Value; break;
                    case EngineCommandKind.NudgeSpeed: _setSpeed += c.Value; break;
                    case EngineCommandKind.JumpToStart: pos = -_viewport.ReadingLineY; _playing = false; break;
                    case EngineCommandKind.SeekToOffset: pos = c.Value; break;
                    case EngineCommandKind.ScrollPixels: pos += c.Value; break;
                }
            }

            var lineHeight = _geometry?.BaseLineHeight ?? 119.0;
            if (_playing) pos += SpeedCurve.ToDipsPerSecond(_setSpeed, lineHeight, _settings.Current.Scroll.Curve) * dt;
            Interlocked.Exchange(ref _positionBits, BitConverter.DoubleToInt64Bits(pos));
            var transport = _playing ? TransportState.Playing : TransportState.Paused;
            if (transport != _status.Transport || _setSpeed != _status.SetSpeed)
            {
                changed = _status with { Transport = transport, SetSpeed = _setSpeed, EffectiveSpeed = _playing ? _setSpeed : 0 };
                Volatile.Write(ref _status, changed);
            }
        }

        if (changed is not null) StatusChanged?.Invoke(this, changed);
    }
}
```

`src/IluPrompter.Core/Input/InputRouter.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signature).
/// Maps keyboard chords and Execute() for the basic transport only; ignores controllers.
/// </summary>
public sealed class InputRouter : IInputRouter, IInputSink
{
    private readonly IScrollEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IKeyModeState _keyMode;
    private readonly IOutputSurface _output;
    private readonly IEnumerable<IScrollInputProvider> _providers;
    private readonly IAppLog _log;

    public InputRouter(
        IScrollEngine engine,
        ISettingsService settings,
        IKeyModeState keyMode,
        IOutputSurface output,
        IEnumerable<IScrollInputProvider> providers,
        IAppLog log)
    {
        _engine = engine;
        _settings = settings;
        _keyMode = keyMode;
        _output = output;
        _providers = providers;
        _log = log;
    }

    public IReadOnlyList<DeviceStatus> DeviceStatuses => Array.Empty<DeviceStatus>();

    public event EventHandler<DeviceStatus>? DeviceStatusChanged;
    public event EventHandler<InputEvent>? InputObserved;
    public event EventHandler<PrompterCommand>? AppCommandRequested;

    public void Start() => _log.Info("input", $"Router stub started; {_providers.Count()} providers registered.");

    public void Stop()
    {
    }

    public bool Execute(PrompterCommand command)
    {
        switch (command)
        {
            case PrompterCommand.PlayPause: _engine.Post(EngineCommand.TogglePlayPause()); return true;
            case PrompterCommand.Play: _engine.Post(EngineCommand.Play()); return true;
            case PrompterCommand.Pause: _engine.Post(EngineCommand.Pause()); return true;
            case PrompterCommand.SpeedUp: _engine.Post(EngineCommand.NudgeSpeed(_settings.Current.Scroll.SpeedStep)); return true;
            case PrompterCommand.SpeedDown: _engine.Post(EngineCommand.NudgeSpeed(-_settings.Current.Scroll.SpeedStep)); return true;
            case PrompterCommand.JumpToStart: _engine.Post(EngineCommand.JumpToStart()); return true;
            case PrompterCommand.ToggleBlank: _output.SetBlank(!_output.State.IsBlank); return true;
            case PrompterCommand.ToggleOutput: AppCommandRequested?.Invoke(this, command); return true;
            default: return false;
        }
    }

    public bool Submit(InputEvent input)
    {
        InputObserved?.Invoke(this, input);
        if (input is not ButtonInput { IsPressed: true } b || input.Source.Kind != ControllerKind.Keyboard) return false;
        var keys = _settings.Current.Input.Keyboard;
        var mode = _engine.Status.Mode;
        if (keys.Global.TryGetValue(b.ControlId, out var g)) return Execute(g.For(mode));
        if (_keyMode.Mode == KeyMode.Transport && keys.Transport.TryGetValue(b.ControlId, out var t)) return Execute(t.For(mode));
        return false;
    }

    public void ReportStatus(DeviceStatus status) => DeviceStatusChanged?.Invoke(this, status);
}
```

`src/IluPrompter.Core/Input/KeyModeState.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen).</summary>
public sealed class KeyModeState : IKeyModeState
{
    private int _mode;

    public KeyMode Mode
    {
        get => (KeyMode)Volatile.Read(ref _mode);
        set
        {
            if (Interlocked.Exchange(ref _mode, (int)value) != (int)value) Changed?.Invoke(this, value);
        }
    }

    public event EventHandler<KeyMode>? Changed;
}
```

`src/IluPrompter.Core/Settings/JsonSettingsService.cs`

```csharp
using System.Text.Json;

namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signatures).
/// Loads/saves plainly; no debounce, no corrupt-file backup, no atomic replace.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly object _gate = new();
    private readonly IAppLog _log;
    private PrompterSettings _current = PrompterSettings.Default;

    public JsonSettingsService(IAppLog log)
        : this(log, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILU Prompter", "settings.json"))
    {
    }

    /// <summary>Tests pass a temp path.</summary>
    public JsonSettingsService(IAppLog log, string settingsPath)
    {
        _log = log;
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public PrompterSettings Current => Volatile.Read(ref _current);

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<PrompterSettings>(File.ReadAllText(SettingsPath), IluJson.Options);
                Volatile.Write(ref _current, loaded ?? PrompterSettings.Default);
            }
        }
        catch (Exception ex)
        {
            _log.Warn("settings", "Using defaults: " + ex.Message);
            Volatile.Write(ref _current, PrompterSettings.Default);
        }
    }

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        PrompterSettings old;
        PrompterSettings updated;
        lock (_gate)
        {
            old = _current;
            updated = change(old);
            if (ReferenceEquals(old, updated)) return;
            Volatile.Write(ref _current, updated);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(old, updated));
    }

    public Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, IluJson.Options));
        return Task.CompletedTask;
    }
}
```

`src/IluPrompter.Core/Documents/PromptDocumentService.cs`

```csharp
namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen). Thread-safe holder of the current snapshot and file info.</summary>
public sealed class PromptDocumentService : IPromptDocumentService
{
    private readonly object _gate = new();
    private PromptDocumentSnapshot _current = PromptDocumentSnapshot.Empty;
    private long _version;

    public PromptDocumentSnapshot Current => Volatile.Read(ref _current);
    public string? FilePath { get; private set; }
    public string Title { get; private set; } = "Untitled";
    public bool IsDirty { get; private set; }
    public DateTimeOffset? LastSaved { get; private set; }

    public event EventHandler<PromptDocumentSnapshot>? SnapshotPublished;
    public event EventHandler? FileInfoChanged;

    public long NextVersion() => Interlocked.Increment(ref _version);

    public void Publish(PromptDocumentSnapshot snapshot)
    {
        lock (_gate)
        {
            if (snapshot.Version <= _current.Version) return;
            Volatile.Write(ref _current, snapshot);
        }

        SnapshotPublished?.Invoke(this, snapshot);
    }

    public void SetFileInfo(string? filePath, string title, bool isDirty, DateTimeOffset? lastSaved)
    {
        lock (_gate)
        {
            FilePath = filePath;
            Title = title;
            IsDirty = isDirty;
            LastSaved = lastSaved;
        }

        FileInfoChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

`src/IluPrompter.Core/Diagnostics/FileAppLog.cs`

```csharp
using System.Globalization;

namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen). Appends lines under a lock; never throws.</summary>
public sealed class FileAppLog : IAppLog
{
    private readonly object _gate = new();

    public FileAppLog(bool diagnosticsEnabled)
        : this(diagnosticsEnabled, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ILU Prompter", "logs"))
    {
    }

    public FileAppLog(bool diagnosticsEnabled, string logDirectory)
    {
        DiagnosticsEnabled = diagnosticsEnabled;
        LogDirectory = logDirectory;
    }

    public bool DiagnosticsEnabled { get; }
    public string LogDirectory { get; }

    public void Info(string area, string message) => Write("INFO", area, message);

    public void Warn(string area, string message) => Write("WARN", area, message);

    public void Error(string area, string message, Exception? exception = null) =>
        Write("ERROR", area, exception is null ? message : message + " | " + exception);

    public void Trace(string area, string message)
    {
        if (DiagnosticsEnabled) Write("TRACE", area, message);
    }

    private void Write(string level, string area, string message)
    {
        try
        {
            var now = DateTime.Now;
            var line = string.Create(CultureInfo.InvariantCulture, $"{now:HH:mm:ss.fff} {level} [{area}] {message}{Environment.NewLine}");
            lock (_gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(Path.Combine(LogDirectory, $"ilu-prompter-{now:yyyyMMdd}.log"), line);
            }
        }
        catch (IOException)
        {
            // Logging must never take the app down.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
```

`src/IluPrompter.Core/CoreServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Core;

/// <summary>Stream A owns this file. Registers Core services as singletons.</summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddIluCore(this IServiceCollection services, bool diagnostics)
    {
        services.AddSingleton<IAppLog>(_ => new FileAppLog(diagnostics));
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IPromptDocumentService, PromptDocumentService>();
        services.AddSingleton<IKeyModeState, KeyModeState>();
        services.AddSingleton<IScrollEngine, ScrollEngine>();
        services.AddSingleton<InputRouter>();
        services.AddSingleton<IInputRouter>(sp => sp.GetRequiredService<InputRouter>());
        services.AddSingleton<IInputSink>(sp => sp.GetRequiredService<InputRouter>());
        return services;
    }
}
```

`src/IluPrompter.Theme/IluPrompter.Theme.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <UseWPF>true</UseWPF>
    <RootNamespace>IluPrompter.Theme</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <!-- Fonts and logo live once, at the repo root, and are linked in as WPF Resources.
         Any TTF dropped into assets/fonts (e.g. Poppins-Bold.ttf, Poppins-Italic.ttf) is picked up with no edit here. -->
    <Resource Include="..\..\assets\fonts\*.ttf" Link="Fonts\%(Filename)%(Extension)" />
    <Resource Include="..\..\assets\branding\wall-logo.png" Link="Images\wall-logo.png" />
    <None Include="..\..\assets\fonts\OFL-*.txt" Link="Licences\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.App.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Theme/Themes/IluTokens.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
  <!-- GENERATED from DESIGN.md front matter. The ONLY file allowed to contain colour hex literals. -->
  <!-- Colours -->
  <Color x:Key="Ilu.Color.Backdrop">#0B0D12</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Backdrop" Color="{StaticResource Ilu.Color.Backdrop}"/>
  <Color x:Key="Ilu.Color.Canvas">#111217</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Canvas" Color="{StaticResource Ilu.Color.Canvas}"/>
  <Color x:Key="Ilu.Color.Surface">#181B1F</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Surface" Color="{StaticResource Ilu.Color.Surface}"/>
  <Color x:Key="Ilu.Color.Raised">#22252B</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Raised" Color="{StaticResource Ilu.Color.Raised}"/>
  <Color x:Key="Ilu.Color.Pressed">#2C3038</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Pressed" Color="{StaticResource Ilu.Color.Pressed}"/>
  <Color x:Key="Ilu.Color.Line">#262A31</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Line" Color="{StaticResource Ilu.Color.Line}"/>
  <Color x:Key="Ilu.Color.LineStrong">#6A707C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.LineStrong" Color="{StaticResource Ilu.Color.LineStrong}"/>
  <Color x:Key="Ilu.Color.Selection">#2B4A80</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Selection" Color="{StaticResource Ilu.Color.Selection}"/>
  <Color x:Key="Ilu.Color.Text">#CCCCDC</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Text" Color="{StaticResource Ilu.Color.Text}"/>
  <Color x:Key="Ilu.Color.TextMuted">#8F8F9C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.TextMuted" Color="{StaticResource Ilu.Color.TextMuted}"/>
  <Color x:Key="Ilu.Color.TextStrong">#ECECF4</Color>
  <SolidColorBrush x:Key="Ilu.Brush.TextStrong" Color="{StaticResource Ilu.Color.TextStrong}"/>
  <Color x:Key="Ilu.Color.Disabled">#5A5E68</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Disabled" Color="{StaticResource Ilu.Color.Disabled}"/>
  <Color x:Key="Ilu.Color.Ok">#596C67</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Ok" Color="{StaticResource Ilu.Color.Ok}"/>
  <Color x:Key="Ilu.Color.OnOk">#FFFFFF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnOk" Color="{StaticResource Ilu.Color.OnOk}"/>
  <Color x:Key="Ilu.Color.Live">#3EE0A4</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Live" Color="{StaticResource Ilu.Color.Live}"/>
  <Color x:Key="Ilu.Color.OnLive">#111217</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnLive" Color="{StaticResource Ilu.Color.OnLive}"/>
  <Color x:Key="Ilu.Color.Idle">#3D434D</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Idle" Color="{StaticResource Ilu.Color.Idle}"/>
  <Color x:Key="Ilu.Color.OnIdle">#FFFFFF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnIdle" Color="{StaticResource Ilu.Color.OnIdle}"/>
  <Color x:Key="Ilu.Color.Active">#0072B2</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Active" Color="{StaticResource Ilu.Color.Active}"/>
  <Color x:Key="Ilu.Color.OnActive">#FFFFFF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnActive" Color="{StaticResource Ilu.Color.OnActive}"/>
  <Color x:Key="Ilu.Color.Attention">#D9981F</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Attention" Color="{StaticResource Ilu.Color.Attention}"/>
  <Color x:Key="Ilu.Color.OnAttention">#111217</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnAttention" Color="{StaticResource Ilu.Color.OnAttention}"/>
  <Color x:Key="Ilu.Color.Fault">#D63A2F</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Fault" Color="{StaticResource Ilu.Color.Fault}"/>
  <Color x:Key="Ilu.Color.OnFault">#FFFFFF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnFault" Color="{StaticResource Ilu.Color.OnFault}"/>
  <Color x:Key="Ilu.Color.Action">#1F60C4</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Action" Color="{StaticResource Ilu.Color.Action}"/>
  <Color x:Key="Ilu.Color.ActionPressed">#3274D9</Color>
  <SolidColorBrush x:Key="Ilu.Brush.ActionPressed" Color="{StaticResource Ilu.Color.ActionPressed}"/>
  <Color x:Key="Ilu.Color.OnAction">#FFFFFF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.OnAction" Color="{StaticResource Ilu.Color.OnAction}"/>
  <Color x:Key="Ilu.Color.Focus">#5794F2</Color>
  <SolidColorBrush x:Key="Ilu.Brush.Focus" Color="{StaticResource Ilu.Color.Focus}"/>
  <Color x:Key="Ilu.Color.MarginOverlay">#1F3274D9</Color>
  <SolidColorBrush x:Key="Ilu.Brush.MarginOverlay" Color="{StaticResource Ilu.Color.MarginOverlay}"/>
  <Color x:Key="Ilu.Color.ScriptPaper">#000000</Color>
  <SolidColorBrush x:Key="Ilu.Brush.ScriptPaper" Color="{StaticResource Ilu.Color.ScriptPaper}"/>
  <Color x:Key="Ilu.Color.ScriptInk">#F2F2F2</Color>
  <SolidColorBrush x:Key="Ilu.Brush.ScriptInk" Color="{StaticResource Ilu.Color.ScriptInk}"/>
  <Color x:Key="Ilu.Color.ReadingMarker">#8F8F9C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.ReadingMarker" Color="{StaticResource Ilu.Color.ReadingMarker}"/>
  <Color x:Key="Ilu.Color.EndMarker">#8F8F9C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.EndMarker" Color="{StaticResource Ilu.Color.EndMarker}"/>
  <Color x:Key="Ilu.Color.InkWhite">#F2F2F2</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkWhite" Color="{StaticResource Ilu.Color.InkWhite}"/>
  <Color x:Key="Ilu.Color.InkYellow">#FFE45C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkYellow" Color="{StaticResource Ilu.Color.InkYellow}"/>
  <Color x:Key="Ilu.Color.InkAmber">#FFB547</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkAmber" Color="{StaticResource Ilu.Color.InkAmber}"/>
  <Color x:Key="Ilu.Color.InkGreen">#3EE0A4</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkGreen" Color="{StaticResource Ilu.Color.InkGreen}"/>
  <Color x:Key="Ilu.Color.InkCyan">#6FD6FF</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkCyan" Color="{StaticResource Ilu.Color.InkCyan}"/>
  <Color x:Key="Ilu.Color.InkPink">#FF8FB8</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkPink" Color="{StaticResource Ilu.Color.InkPink}"/>
  <Color x:Key="Ilu.Color.InkRed">#FF6B6B</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkRed" Color="{StaticResource Ilu.Color.InkRed}"/>
  <Color x:Key="Ilu.Color.InkGrey">#8F8F9C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.InkGrey" Color="{StaticResource Ilu.Color.InkGrey}"/>
  <Color x:Key="Ilu.Color.HighlightYellow">#FFE45C</Color>
  <SolidColorBrush x:Key="Ilu.Brush.HighlightYellow" Color="{StaticResource Ilu.Color.HighlightYellow}"/>
  <Color x:Key="Ilu.Color.HighlightBlue">#1F4FA0</Color>
  <SolidColorBrush x:Key="Ilu.Brush.HighlightBlue" Color="{StaticResource Ilu.Color.HighlightBlue}"/>
  <Color x:Key="Ilu.Color.HighlightGreen">#1E6B50</Color>
  <SolidColorBrush x:Key="Ilu.Brush.HighlightGreen" Color="{StaticResource Ilu.Color.HighlightGreen}"/>
  <Color x:Key="Ilu.Color.HighlightMagenta">#7A2160</Color>
  <SolidColorBrush x:Key="Ilu.Brush.HighlightMagenta" Color="{StaticResource Ilu.Color.HighlightMagenta}"/>
  <Color x:Key="Ilu.Color.HighlightGrey">#3D434D</Color>
  <SolidColorBrush x:Key="Ilu.Brush.HighlightGrey" Color="{StaticResource Ilu.Color.HighlightGrey}"/>
  <!-- Fonts (Poppins / DM Mono bundled in IluPrompter.Theme as Resource; add Poppins-Bold/Italic TTFs to assets/fonts and they are picked up) -->
  <FontFamily x:Key="Ilu.Font.Ui">pack://application:,,,/IluPrompter.Theme;component/Fonts/#Poppins, Segoe UI</FontFamily>
  <FontFamily x:Key="Ilu.Font.Mono">pack://application:,,,/IluPrompter.Theme;component/Fonts/#DM Mono, Cascadia Mono, Consolas</FontFamily>
  <FontFamily x:Key="Ilu.Font.Icon">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>
  <!-- Sizes (DIP) -->
  <sys:Double x:Key="Ilu.Size.S1">4</sys:Double>
  <sys:Double x:Key="Ilu.Size.S2">8</sys:Double>
  <sys:Double x:Key="Ilu.Size.S3">12</sys:Double>
  <sys:Double x:Key="Ilu.Size.S4">16</sys:Double>
  <sys:Double x:Key="Ilu.Size.S5">24</sys:Double>
  <sys:Double x:Key="Ilu.Size.S6">32</sys:Double>
  <sys:Double x:Key="Ilu.Size.RegionGap">8</sys:Double>
  <sys:Double x:Key="Ilu.Size.WindowPad">8</sys:Double>
  <sys:Double x:Key="Ilu.Size.PanelPad">12</sys:Double>
  <sys:Double x:Key="Ilu.Size.PanelTitleBar">36</sys:Double>
  <sys:Double x:Key="Ilu.Size.AppBar">44</sys:Double>
  <sys:Double x:Key="Ilu.Size.StatusBar">28</sys:Double>
  <sys:Double x:Key="Ilu.Size.ControlPrimary">52</sys:Double>
  <sys:Double x:Key="Ilu.Size.ControlSecondary">44</sys:Double>
  <sys:Double x:Key="Ilu.Size.ControlInput">32</sys:Double>
  <sys:Double x:Key="Ilu.Size.ControlToolbar">32</sys:Double>
  <sys:Double x:Key="Ilu.Size.Segmented">36</sys:Double>
  <sys:Double x:Key="Ilu.Size.Chip">28</sys:Double>
  <sys:Double x:Key="Ilu.Size.ListRow">32</sys:Double>
  <sys:Double x:Key="Ilu.Size.TargetGap">8</sys:Double>
  <sys:Double x:Key="Ilu.Size.PlayGap">12</sys:Double>
  <sys:Double x:Key="Ilu.Size.SectionsWidth">240</sys:Double>
  <sys:Double x:Key="Ilu.Size.LiveColumnWide">720</sys:Double>
  <sys:Double x:Key="Ilu.Size.LiveColumnCompact">480</sys:Double>
  <sys:Double x:Key="Ilu.Size.LiveColumnNarrow">440</sys:Double>
  <sys:Double x:Key="Ilu.Size.EditorPaperPad">24</sys:Double>
  <sys:Double x:Key="Ilu.Size.MinWindowWidth">1024</sys:Double>
  <sys:Double x:Key="Ilu.Size.MinWindowHeight">600</sys:Double>
  <!-- Radii -->
  <CornerRadius x:Key="Ilu.Radius.None">0</CornerRadius>
  <CornerRadius x:Key="Ilu.Radius.Chip">2</CornerRadius>
  <CornerRadius x:Key="Ilu.Radius.Panel">3</CornerRadius>
  <CornerRadius x:Key="Ilu.Radius.Control">4</CornerRadius>
  <CornerRadius x:Key="Ilu.Radius.Button">6</CornerRadius>
  <!-- Padding -->
  <Thickness x:Key="Ilu.Pad.Panel">12</Thickness>
  <Thickness x:Key="Ilu.Pad.Window">8</Thickness>
  <Thickness x:Key="Ilu.Pad.EditorPaper">24</Thickness>
  <!-- Motion (DESIGN.md Motion table) -->
  <Duration x:Key="Ilu.Motion.Ui">0:0:0</Duration>
  <Duration x:Key="Ilu.Motion.RampStart">0:0:0.400</Duration>
  <Duration x:Key="Ilu.Motion.RampStop">0:0:0.200</Duration>
  <Duration x:Key="Ilu.Motion.RampChange">0:0:0.300</Duration>
  <Duration x:Key="Ilu.Motion.LineStep">0:0:0.220</Duration>
  <Duration x:Key="Ilu.Motion.ParagraphStep">0:0:0.320</Duration>
  <Duration x:Key="Ilu.Motion.Jump">0:0:0</Duration>
</ResourceDictionary>
```

`src/IluPrompter.Theme/Themes/IluControls.xaml`

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="/IluPrompter.Theme;component/Themes/IluTokens.xaml"/>
  </ResourceDictionary.MergedDictionaries>
  <!-- Stream D: Ilu.Text.* TextBlock styles and every control template (buttons, chips, segmented, inputs,
       menus, tooltips, scroll bars, check boxes) per DESIGN.md. No hex literals here: use Ilu.Color/Ilu.Brush keys. -->
</ResourceDictionary>
```

`src/IluPrompter.Documents/IluPrompter.Documents.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <UseWPF>true</UseWPF>
    <RootNamespace>IluPrompter.Documents</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\IluPrompter.Theme\IluPrompter.Theme.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="DocumentFormat.OpenXml" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.Documents.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Documents/Layout/DocumentNavigator.cs`

```csharp
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Top-level and nested paragraphs; no END-tag skipping.</summary>
public sealed class DocumentNavigator : IDocumentNavigator
{
    public IReadOnlyList<Paragraph> GetParagraphs(FlowDocument document)
    {
        var list = new List<Paragraph>();
        Walk(document.Blocks, list);
        return list;
    }

    public string GetText(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n');

    public DocumentAnchor GetAnchor(FlowDocument document, TextPointer pointer)
    {
        var paragraphs = GetParagraphs(document);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (paragraphs[i].ContentEnd.CompareTo(pointer) >= 0)
                return new DocumentAnchor(i, Math.Max(0, new TextRange(paragraphs[i].ContentStart, pointer).Text.Length));
        }

        return new DocumentAnchor(Math.Max(0, paragraphs.Count - 1), 0);
    }

    public TextPointer GetPointer(FlowDocument document, DocumentAnchor anchor)
    {
        var paragraphs = GetParagraphs(document);
        if (paragraphs.Count == 0) return document.ContentStart;
        return paragraphs[Math.Clamp(anchor.ParagraphIndex, 0, paragraphs.Count - 1)].ContentStart;
    }

    private static void Walk(IEnumerable<Block> blocks, List<Paragraph> into)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph p: into.Add(p); break;
                case Section s: Walk(s.Blocks, into); break;
                case List l: foreach (var item in l.ListItems) Walk(item.Blocks, into); break;
                case Table t:
                    foreach (var group in t.RowGroups)
                        foreach (var row in group.Rows)
                            foreach (var cell in row.Cells) Walk(cell.Blocks, into);
                    break;
            }
        }
    }
}
```

`src/IluPrompter.Documents/Layout/SectionParser.cs`

```csharp
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Basic prefix match.</summary>
public sealed class SectionParser : ISectionParser
{
    public bool TryParse(string paragraphText, string marker, out string name)
    {
        var t = paragraphText.TrimStart();
        if (marker.Length > 0 && t.StartsWith(marker, StringComparison.Ordinal))
        {
            name = t[marker.Length..].TrimStart('#', ' ', '\t').Trim();
            return true;
        }

        name = "";
        return false;
    }

    public IReadOnlyList<SectionInfo> Parse(IReadOnlyList<string> paragraphTexts, string marker)
    {
        var result = new List<SectionInfo>();
        for (var i = 0; i < paragraphTexts.Count; i++)
        {
            if (TryParse(paragraphTexts[i], marker, out var name))
                result.Add(new SectionInfo(result.Count, name.Length == 0 ? $"Section {result.Count + 1}" : name, i));
        }

        return result;
    }
}
```

`src/IluPrompter.Documents/Layout/OutputDocumentFactory.cs`

```csharp
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Loads the XAML and applies base font/ink only; no size scaling,
/// no section restyling, no END block.</summary>
public sealed class OutputDocumentFactory : IOutputDocumentFactory
{
    private readonly ISnapshotBuilder _snapshots;

    public OutputDocumentFactory(ISnapshotBuilder snapshots) => _snapshots = snapshots;

    public FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            FontSize = style.BaseFontSize,
            Foreground = new SolidColorBrush(style.Ink),
            Background = Brushes.Transparent,
            LineHeight = style.BaseFontSize * style.LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = style.Alignment,
        };
        _snapshots.LoadInto(doc, snapshot.FlowDocumentXaml);
        return doc;
    }
}
```

`src/IluPrompter.Documents/Layout/LayoutGeometryBuilder.cs`

```csharp
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). One "line" per paragraph, no END detection.</summary>
public sealed class LayoutGeometryBuilder : ILayoutGeometryBuilder
{
    private readonly IDocumentNavigator _navigator;

    public LayoutGeometryBuilder(IDocumentNavigator navigator) => _navigator = navigator;

    public LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options)
    {
        var paragraphs = _navigator.GetParagraphs(laidOutDocument);
        var tops = new double[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var rect = paragraphs[i].ContentStart.GetCharacterRect(LogicalDirection.Forward);
            tops[i] = rect.IsEmpty ? i * options.BaseLineHeight : rect.Top;
        }

        var heights = Enumerable.Repeat(options.BaseLineHeight, tops.Length).ToArray();
        var lineParagraphs = Enumerable.Range(0, tops.Length).ToArray();
        var end = tops.Length == 0 ? 0 : tops[^1] + options.BaseLineHeight;
        var sections = options.Snapshot.Sections
            .Where(s => s.ParagraphIndex < tops.Length)
            .Select(s => new SectionMarker(s.Index, s.Name, s.ParagraphIndex, tops[s.ParagraphIndex]))
            .ToList();
        return new LayoutGeometry(options.LayoutKey, options.Snapshot.Version, options.BaseLineHeight, end, end,
            tops, heights, lineParagraphs, new int[tops.Length], (double[])tops.Clone(), sections, options.Snapshot.WordCount);
    }
}
```

`src/IluPrompter.Documents/Storage/SnapshotBuilder.cs`

```csharp
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). No flattening of lists/tables.</summary>
public sealed class SnapshotBuilder : ISnapshotBuilder
{
    private readonly IDocumentNavigator _navigator;
    private readonly ISectionParser _sections;

    public SnapshotBuilder(IDocumentNavigator navigator, ISectionParser sections)
    {
        _navigator = navigator;
        _sections = sections;
    }

    public PromptDocumentSnapshot Build(FlowDocument editorDocument, long version, double editorBaseFontSize, string sectionMarker)
    {
        var texts = _navigator.GetParagraphs(editorDocument).Select(_navigator.GetText).ToList();
        var words = texts.Sum(t => t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
        return new PromptDocumentSnapshot(version, SaveXaml(editorDocument), editorBaseFontSize, texts.Count, _sections.Parse(texts, sectionMarker), words);
    }

    public ScriptFile ToScriptFile(FlowDocument editorDocument, string title, double editorBaseFontSize, ScriptOverrides? overrides) =>
        new() { Title = title, DocumentXaml = SaveXaml(editorDocument), EditorBaseFontSize = editorBaseFontSize, Overrides = overrides };

    public void LoadInto(FlowDocument target, string documentXaml)
    {
        target.Blocks.Clear();
        if (string.IsNullOrEmpty(documentXaml))
        {
            target.Blocks.Add(new Paragraph());
            return;
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(documentXaml));
        new TextRange(target.ContentStart, target.ContentEnd).Load(stream, DataFormats.Xaml);
    }

    internal static string SaveXaml(FlowDocument document)
    {
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Xaml);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
```

`src/IluPrompter.Documents/Storage/ScriptFileService.cs`

```csharp
using System.IO;
using System.Text.Json;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). .ilup load/save and plain .txt import only.</summary>
public sealed class ScriptFileService : IScriptFileService
{
    public IReadOnlyList<string> OpenExtensions { get; } = [".ilup", ".rtf", ".txt", ".docx"];

    public IReadOnlyList<string> ExportExtensions { get; } = [".rtf", ".txt"];

    public ScriptFile Load(string path)
    {
        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ilup":
                    return JsonSerializer.Deserialize<ScriptFile>(File.ReadAllText(path), IluJson.Options)
                        ?? throw new ScriptFileException("The script file is empty.");
                case ".txt":
                    var doc = new FlowDocument();
                    foreach (var line in File.ReadAllLines(path)) doc.Blocks.Add(new Paragraph(new Run(line)));
                    return new ScriptFile { Title = Path.GetFileNameWithoutExtension(path), DocumentXaml = SnapshotBuilder.SaveXaml(doc) };
                default:
                    throw new ScriptFileException("This file type can't be opened yet.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ScriptFileException("The script could not be opened: " + ex.Message, ex);
        }
    }

    public void Save(string path, ScriptFile file) =>
        File.WriteAllText(path, JsonSerializer.Serialize(file, IluJson.Options));

    public void Export(string path, FlowDocument document) =>
        throw new ScriptFileException("Export is not available yet.");
}
```

`src/IluPrompter.Documents/Editing/ScriptFormatter.cs`

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Minimal, non-throwing behaviour.</summary>
public sealed class ScriptFormatter : IScriptFormatter
{
    public double Factor(ScriptSize size) => size switch
    {
        ScriptSize.Small => 0.80,
        ScriptSize.Large => 1.25,
        ScriptSize.Huge => 1.50,
        _ => 1.00,
    };

    public void ApplySize(TextRange range, ScriptSize size, double editorBaseFontSize) =>
        range.ApplyPropertyValue(TextElement.FontSizeProperty, editorBaseFontSize * Factor(size));

    public ScriptSize? GetSize(TextRange range, double editorBaseFontSize) => ScriptSize.Normal;

    public void ToggleStrikethrough(TextRange range) =>
        range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);

    public bool? GetStrikethrough(TextRange range) => false;

    public void ApplyInk(TextRange range, Color? ink) =>
        range.ApplyPropertyValue(TextElement.ForegroundProperty, ink is { } c ? new SolidColorBrush(c) : null);

    public void ApplyHighlight(TextRange range, Color? highlight, Color? highlightInk) =>
        range.ApplyPropertyValue(TextElement.BackgroundProperty, highlight is { } c ? new SolidColorBrush(c) : null);

    public void ClearFormatting(TextRange range) => range.ClearAllProperties();

    public Paragraph InsertSectionMarker(RichTextBox editor, string marker, string name)
    {
        var paragraph = new Paragraph(new Run($"{marker} {name}"));
        editor.Document.Blocks.Add(paragraph);
        return paragraph;
    }

    public void StyleSectionParagraphs(FlowDocument document, string marker, Brush labelBrush, Brush ruleBrush)
    {
    }
}
```

`src/IluPrompter.Documents/DocumentsServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Documents;

/// <summary>Stream E owns this file.</summary>
public static class DocumentsServiceCollectionExtensions
{
    public static IServiceCollection AddIluDocuments(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentNavigator, DocumentNavigator>();
        services.AddSingleton<ISectionParser, SectionParser>();
        services.AddSingleton<ISnapshotBuilder, SnapshotBuilder>();
        services.AddSingleton<IScriptFileService, ScriptFileService>();
        services.AddSingleton<IOutputDocumentFactory, OutputDocumentFactory>();
        services.AddSingleton<ILayoutGeometryBuilder, LayoutGeometryBuilder>();
        services.AddSingleton<IScriptFormatter, ScriptFormatter>();
        return services;
    }
}
```

`src/IluPrompter.Input/IluPrompter.Input.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <UseWPF>true</UseWPF>
    <RootNamespace>IluPrompter.Input</RootNamespace>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\IluPrompter.Core\IluPrompter.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Vortice.XInput" />
    <PackageReference Include="HidSharp" />
    <PackageReference Include="RawInput.Sharp" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.Input.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Input/AppWindow/AppInputBridge.cs`

```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>STUB (stream B replaces the body). Keyboard only, no typing-context detection, no wheel.</summary>
public sealed class AppInputBridge : IAppInputBridge
{
    private static readonly InputSourceId Source = new(ControllerKind.Keyboard, AppSources.App);
    private readonly IInputSink _sink;

    public AppInputBridge(IInputSink sink) => _sink = sink;

    public void AttachWindow(Window window) => window.PreviewKeyDown += OnPreviewKeyDown;

    public void DetachWindow(Window window) => window.PreviewKeyDown -= OnPreviewKeyDown;

    public void AttachWheelArea(UIElement element)
    {
    }

    public void DetachWheelArea(UIElement element)
    {
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var m = Keyboard.Modifiers;
        var chord = ControlIds.KeyChord(m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Alt), m.HasFlag(ModifierKeys.Shift), key.ToString());
        e.Handled = _sink.Submit(new ButtonInput(Source, chord, Stopwatch.GetTimestamp(), true, e.IsRepeat));
    }
}
```

`src/IluPrompter.Input/Providers.Stubs.cs`

```csharp
using IluPrompter.Core;

namespace IluPrompter.Input;

// STUBS (stream B replaces each class with a real implementation in its own file:
// RawInput/RawMouseWheelProvider.cs, XInput/XInputProvider.cs, Shuttle/ContourShuttleProvider.cs,
// and deletes this file). Keep the class names and constructor signatures.

public sealed class RawMouseWheelProvider : IScrollInputProvider
{
    public RawMouseWheelProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.MouseWheel;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) { }
    public void Stop() { }
    public void Dispose() { }
}

public sealed class XInputProvider : IScrollInputProvider
{
    public XInputProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.Xbox;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) =>
        sink.ReportStatus(new DeviceStatus(new InputSourceId(Kind, "xinput:0"), "Xbox", DeviceConnection.NotFound));
    public void Stop() { }
    public void Dispose() { }
}

public sealed class ContourShuttleProvider : IScrollInputProvider
{
    public ContourShuttleProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.JogShuttle;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) =>
        sink.ReportStatus(new DeviceStatus(new InputSourceId(Kind, ""), "Shuttle", DeviceConnection.NotFound));
    public void Stop() { }
    public void Dispose() { }
}
```

`src/IluPrompter.Input/InputServiceCollectionExtensions.cs`

```csharp
using IluPrompter.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Input;

/// <summary>Stream B owns this file. Providers are registered as IScrollInputProvider (the router receives all).</summary>
public static class InputServiceCollectionExtensions
{
    public static IServiceCollection AddIluInput(this IServiceCollection services)
    {
        services.AddSingleton<IScrollInputProvider, RawMouseWheelProvider>();
        services.AddSingleton<IScrollInputProvider, XInputProvider>();
        services.AddSingleton<IScrollInputProvider, ContourShuttleProvider>();
        services.AddSingleton<IAppInputBridge, AppInputBridge>();
        return services;
    }
}
```

`src/IluPrompter.Rendering/IluPrompter.Rendering.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <UseWPF>true</UseWPF>
    <RootNamespace>IluPrompter.Rendering</RootNamespace>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\IluPrompter.Documents\IluPrompter.Documents.csproj" />
    <ProjectReference Include="..\IluPrompter.Theme\IluPrompter.Theme.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <AdditionalFiles Include="NativeMethods.txt" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.Rendering.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.Rendering/NativeMethods.txt`

```text
SetWindowPos
GetWindowLong
SetWindowLong
EnumDisplayMonitors
GetMonitorInfo
MONITORINFOEXW
GetDpiForMonitor
QueryDisplayConfig
GetDisplayConfigBufferSizes
DisplayConfigGetDeviceInfo
DISPLAYCONFIG_TARGET_DEVICE_NAME
DISPLAYCONFIG_SOURCE_DEVICE_NAME
SetThreadExecutionState
DwmSetWindowAttribute
WINDOW_EX_STYLE
```

`src/IluPrompter.Rendering/Output/OutputHost.cs`

```csharp
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>
/// IOutputSurface implementation: owns the output STA thread, its Dispatcher and the OutputWindow.
/// STUB (stream C replaces the body; keep the type name and constructor signature): no thread, no window.
/// </summary>
public sealed class OutputHost : IOutputSurface
{
    private readonly IMonitorService _monitors;
    private OutputState _state;

    public OutputHost(
        IScrollEngine engine,
        IPromptDocumentService documents,
        ISettingsService settings,
        IMonitorService monitors,
        IOutputDocumentFactory documentFactory,
        ILayoutGeometryBuilder geometryBuilder,
        IAppLog log)
    {
        _monitors = monitors;
        _state = OutputState.Initial with { Geometry = settings.Current.Output.LastGeometry };
    }

    public OutputState State => Volatile.Read(ref _state);

    public event EventHandler<OutputState>? StateChanged;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ShowAsync()
    {
        var monitor = _monitors.ResolveOutputMonitor(null);
        Set(State with { IsOpen = monitor is not null, Monitor = monitor, Display = monitor is null ? OutputDisplayStatus.NoSecondDisplay : OutputDisplayStatus.Ready });
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        Set(State with { IsOpen = false });
        return Task.CompletedTask;
    }

    public void SetBlank(bool blank) => Set(State with { IsBlank = blank });

    public Task ShutdownAsync() => Task.CompletedTask;

    private void Set(OutputState s)
    {
        Volatile.Write(ref _state, s);
        StateChanged?.Invoke(this, s);
    }
}
```

`src/IluPrompter.Rendering/Monitors/MonitorService.cs`

```csharp
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>STUB (stream C replaces the body; keep the type name and constructor signature). Reports no displays.</summary>
public sealed class MonitorService : IMonitorService
{
    public MonitorService(IAppLog log)
    {
    }

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors() => Array.Empty<MonitorInfo>();

    public MonitorInfo? ResolveOutputMonitor(string? savedStableId) => null;

    internal void RaiseChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);
}
```

`src/IluPrompter.Rendering/RenderingServiceCollectionExtensions.cs`

```csharp
using IluPrompter.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Rendering;

/// <summary>Stream C owns this file.</summary>
public static class RenderingServiceCollectionExtensions
{
    public static IServiceCollection AddIluRendering(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IOutputSurface, OutputHost>();
        services.AddSingleton<PreviewServices>();
        return services;
    }
}
```

`src/IluPrompter.App/IluPrompter.App.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWPF>true</UseWPF>
    <RootNamespace>IluPrompter.App</RootNamespace>
    <AssemblyName>ILUPrompter</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <!-- Publish: dotnet publish src/IluPrompter.App -c Release -p:SelfContained=true -o publish/ILUPrompter -->
    <PublishSingleFile>false</PublishSingleFile>
    <PublishReadyToRun>false</PublishReadyToRun>
    <InvariantGlobalization>false</InvariantGlobalization>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\IluPrompter.Documents\IluPrompter.Documents.csproj" />
    <ProjectReference Include="..\IluPrompter.Input\IluPrompter.Input.csproj" />
    <ProjectReference Include="..\IluPrompter.Rendering\IluPrompter.Rendering.csproj" />
    <ProjectReference Include="..\IluPrompter.Theme\IluPrompter.Theme.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="CommunityToolkit.Mvvm" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="IluPrompter.App.Tests" />
  </ItemGroup>
</Project>
```

`src/IluPrompter.App/app.manifest`

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ILUPrompter.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- Never elevate. -->
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10 / 11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
</assembly>
```

`src/IluPrompter.App/App.xaml`

```xml
<Application x:Class="IluPrompter.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowClose">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- Theme placeholder: tokens (generated from DESIGN.md) + control styles (stream D). -->
        <ResourceDictionary Source="pack://application:,,,/IluPrompter.Theme;component/Themes/IluTokens.xaml" />
        <ResourceDictionary Source="pack://application:,,,/IluPrompter.Theme;component/Themes/IluControls.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

`src/IluPrompter.App/App.xaml.cs`

```csharp
using System.Windows;
using IluPrompter.App.Composition;
using IluPrompter.App.Views;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Input;
using IluPrompter.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App;

/// <summary>Composition root. Stream D owns this file; the startup order below is part of the contract.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var diagnostics = e.Args.Contains("--diag", StringComparer.OrdinalIgnoreCase);

        _services = new ServiceCollection()
            .AddIluCore(diagnostics)
            .AddIluDocuments()
            .AddIluInput()
            .AddIluRendering()
            .AddIluApp()
            .BuildServiceProvider();

        var log = _services.GetRequiredService<IAppLog>();
        log.Info("app", "Starting ILU Prompter " + typeof(App).Assembly.GetName().Version);
        DispatcherUnhandledException += (_, args) => log.Error("app", "Unhandled UI exception", args.Exception);

        // 1. settings  2. output thread  3. main window (+ input bridge + preview)  4. input router
        _services.GetRequiredService<ISettingsService>().Load();
        await _services.GetRequiredService<IOutputSurface>().InitializeAsync();

        var main = _services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();

        _services.GetRequiredService<IInputRouter>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.GetRequiredService<IInputRouter>().Stop();
            _services.GetRequiredService<IOutputSurface>().ShutdownAsync().GetAwaiter().GetResult();
            _services.GetRequiredService<ISettingsService>().SaveAsync().GetAwaiter().GetResult();
            _services.Dispose();
        }

        base.OnExit(e);
    }
}
```

`src/IluPrompter.App/Composition/AppServiceCollectionExtensions.cs`

```csharp
using IluPrompter.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App.Composition;

/// <summary>Stream D owns this file: windows and view models.</summary>
public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddIluApp(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        return services;
    }
}
```

`src/IluPrompter.App/Views/MainWindow.xaml`

```xml
<Window x:Class="IluPrompter.App.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:r="clr-namespace:IluPrompter.Rendering;assembly=IluPrompter.Rendering"
        Title="ILU Prompter"
        MinWidth="1024" MinHeight="600" Width="1280" Height="800"
        WindowState="Maximized"
        Background="{StaticResource Ilu.Brush.Canvas}"
        Foreground="{StaticResource Ilu.Brush.Text}"
        FontFamily="{StaticResource Ilu.Font.Ui}">
  <!-- SCAFFOLD placeholder layout: stream D replaces with DESIGN.md regions A..G. -->
  <Grid Margin="{StaticResource Ilu.Pad.Window}">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="240" />
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="720" />
    </Grid.ColumnDefinitions>
    <Border Grid.Column="0" Background="{StaticResource Ilu.Brush.Surface}" Margin="0,0,8,0" />
    <RichTextBox x:Name="Editor" Grid.Column="1" Background="{StaticResource Ilu.Brush.ScriptPaper}"
                 Foreground="{StaticResource Ilu.Brush.ScriptInk}" FontSize="20" Margin="0,0,8,0" />
    <r:PrompterPreview x:Name="Preview" Grid.Column="2" Height="392" VerticalAlignment="Top" />
  </Grid>
</Window>
```

`src/IluPrompter.App/Views/MainWindow.xaml.cs`

```csharp
using System.Windows;
using IluPrompter.Input;
using IluPrompter.Rendering;

namespace IluPrompter.App.Views;

/// <summary>SCAFFOLD placeholder; stream D owns and replaces.</summary>
public partial class MainWindow : Window
{
    public MainWindow(IAppInputBridge inputBridge, PreviewServices previewServices)
    {
        InitializeComponent();
        inputBridge.AttachWindow(this);
        inputBridge.AttachWheelArea(Preview);
        Preview.Attach(previewServices);
    }
}
```

`tests/IluPrompter.Core.Tests/IluPrompter.Core.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IluPrompter.Core\IluPrompter.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`tests/IluPrompter.Core.Tests/ScaffoldSmokeTests.cs`

```csharp
using System.Text.Json;
using IluPrompter.Core;

namespace IluPrompter.Core.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void DefaultSettings_RoundTripThroughJson()
    {
        var json = JsonSerializer.Serialize(PrompterSettings.Default, IluJson.Options);
        var back = JsonSerializer.Deserialize<PrompterSettings>(json, IluJson.Options);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, IluJson.Options));
        Assert.Equal(PrompterCommand.PlayPause, back!.Input.Keyboard.Transport["Space"].Display);
    }

    [Fact]
    public void LayoutGeometry_FindsLineAtY()
    {
        var g = new LayoutGeometry("k", 1, 100, 300, 300, [0, 100, 200], [100, 100, 100], [0, 0, 1], [0, 12, 0], [0, 200], Array.Empty<SectionMarker>(), 10);
        Assert.Equal(1, g.FindLineAt(150));
        Assert.Equal(0, g.FindLineAt(-50));
        Assert.Equal(2, g.FindLineForAnchor(new DocumentAnchor(1, 0)));
        Assert.Equal(1, g.FindLineForAnchor(new DocumentAnchor(0, 20)));
    }

    [Fact]
    public void Typography_MatchesDesignExample()
    {
        Assert.Equal(91.54, TypographyMath.BaseFontSize(1080 - 64 - 64, 8, 1.3), 2);
    }
}
```

`tests/IluPrompter.Input.Tests/IluPrompter.Input.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Input\IluPrompter.Input.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`tests/IluPrompter.Input.Tests/ScaffoldSmokeTests.cs`

```csharp
using IluPrompter.Core;

namespace IluPrompter.Input.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void KeyChord_UsesCanonicalModifierOrder() =>
        Assert.Equal("Ctrl+Alt+Shift+PageDown", ControlIds.KeyChord(true, true, true, "PageDown"));
}
```

`tests/IluPrompter.Documents.Tests/IluPrompter.Documents.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <PackageReference Include="Xunit.StaFact" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Theme\IluPrompter.Theme.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Documents\IluPrompter.Documents.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`tests/IluPrompter.Documents.Tests/ScaffoldSmokeTests.cs`

```csharp
using System.Windows.Documents;
using IluPrompter.Documents;

namespace IluPrompter.Documents.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void SectionParser_FindsMarkers()
    {
        var sections = new SectionParser().Parse(["Hello", "## Welcome", "Text", "##"], "##");
        Assert.Equal(2, sections.Count);
        Assert.Equal("Welcome", sections[0].Name);
        Assert.Equal(3, sections[1].ParagraphIndex);
    }

    [WpfFact]
    public void SnapshotBuilder_RoundTripsXaml()
    {
        var doc = new FlowDocument(new Paragraph(new Run("## One")));
        doc.Blocks.Add(new Paragraph(new Bold(new Run("Read this"))));
        var builder = new SnapshotBuilder(new DocumentNavigator(), new SectionParser());
        var snap = builder.Build(doc, 1, 20, "##");
        Assert.Equal(2, snap.ParagraphCount);
        Assert.Single(snap.Sections);
        var copy = new FlowDocument();
        builder.LoadInto(copy, snap.FlowDocumentXaml);
        Assert.Equal(2, new DocumentNavigator().GetParagraphs(copy).Count);
    }
}
```

`tests/IluPrompter.Rendering.Tests/IluPrompter.Rendering.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <PackageReference Include="Xunit.StaFact" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Documents\IluPrompter.Documents.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Theme\IluPrompter.Theme.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Rendering\IluPrompter.Rendering.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`tests/IluPrompter.Rendering.Tests/ScaffoldSmokeTests.cs`

```csharp
using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

public class ScaffoldSmokeTests
{
    [WpfFact]
    public void Preview_CanBeConstructed() => Assert.NotNull(new PrompterPreview());
}
```

`tests/IluPrompter.App.Tests/IluPrompter.App.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseWPF>true</UseWPF>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <PackageReference Include="Xunit.StaFact" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\IluPrompter.Core\IluPrompter.Core.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.Theme\IluPrompter.Theme.csproj" />
    <ProjectReference Include="..\..\src\IluPrompter.App\IluPrompter.App.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`tests/IluPrompter.App.Tests/ScaffoldSmokeTests.cs`

```csharp
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.App.Tests;

public class ScaffoldSmokeTests
{
    [WpfFact]
    public void ThemeTokens_LoadFromResourceDictionary()
    {
        Assert.Equal(Colors.Black, ThemeResources.GetColor("script-paper"));
        Assert.True(ThemeResources.TryGetColor("Ilu.Color.Live", out _));
    }
}
```
