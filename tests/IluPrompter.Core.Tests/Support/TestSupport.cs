using System.Diagnostics;

namespace IluPrompter.Core.Tests;

/// <summary>Synthetic geometry: N lines of equal height, grouped into paragraphs, with optional section paragraphs.</summary>
internal static class TestGeometry
{
    public const double LineHeight = 119.6;
    public const double ReadingLine = 314;

    public static ViewportMetrics Viewport(double readingLineY = ReadingLine) => new(1600, 952, readingLineY);

    /// <summary>Lines tile the document from y = 0; END sits right below the last line.</summary>
    public static LayoutGeometry Create(
        int lines = 100,
        double lineHeight = LineHeight,
        int linesPerParagraph = 5,
        int[]? sectionParagraphs = null,
        string key = "k1",
        int charsPerLine = 40,
        int wordCount = 0)
    {
        var tops = new double[lines];
        var heights = new double[lines];
        var paragraphs = new int[lines];
        var offsets = new int[lines];
        for (var i = 0; i < lines; i++)
        {
            tops[i] = i * lineHeight;
            heights[i] = lineHeight;
            paragraphs[i] = i / linesPerParagraph;
            offsets[i] = (i % linesPerParagraph) * charsPerLine;
        }

        var paragraphCount = lines == 0 ? 0 : ((lines - 1) / linesPerParagraph) + 1;
        var paragraphTops = new double[paragraphCount];
        for (var p = 0; p < paragraphCount; p++)
        {
            paragraphTops[p] = p * linesPerParagraph * lineHeight;
        }

        var sections = new List<SectionMarker>();
        if (sectionParagraphs is not null)
        {
            for (var s = 0; s < sectionParagraphs.Length; s++)
            {
                var p = sectionParagraphs[s];
                sections.Add(new SectionMarker(s, "Section " + (s + 1), p, paragraphTops[p]));
            }
        }

        var endTop = lines * lineHeight;
        return new LayoutGeometry(key, 1, lineHeight, endTop + lineHeight, endTop, tops, heights, paragraphs, offsets, paragraphTops, sections, wordCount > 0 ? wordCount : lines * 5);
    }

    public static double Pmin(LayoutGeometry g, double r = ReadingLine) => (g.LineCount > 0 ? g.LineCentre(0) : 0) - r;

    public static double Pmax(LayoutGeometry g, double r = ReadingLine) => g.EndTop + (g.BaseLineHeight / 2) - r;

    /// <summary>The position that puts line i's centre on the reading line.</summary>
    public static double PosOfLine(LayoutGeometry g, int i, double r = ReadingLine) => g.LineCentre(i) - r;
}

internal sealed class FakeSettings : ISettingsService
{
    private PrompterSettings _current;

    public FakeSettings(PrompterSettings? initial = null) => _current = initial ?? PrompterSettings.Default;

    public PrompterSettings Current => Volatile.Read(ref _current);

    public string SettingsPath => "fake";

    public int SaveCount { get; private set; }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Load()
    {
    }

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        var old = _current;
        var updated = change(old);
        if (ReferenceEquals(old, updated))
        {
            return;
        }

        Volatile.Write(ref _current, updated);
        Changed?.Invoke(this, new SettingsChangedEventArgs(old, updated));
    }

    public Task SaveAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class TestLog : IAppLog
{
    private readonly object _gate = new();
    private readonly List<string> _lines = [];

    public bool DiagnosticsEnabled => true;

    public string LogDirectory => "test";

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return _lines.ToArray();
            }
        }
    }

    public void Info(string area, string message) => Add("INFO", area, message);

    public void Warn(string area, string message) => Add("WARN", area, message);

    public void Error(string area, string message, Exception? exception = null) => Add("ERROR", area, message + exception);

    public void Trace(string area, string message) => Add("TRACE", area, message);

    private void Add(string level, string area, string message)
    {
        lock (_gate)
        {
            _lines.Add($"{level} [{area}] {message}");
        }
    }
}

/// <summary>Drives a real ScrollEngine with synthetic time (Stopwatch ticks derived from seconds).</summary>
internal sealed class EngineHarness
{
    private readonly long _origin = Stopwatch.Frequency * 1000L;
    private double _now;

    public EngineHarness(LayoutGeometry? geometry = null, ViewportMetrics? viewport = null, PrompterSettings? settings = null)
    {
        Settings = new FakeSettings(settings);
        Engine = new ScrollEngine(Settings);
        Geometry = geometry ?? TestGeometry.Create();
        Viewport = viewport ?? TestGeometry.Viewport();
        Engine.SetLayout(Geometry, Viewport);
        Engine.Tick(Timestamp(0)); // establishes time zero
    }

    public FakeSettings Settings { get; }

    public ScrollEngine Engine { get; }

    public LayoutGeometry Geometry { get; private set; }

    public ViewportMetrics Viewport { get; private set; }

    public double Now => _now;

    public double R => Viewport.ReadingLineY;

    public double Pos => Engine.ReadPosition();

    public double Pmin => TestGeometry.Pmin(Geometry, R);

    public double Pmax => TestGeometry.Pmax(Geometry, R);

    public EngineStatus Status => Engine.Status;

    public long Timestamp(double seconds) => _origin + (long)Math.Round(seconds * Stopwatch.Frequency);

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics? viewport = null)
    {
        Geometry = geometry;
        Viewport = viewport ?? Viewport;
        Engine.SetLayout(Geometry, Viewport);
    }

    public void Post(EngineCommand command) => Engine.Post(command);

    /// <summary>One Tick at the current time (applies queued commands without advancing time).</summary>
    public void Apply(params EngineCommand[] commands)
    {
        foreach (var c in commands)
        {
            Engine.Post(c);
        }

        // A tick needs a newer timestamp; use one stopwatch tick so no motion is added.
        _now += 1.0 / Stopwatch.Frequency;
        Engine.Tick(Timestamp(_now));
    }

    /// <summary>Advance by the given seconds in steps of at most 1/60 s.</summary>
    public void Run(double seconds, double hz = 60)
    {
        var steps = Math.Max(1, (int)Math.Ceiling((seconds * hz) - 1e-9));
        var dt = seconds / steps;
        for (var i = 0; i < steps; i++)
        {
            _now += dt;
            Engine.Tick(Timestamp(_now));
        }
    }

    /// <summary>A single Tick that is dt seconds after the previous one (use to test the dt clamp).</summary>
    public void Step(double dt)
    {
        _now += dt;
        Engine.Tick(Timestamp(_now));
    }

    /// <summary>Tick again with the timestamp of the previous tick (must be ignored).</summary>
    public void TickDuplicate() => Engine.Tick(Timestamp(_now));

    public void Play() => Apply(EngineCommand.Play());

    public void Pause() => Apply(EngineCommand.Pause());

    /// <summary>Cut to the position that puts line i on the reading line, keeping the play state.</summary>
    public void SeekToLine(int i) => Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(Geometry, i, R)));

    public double Speed(double speedUnits) =>
        SpeedCurve.ToDipsPerSecond(speedUnits, Geometry.BaseLineHeight, Settings.Current.Scroll.Curve);
}
