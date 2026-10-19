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
