using System.Globalization;
using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>
/// The short names and action labels of the Controller drawing (DESIGN.md "Short names, for the drawing only" and the
/// action-label table). Settings › Controller keeps the long names from <see cref="Labels"/>. Pure.
/// </summary>
public static class ControllerLabels
{
    public const string NotUsed = "Not used";

    /// <summary>Short name of a control: "RT", "D-pad ↑", "Button 7".</summary>
    public static string ShortName(string controlId) => controlId switch
    {
        ControlIds.Wheel => "Wheel",
        ControlIds.MiddleButton => "Middle",
        ControlIds.XButton1 => "Side 1",
        ControlIds.XButton2 => "Side 2",
        ControlIds.LeftTrigger => "LT",
        ControlIds.RightTrigger => "RT",
        ControlIds.LeftShoulder => "LB",
        ControlIds.RightShoulder => "RB",
        ControlIds.A => "A",
        ControlIds.B => "B",
        ControlIds.X => "X",
        ControlIds.Y => "Y",
        ControlIds.DPadUp => "D-pad ↑",
        ControlIds.DPadDown => "D-pad ↓",
        ControlIds.DPadLeft => "D-pad ←",
        ControlIds.DPadRight => "D-pad →",
        ControlIds.View => "View",
        ControlIds.Menu => "Menu",
        ControlIds.LeftThumbPress => "LS press",
        ControlIds.RightThumbPress => "RS press",
        ControlIds.LeftThumbX => "LS",
        ControlIds.LeftThumbY => "LS",
        ControlIds.RightThumbX => "RS",
        ControlIds.RightThumbY => "RS",
        ControlIds.ShuttleRing => "Ring",
        ControlIds.JogWheel => "Jog",
        _ when controlId.StartsWith("Button", StringComparison.Ordinal) => "Button " + controlId["Button".Length..],
        _ => controlId,
    };

    /// <summary>The label for what a command does, from the action-label table.</summary>
    public static string Command(PrompterCommand command) => command switch
    {
        PrompterCommand.PlayPause => "Play / pause",
        PrompterCommand.Play => "Play",
        PrompterCommand.Pause => "Pause",
        PrompterCommand.SpeedUp => "Speed +",
        PrompterCommand.SpeedDown => "Speed −",
        PrompterCommand.SpeedUpFine => "Speed + fine",
        PrompterCommand.SpeedDownFine => "Speed − fine",
        PrompterCommand.SpeedReset => "Start speed",
        PrompterCommand.LineNext => "Next line",
        PrompterCommand.LinePrevious => "Previous line",
        PrompterCommand.ParagraphNext => "Next para",
        PrompterCommand.ParagraphPrevious => "Previous para",
        PrompterCommand.SectionNext => "Next section",
        PrompterCommand.SectionPrevious => "Previous section",
        PrompterCommand.JumpToStart => "Jump to start",
        PrompterCommand.JumpToEnd => "Jump to end",
        PrompterCommand.ToggleScrollMode => "Switch mode",
        PrompterCommand.SetDisplayScrolling => "Display scrolling",
        PrompterCommand.SetLineScrolling => "Line scrolling",
        PrompterCommand.ToggleBlank => "Blank output",
        PrompterCommand.ToggleOutput => "Output on / off",
        _ => NotUsed,
    };

    /// <summary>What a button does in the current mode, or "Not used" when it has no binding.</summary>
    public static string Button(IReadOnlyDictionary<string, BindingAction> buttons, string controlId, ScrollMode mode) =>
        buttons.TryGetValue(controlId, out var action) ? Command(action.For(mode)) : NotUsed;

    /// <summary>True when the control does something in <paramref name="mode"/> (not None, and present).</summary>
    public static bool IsBound(IReadOnlyDictionary<string, BindingAction> buttons, string controlId, ScrollMode mode) =>
        buttons.TryGetValue(controlId, out var action) && action.For(mode) != PrompterCommand.None;

    /// <summary>An axis role without a direction: "Speed", "Speed forward", "Speed reverse". None reads "Not used".</summary>
    public static string AxisAction(AxisRole role) => role switch
    {
        AxisRole.Speed => "Speed",
        AxisRole.SpeedForward => "Speed forward",
        AxisRole.SpeedReverse => "Speed reverse",
        _ => NotUsed,
    };

    /// <summary>A resting relative row: the role without a direction ("Speed", "Lines", "Scroll").</summary>
    public static string RelativeRest(RelativeRole role) => role switch
    {
        RelativeRole.Speed => "Speed",
        RelativeRole.Lines => "Lines",
        RelativeRole.Pixels => "Scroll",
        _ => NotUsed,
    };

    /// <summary>
    /// A lit relative row, from the net amount of the burst. <paramref name="forwardUnits"/> is the router's own unit
    /// count (wheel: towards you; jog: clockwise; after Invert) so this agrees with what the engine did:
    /// Speed gives "Speed + 1.5" or "Speed − 0.5" (units × SpeedPerUnit), Lines gives Next line / Previous line.
    /// </summary>
    public static string RelativeLit(RelativeBinding binding, RelativeRole role, double forwardUnits)
    {
        switch (role)
        {
            case RelativeRole.Speed:
                var delta = forwardUnits * binding.SpeedPerUnit;
                var rounded = Math.Round(Math.Abs(delta), 1, MidpointRounding.AwayFromZero);
                if (rounded == 0) return RelativeRest(role);
                return "Speed " + (delta < 0 ? ReadoutFormat.Minus : "+") + " " + rounded.ToString("0.0", CultureInfo.InvariantCulture);
            case RelativeRole.Lines:
                var lines = forwardUnits * binding.LinesPerUnit;
                return lines == 0 ? RelativeRest(role) : lines > 0 ? "Next line" : "Previous line";
            case RelativeRole.Pixels:
                return "Scroll";
            default:
                return NotUsed;
        }
    }

    /// <summary>The router's own conversion: the wheel is "forward" towards the user, the jog clockwise; Invert flips it.</summary>
    public static double ForwardUnits(ControllerKind kind, RelativeBinding binding, double amount)
    {
        var units = kind == ControllerKind.MouseWheel ? -amount : amount;
        return binding.Invert ? -units : units;
    }

    // ===== values =====

    /// <summary>"62 %" for a trigger pull (0 to 1).</summary>
    public static string Percent(double value) =>
        ((int)Math.Round(Math.Clamp(value, 0, 1) * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + " %";

    /// <summary>"+62 %", "−62 %" or "0 %" for a stick axis (-1 to 1).</summary>
    public static string SignedPercent(double value)
    {
        var pct = (int)Math.Round(Math.Clamp(value, -1, 1) * 100, MidpointRounding.AwayFromZero);
        var text = Math.Abs(pct).ToString(CultureInfo.InvariantCulture) + " %";
        return pct > 0 ? "+" + text : pct < 0 ? ReadoutFormat.Minus + text : text;
    }

    /// <summary>"+3", "−7" or "0" for the shuttle ring position.</summary>
    public static string RingPosition(int k) =>
        k > 0 ? "+" + k.ToString(CultureInfo.InvariantCulture) : k < 0 ? ReadoutFormat.Minus + (-k).ToString(CultureInfo.InvariantCulture) : "0";

    /// <summary>The wheel burst: "▲ 3" (away from you), "▼ 2" (towards you), one decimal for fractions, "0" when over.</summary>
    public static string WheelBurst(double net)
    {
        var magnitude = FormatCount(Math.Abs(net));
        if (magnitude == "0") return "0";
        return (net > 0 ? "▲ " : "▼ ") + magnitude;
    }

    /// <summary>The jog burst: "+4" (clockwise), "−2", or "0".</summary>
    public static string JogBurst(double net)
    {
        var magnitude = FormatCount(Math.Abs(net));
        if (magnitude == "0") return "0";
        return (net > 0 ? "+" : ReadoutFormat.Minus) + magnitude;
    }

    private static string FormatCount(double magnitude)
    {
        var rounded = Math.Round(magnitude, 1, MidpointRounding.AwayFromZero);
        return Math.Abs(rounded - Math.Round(rounded)) < 0.0001
            ? ((int)Math.Round(rounded)).ToString(CultureInfo.InvariantCulture)
            : rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
