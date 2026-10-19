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
