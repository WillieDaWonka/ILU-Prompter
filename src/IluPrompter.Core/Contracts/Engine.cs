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
