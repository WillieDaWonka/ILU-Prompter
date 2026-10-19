using System.Globalization;
using IluPrompter.Core;
using Vortice.XInput;
using CoreBatteryLevel = IluPrompter.Core.BatteryLevel;
using XBatteryLevel = Vortice.XInput.BatteryLevel;

namespace IluPrompter.Input;

/// <summary>Pure XInput conversions (contract §2.3 normalisation table). Touches no device.</summary>
internal static class XInputNormaliser
{
    /// <summary>An axis event is raised when the value moves by more than this.</summary>
    public const double AxisChangeThreshold = 0.002;

    public const int SlotCount = 4;

    /// <summary>Largest number of edges one comparison can produce.</summary>
    public const int MaxEdges = 14;

    /// <summary>The six analogue controls, in the order the provider reads them.</summary>
    public static readonly string[] AxisIds =
    {
        ControlIds.LeftThumbX, ControlIds.LeftThumbY,
        ControlIds.RightThumbX, ControlIds.RightThumbY,
        ControlIds.LeftTrigger, ControlIds.RightTrigger,
    };

    /// <summary>Every readable button and the canonical id it reports under. (Guide is not exposed by the
    /// documented XInput API.) View is Back and Menu is Start, as in <see cref="ControlIds"/>.</summary>
    public static readonly (GamepadButtons Flag, string Id)[] ButtonTable =
    {
        (GamepadButtons.A, ControlIds.A),
        (GamepadButtons.B, ControlIds.B),
        (GamepadButtons.X, ControlIds.X),
        (GamepadButtons.Y, ControlIds.Y),
        (GamepadButtons.DPadUp, ControlIds.DPadUp),
        (GamepadButtons.DPadDown, ControlIds.DPadDown),
        (GamepadButtons.DPadLeft, ControlIds.DPadLeft),
        (GamepadButtons.DPadRight, ControlIds.DPadRight),
        (GamepadButtons.LeftShoulder, ControlIds.LeftShoulder),
        (GamepadButtons.RightShoulder, ControlIds.RightShoulder),
        (GamepadButtons.Back, ControlIds.View),
        (GamepadButtons.Start, ControlIds.Menu),
        (GamepadButtons.LeftThumb, ControlIds.LeftThumbPress),
        (GamepadButtons.RightThumb, ControlIds.RightThumbPress),
    };

    /// <summary>Stick position: raw / 32767 clamped to -1..1 (so -32768 is -1).</summary>
    public static double Thumb(short raw) => Math.Clamp(raw / 32767.0, -1.0, 1.0);

    /// <summary>Trigger position: raw / 255, 0..1.</summary>
    public static double Trigger(byte raw) => raw / 255.0;

    /// <summary>
    /// Whether to raise a new axis event. True when the value moved by more than <see cref="AxisChangeThreshold"/>,
    /// and also when it returns to exactly zero (so a released stick or trigger never keeps a tiny residue).
    /// </summary>
    public static bool AxisChanged(double last, double current) =>
        Math.Abs(current - last) > AxisChangeThreshold || (current == 0.0 && last != 0.0);

    /// <summary>Reads the six axis values of a pad state in <see cref="AxisIds"/> order.</summary>
    public static void ReadAxes(in Gamepad pad, Span<double> values)
    {
        values[0] = Thumb(pad.LeftThumbX);
        values[1] = Thumb(pad.LeftThumbY);
        values[2] = Thumb(pad.RightThumbX);
        values[3] = Thumb(pad.RightThumbY);
        values[4] = Trigger(pad.LeftTrigger);
        values[5] = Trigger(pad.RightTrigger);
    }

    /// <summary>Writes the button edges between two button masks and returns the count (allocation free).</summary>
    public static int DiffButtons(GamepadButtons previous, GamepadButtons current, Span<ButtonEdge> edges)
    {
        var n = 0;
        foreach (var (flag, id) in ButtonTable)
        {
            var was = (previous & flag) != 0;
            var isDown = (current & flag) != 0;
            if (was != isDown)
            {
                edges[n++] = new ButtonEdge(id, isDown);
            }
        }

        return n;
    }

    public static string DeviceId(int index) => "xinput:" + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses "xinput:N" (N 0..3). Null, blank or malformed ids give -1 = "first connected pad".</summary>
    public static int ParseDeviceId(string? deviceId)
    {
        const string prefix = "xinput:";
        if (deviceId is not null
            && deviceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(deviceId.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n is >= 0 and < SlotCount)
        {
            return n;
        }

        return -1;
    }

    public static string DisplayName(int index) =>
        "Xbox controller " + (index + 1).ToString(CultureInfo.InvariantCulture);

    public static CoreBatteryLevel Battery(BatteryType type, XBatteryLevel level) => type switch
    {
        BatteryType.Wired => CoreBatteryLevel.Wired,
        BatteryType.Disconnected or BatteryType.Unknown => CoreBatteryLevel.Unknown,
        _ => level switch
        {
            XBatteryLevel.Empty => CoreBatteryLevel.Empty,
            XBatteryLevel.Low => CoreBatteryLevel.Low,
            XBatteryLevel.Medium => CoreBatteryLevel.Medium,
            XBatteryLevel.Full => CoreBatteryLevel.Full,
            _ => CoreBatteryLevel.Unknown,
        },
    };
}
