namespace IluPrompter.Input;

/// <summary>What changed between the previous report and this one.</summary>
internal readonly record struct ShuttleChanges(
    bool IsBaseline,
    bool RingChanged,
    double Ring,
    int JogDelta,
    int ButtonEdgeCount);

/// <summary>
/// Turns a stream of <see cref="ShuttleReport"/>s into changes. The first report after (re)opening the
/// device is the baseline: it reports the ring position once, but no jog movement and no button edges
/// (buttons already held at connect do not fire). Pure state machine, no device access.
/// </summary>
internal sealed class ShuttleReportTracker
{
    /// <summary>Largest number of edges one report can produce (16 buttons).</summary>
    public const int MaxEdges = 16;

    private readonly ContourModel _model;
    private ShuttleReport _previous;

    public ShuttleReportTracker(ContourModel model) => _model = model;

    public bool HasBaseline { get; private set; }

    public void Reset() => HasBaseline = false;

    public ShuttleChanges Update(in ShuttleReport report, Span<ButtonEdge> edges)
    {
        var ring = ContourShuttleReportParser.RingValue(report.Ring);
        if (!HasBaseline)
        {
            _previous = report;
            HasBaseline = true;
            return new ShuttleChanges(true, true, ring, 0, 0);
        }

        var ringChanged = ContourShuttleReportParser.RingValue(_previous.Ring) != ring;
        int jog = ContourShuttleReportParser.JogDelta(_previous.Jog, report.Jog);

        var count = 0;
        var changed = (ushort)(_previous.Buttons ^ report.Buttons);
        if (changed != 0)
        {
            for (var bit = 0; bit < 16; bit++)
            {
                var mask = (ushort)(1 << bit);
                if ((changed & mask) != 0)
                {
                    edges[count++] = new ButtonEdge(_model.ControlId(bit), (report.Buttons & mask) != 0);
                }
            }
        }

        _previous = report;
        return new ShuttleChanges(false, ringChanged, ring, jog, count);
    }
}
