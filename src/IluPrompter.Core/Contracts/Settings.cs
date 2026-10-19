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

/// <summary>The two tabs of the control window's live panel (DESIGN.md D7). Stored by name.</summary>
public enum LivePanelTab
{
    Controller = 0,
    OutputLayout = 1,
}

public sealed record WindowSettings
{
    public double Left { get; init; } = double.NaN;
    public double Top { get; init; } = double.NaN;
    public double Width { get; init; } = 1280;
    public double Height { get; init; } = 800;
    public bool IsMaximized { get; init; } = true;

    /// <summary>The tab the live panel was left on (added with the live panel; old files load as Controller).</summary>
    public LivePanelTab LivePanelTab { get; init; } = LivePanelTab.Controller;

    /// <summary>False when the live panel was hidden (added with the live panel; old files load as open). An inline
    /// body follows it at launch; the flyout form ignores it at launch but writes it when opened or hidden.</summary>
    public bool LivePanelOpen { get; init; } = true;
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
