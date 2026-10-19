using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>Which of the four drawings the Controller tab shows (DESIGN.md "Controller drawing").</summary>
public enum ControllerDrawingKind
{
    None = 0,
    Mouse,
    Xbox,
    ShuttleXpress,
    ShuttlePro,
}

/// <summary>
/// How one button-like part is painted. Bound = does something in the current scrolling mode; Lit = a bound control that
/// is pressed or inside its hold; Flash = an unbound control that is pressed or inside its hold (the neutral flash).
/// </summary>
public enum PartLook
{
    Unbound = 0,
    Bound,
    Lit,
    Flash,
}

/// <summary>The fill of an analogue indicator (stick dot, trigger fill, shuttle arc): text, text-muted, line-strong.</summary>
public enum IndicatorLook
{
    Bound = 0,
    Idle,
    Unbound,
}

/// <summary>A stick press lights the ring's stroke only.</summary>
public enum PressLook
{
    None = 0,
    Lit,
    Flash,
}

/// <summary>The states of the Controller tab (DESIGN.md "States" table under "Controller drawing").</summary>
public enum ControllerPanelState
{
    /// <summary>Keyboard only: no drawing, "No controller chosen."</summary>
    NotChosen = 0,

    /// <summary>Chosen but never seen (<see cref="DeviceConnection.NotFound"/>): Missing drawing.</summary>
    NotFound,

    /// <summary>Was connected, stopped answering (<see cref="DeviceConnection.Disconnected"/>): Missing drawing.</summary>
    Lost,

    /// <summary>Connected, nothing has arrived yet: drawing at rest, "No input yet."</summary>
    NoInputYet,

    /// <summary>Connected and in use.</summary>
    InUse,
}

/// <summary>A stick: ring r 18, dashed deadzone ring, dot r 5 that travels 13 (design box units).</summary>
public sealed record StickFrame(string Id, bool Bound, PressLook Press, int DotDx, int DotDy, double DeadzoneRadius, IndicatorLook Dot);

/// <summary>A trigger: a 12 x 60 meter filled from the bottom (FillPx 0 to 60), with a deadzone tick.</summary>
public sealed record TriggerFrame(string Id, bool Bound, int FillPx, double TickOffsetFromTop, IndicatorLook Fill);

/// <summary>The mouse wheel: treads phase 0 to 3, chevrons lit for 400 ms after a notch.</summary>
public sealed record WheelFrame(bool Bound, int TreadPhase, bool UpLit, bool DownLit);

/// <summary>The shuttle ring (position K, -7 to +7) and jog wheel (dimple index 0 to 9).</summary>
public sealed record ShuttleFrame(bool RingBound, int RingK, IndicatorLook RingArc, bool JogBound, int JogDimple, bool JogLit);

/// <summary>
/// Everything the drawing shows at one poll. Immutable; two frames are equal when they look the same
/// (<see cref="Signature"/>), so the shell sets visual properties only when a frame really changed.
/// </summary>
public sealed record ControllerFrame
{
    public ControllerDrawingKind Kind { get; init; }

    /// <summary>The controller is MISSING: no fills, nothing lit, every part dim.</summary>
    public bool Missing { get; init; }

    /// <summary>Button-like parts by ControlId (Middle, XButton1, A, DPadUp, Button7 ...). Sticks and triggers are separate.</summary>
    public IReadOnlyDictionary<string, PartLook> Buttons { get; init; } = new Dictionary<string, PartLook>();

    public IReadOnlyList<StickFrame> Sticks { get; init; } = [];

    public IReadOnlyList<TriggerFrame> Triggers { get; init; } = [];

    public WheelFrame? Wheel { get; init; }

    public ShuttleFrame? Shuttle { get; init; }

    public static ControllerFrame Empty { get; } = new();

    /// <summary>A stable text form of everything above; equal frames have equal signatures.</summary>
    public string Signature { get; init; } = "";

    public bool Equals(ControllerFrame? other) => other is not null && string.Equals(Signature, other.Signature, StringComparison.Ordinal);

    public override int GetHashCode() => Signature.GetHashCode(StringComparison.Ordinal);

    public PartLook Look(string id) => Buttons.TryGetValue(id, out var look) ? look : PartLook.Unbound;
}

/// <summary>One 22 px row of the info column: [control] [what it does] [value].</summary>
public sealed record ControllerRow(string Control, string Action, string Value, bool Lit);

/// <summary>The Xbox battery glyph (DESIGN.md: three segments Full, Medium, Low, Empty, or the word Wired).</summary>
public enum BatteryGlyph
{
    None = 0,
    Full,
    Medium,
    Low,
    Empty,
    Wired,
}

/// <summary>The whole Controller tab at one poll: the drawing plus every word beside it.</summary>
public sealed record ControllerPanelModel
{
    public ControllerPanelState State { get; init; }

    public ControllerFrame Frame { get; init; } = ControllerFrame.Empty;

    /// <summary>The device line: "Xbox controller · player 1".</summary>
    public string DeviceLine { get; init; } = "";

    /// <summary>The detail line (large body only): "Over the preview only", "Contour ShuttlePRO v2".</summary>
    public string DetailLine { get; init; } = "";

    public BatteryGlyph Battery { get; init; }

    public string BatteryText { get; init; } = "";

    /// <summary>The chip shown in the Missing states (D1's controller chip, exactly).</summary>
    public ChipModel? Chip { get; init; }

    /// <summary>Persistent rows first, then up to the transient cap of lit buttons.</summary>
    public IReadOnlyList<ControllerRow> Rows { get; init; } = [];

    /// <summary>The body-text lines for the Missing and NotChosen states (first line, then the keyboard sentence).</summary>
    public string MessageTitle { get; init; } = "";

    public string MessageDetail { get; init; } = "";

    /// <summary>"Turn the wheel over the preview." and so on, shown with "No input yet.".</summary>
    public string Hint { get; init; } = "";

    /// <summary>The panel's last-input line: "Last: A · Play / pause", or "" before any input.</summary>
    public string LastText { get; init; } = "";

    /// <summary>The same line for the tab bar: "Last: A · Play / pause", "No input yet", "Xbox missing", "No controller".</summary>
    public string BarText { get; init; } = "";

    public string Signature { get; init; } = "";

    public bool Equals(ControllerPanelModel? other) => other is not null && string.Equals(Signature, other.Signature, StringComparison.Ordinal);

    public override int GetHashCode() => Signature.GetHashCode(StringComparison.Ordinal);

    public static ControllerPanelModel Empty { get; } = new();
}
