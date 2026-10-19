using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>Plain-English names for commands and controls, in the DESIGN.md voice (British spelling, sentence case).</summary>
public static class Labels
{
    public static string Command(PrompterCommand command) => command switch
    {
        PrompterCommand.PlayPause => "Play or pause",
        PrompterCommand.Play => "Play",
        PrompterCommand.Pause => "Pause",
        PrompterCommand.SpeedUp => "Speed up",
        PrompterCommand.SpeedDown => "Slow down",
        PrompterCommand.SpeedUpFine => "Speed up a little",
        PrompterCommand.SpeedDownFine => "Slow down a little",
        PrompterCommand.SpeedReset => "Back to the start speed",
        PrompterCommand.LineNext => "Next line",
        PrompterCommand.LinePrevious => "Previous line",
        PrompterCommand.ParagraphNext => "Next paragraph",
        PrompterCommand.ParagraphPrevious => "Previous paragraph",
        PrompterCommand.SectionNext => "Next section",
        PrompterCommand.SectionPrevious => "Previous section",
        PrompterCommand.JumpToStart => "Jump to start",
        PrompterCommand.JumpToEnd => "Jump to end",
        PrompterCommand.ToggleScrollMode => "Switch scrolling mode",
        PrompterCommand.SetDisplayScrolling => "Display scrolling",
        PrompterCommand.SetLineScrolling => "Line scrolling",
        PrompterCommand.ToggleBlank => "Blank output",
        PrompterCommand.ToggleOutput => "Show or hide output",
        _ => "Nothing",
    };

    /// <summary>"Speed up" or, when Line scrolling differs, "Speed up (Line scrolling: next line)".</summary>
    public static string Action(BindingAction action)
    {
        if (action.LineMode is not { } line || line == action.Display) return Command(action.Display);
        return line == PrompterCommand.None
            ? $"{Command(action.Display)} (Display scrolling only)"
            : $"{Command(action.Display)} (Line scrolling: {Command(line).ToLowerInvariant()})";
    }

    public static string Control(string controlId) => controlId switch
    {
        ControlIds.Wheel => "Wheel",
        ControlIds.MiddleButton => "Middle click",
        ControlIds.XButton1 => "Side button 1",
        ControlIds.XButton2 => "Side button 2",
        ControlIds.LeftTrigger => "Left trigger",
        ControlIds.RightTrigger => "Right trigger",
        ControlIds.LeftThumbX => "Left stick, sideways",
        ControlIds.LeftThumbY => "Left stick, up and down",
        ControlIds.RightThumbX => "Right stick, sideways",
        ControlIds.RightThumbY => "Right stick, up and down",
        ControlIds.A => "A",
        ControlIds.B => "B",
        ControlIds.X => "X",
        ControlIds.Y => "Y",
        ControlIds.DPadUp => "D-pad up",
        ControlIds.DPadDown => "D-pad down",
        ControlIds.DPadLeft => "D-pad left",
        ControlIds.DPadRight => "D-pad right",
        ControlIds.LeftShoulder => "LB",
        ControlIds.RightShoulder => "RB",
        ControlIds.View => "View button",
        ControlIds.Menu => "Menu button",
        ControlIds.LeftThumbPress => "Left stick press",
        ControlIds.RightThumbPress => "Right stick press",
        ControlIds.ShuttleRing => "Shuttle ring",
        ControlIds.JogWheel => "Jog wheel",
        _ when controlId.StartsWith("Button", StringComparison.Ordinal) => "Button " + controlId["Button".Length..],
        _ => controlId,
    };

    public static string AxisRoleText(AxisRole role) => role switch
    {
        AxisRole.Speed => "Speed, forward and reverse",
        AxisRole.SpeedForward => "Speed, forward",
        AxisRole.SpeedReverse => "Speed, reverse",
        _ => "Not used",
    };

    public static string Relative(RelativeBinding binding)
    {
        static string Role(RelativeRole role) => role switch
        {
            RelativeRole.Speed => "speed",
            RelativeRole.Lines => "one line per notch",
            RelativeRole.Pixels => "smooth scroll",
            _ => "nothing",
        };

        return binding.DisplayRole == binding.LineRole
            ? Capitalise(Role(binding.DisplayRole))
            : $"{Capitalise(Role(binding.DisplayRole))} (Line scrolling: {Role(binding.LineRole)})";
    }

    public static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Key chord for display: "Ctrl+Shift+PageDown" becomes "Ctrl+Shift+Page Down".</summary>
    public static string Chord(string chord)
    {
        var parts = chord.Split('+');
        var key = parts[^1] switch
        {
            "PageDown" => "Page Down",
            "PageUp" => "Page Up",
            "Escape" => "Esc",
            "D0" => "0",
            "NumPad0" => "Num 0",
            "OemComma" => ",",
            "OemQuestion" => "/",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemPlus" => "=",
            "OemMinus" => "−",
            var other => other,
        };
        parts[^1] = key;
        return string.Join("+", parts);
    }
}
