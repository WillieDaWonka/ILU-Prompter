using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>Chip fill, by the house state names (Tag values in the theme: ok, live, idle, active, attention, fault).</summary>
public enum ChipKind
{
    Ok,
    Live,
    Idle,
    Active,
    Attention,
    Fault,
}

/// <summary>One state chip (DESIGN.md "State chip"): glyph, subject, state word in capitals, tooltip.</summary>
public sealed record ChipModel(ChipKind Kind, string Glyph, string Subject, string State, string Tooltip)
{
    /// <summary>The theme's Tag value for this chip's fill.</summary>
    public string StyleTag => Kind.ToString().ToLowerInvariant();

    /// <summary>Screen-reader sentence: the whole chip, then what it means.</summary>
    public string Automation => $"{Subject}, {State}. {Tooltip}";
}

/// <summary>What the output chip needs besides <see cref="OutputState"/>.</summary>
public readonly record struct OutputChipContext(bool UserPressedShow, bool SecondDisplayAvailable, bool Duplicated, string? DisplayName);

/// <summary>
/// Maps engine and device state to the two chips of the live column. Pure; every row of DESIGN.md's two chip tables
/// is covered by a test. Glyphs are the DESIGN.md shapes, set in a text symbol font by the theme.
/// </summary>
public static class ChipMapper
{
    public const string GlyphOff = "○";      // ○
    public const string GlyphLive = "▶";     // ▶
    public const string GlyphBlank = "■";    // ■
    public const string GlyphCaution = "▲";  // ▲
    public const string GlyphFault = "✕";    // ✕
    public const string GlyphOk = "✓";       // ✓

    public const string KeyboardStillWorks = "The keyboard still works: Space plays and pauses.";

    public static ChipModel Output(OutputState state, OutputChipContext context)
    {
        var name = state.Monitor is { } m ? $"Display {m.DisplayNumber}" : context.DisplayName ?? "Output";
        var displayName = state.Monitor is { } mm ? $"Display {mm.DisplayNumber}" : context.DisplayName ?? "Display 2";

        // The chosen display vanished while the output was open: it waits, hidden, on the same display.
        if (state.IsOpen && state.Display == OutputDisplayStatus.Missing)
            return new ChipModel(ChipKind.Fault, GlyphFault, name, "NO DISPLAY",
                $"{displayName} disconnected. The output will come back when it does.");

        if (state.Display == OutputDisplayStatus.Duplicated || (!state.IsOpen && context.Duplicated))
            return new ChipModel(ChipKind.Attention, GlyphCaution, name, "DUPLICATED",
                "Windows is copying your laptop screen. Set Windows › Display › Extend these displays (Win+P › Extend).");

        if (!state.IsOpen)
        {
            if (!context.SecondDisplayAvailable || (state.Display == OutputDisplayStatus.NoSecondDisplay && context.UserPressedShow))
            {
                return new ChipModel(context.UserPressedShow ? ChipKind.Attention : ChipKind.Idle,
                    context.UserPressedShow ? GlyphCaution : GlyphOff, "Output", "NO 2ND DISPLAY",
                    "Connect the presenter's display, then press F11.");
            }

            return new ChipModel(ChipKind.Idle, GlyphOff, "Output", "OFF", $"Press F11 to show the script on {displayName}.");
        }

        if (state.IsBlank)
            return new ChipModel(ChipKind.Active, GlyphBlank, name, "BLANK", "The presenter sees black. Press B to show the script.");

        if (state.Display == OutputDisplayStatus.NoSecondDisplay)
            return new ChipModel(ChipKind.Attention, GlyphCaution, "Output", "NO 2ND DISPLAY", "Connect the presenter's display, then press F11.");

        return new ChipModel(ChipKind.Live, GlyphLive, name, "LIVE", "The presenter can see this. Changes show on " + displayName + " now.");
    }

    public static string ControllerName(ControllerKind kind) => kind switch
    {
        ControllerKind.MouseWheel => "Wheel",
        ControllerKind.Xbox => "Xbox",
        ControllerKind.JogShuttle => "Shuttle",
        _ => "Keyboard",
    };

    /// <summary>
    /// The controller chip. <paramref name="status"/> is the latest report for that controller, or null if none has
    /// been seen. A lost controller is a fault only while the output is LIVE, otherwise it needs attention.
    /// </summary>
    public static ChipModel Controller(ControllerKind kind, DeviceStatus? status, bool outputIsLive)
    {
        var name = ControllerName(kind);

        // A wheel can't report: always ready.
        if (kind is ControllerKind.MouseWheel or ControllerKind.Keyboard)
            return new ChipModel(ChipKind.Ok, GlyphOk, name, "READY", "The mouse wheel drives the prompt over the preview. " + KeyboardStillWorks);

        var connected = status is { Connection: DeviceConnection.Connected };
        if (!connected)
        {
            var lost = status is { Connection: DeviceConnection.Disconnected };
            var sentence = lost
                ? $"The {FullName(kind)} stopped answering. {KeyboardStillWorks}"
                : $"No {FullName(kind)} found. Connect it, then wait a moment. {KeyboardStillWorks}";
            return outputIsLive
                ? new ChipModel(ChipKind.Fault, GlyphFault, name, "MISSING", sentence)
                : new ChipModel(ChipKind.Attention, GlyphCaution, name, "MISSING", sentence);
        }

        if (status!.Battery is BatteryLevel.Low or BatteryLevel.Empty)
            return new ChipModel(ChipKind.Attention, GlyphCaution, name, "BATTERY LOW",
                $"The {FullName(kind)} battery is low. Charge or replace it soon. {KeyboardStillWorks}");

        return new ChipModel(ChipKind.Ok, GlyphOk, name, "READY", $"The {FullName(kind)} is connected. {KeyboardStillWorks}");
    }

    /// <summary>The chip on a Settings card for a controller that is not the chosen one.</summary>
    public static ChipModel NotInUse(ControllerKind kind) =>
        new(ChipKind.Idle, GlyphOff, ControllerName(kind), "NOT IN USE", "Choose this controller to use it.");

    public static string FullName(ControllerKind kind) => kind switch
    {
        ControllerKind.MouseWheel => "mouse wheel",
        ControllerKind.Xbox => "Xbox controller",
        ControllerKind.JogShuttle => "jog-shuttle",
        _ => "keyboard",
    };
}
