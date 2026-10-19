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
