using System.Windows;
using System.Windows.Documents;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering.Tests;

internal sealed class RecordingLog : IAppLog
{
    private readonly object _gate = new();
    private readonly List<string> _lines = new();

    public bool DiagnosticsEnabled { get; init; }
    public string LogDirectory => "";

    public IReadOnlyList<string> Lines
    {
        get { lock (_gate) { return _lines.ToArray(); } }
    }

    public void Info(string area, string message) => Add("INFO", area, message);
    public void Warn(string area, string message) => Add("WARN", area, message);
    public void Error(string area, string message, Exception? exception = null) => Add("ERROR", area, message + exception);
    public void Trace(string area, string message) => Add("TRACE", area, message);

    private void Add(string level, string area, string message)
    {
        lock (_gate) { _lines.Add($"{level} [{area}] {message}"); }
    }
}

/// <summary>An engine that records what the renderers tell it. Ticks are counted, position is settable.</summary>
internal sealed class FakeEngine : IScrollEngine
{
    private readonly object _gate = new();
    private long _positionBits;

    public List<(LayoutGeometry Geometry, ViewportMetrics Viewport)> Layouts { get; } = new();
    public List<EngineCommand> Posted { get; } = new();
    public int Ticks { get; private set; }
    public int StaleTicks { get; private set; }

    public void Post(EngineCommand command) { lock (_gate) { Posted.Add(command); } }
    public void Tick(long timestamp) => Ticks++;
    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05) { StaleTicks++; return true; }

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
        lock (_gate)
        {
            Layouts.Add((geometry, viewport));
            Geometry = geometry;
            Viewport = viewport;
        }
    }

    public double ReadPosition() => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _positionBits));
    public void SetPosition(double p) => Interlocked.Exchange(ref _positionBits, BitConverter.DoubleToInt64Bits(p));

    public EngineStatus Status { get; set; } = new();
    public LayoutGeometry? Geometry { get; private set; }
    public ViewportMetrics Viewport { get; private set; }
    public event EventHandler<EngineStatus>? StatusChanged { add { } remove { } }

    public int LayoutCount { get { lock (_gate) { return Layouts.Count; } } }
}

internal sealed class FakeSettings : ISettingsService
{
    private readonly object _gate = new();
    private PrompterSettings _current;

    public FakeSettings(PrompterSettings? initial = null) => _current = initial ?? PrompterSettings.Default;

    public PrompterSettings Current { get { lock (_gate) { return _current; } } }
    public string SettingsPath => "";
    public void Load() { }
    public Task SaveAsync() => Task.CompletedTask;
    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        PrompterSettings old, next;
        lock (_gate)
        {
            old = _current;
            next = change(old);
            if (ReferenceEquals(next, old)) return;
            _current = next;
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(old, next));
    }
}

/// <summary>Builds a plain FlowDocument of N paragraphs; counts calls. Stands in for Documents (stream E).</summary>
internal sealed class FakeDocumentFactory : IOutputDocumentFactory
{
    public int Paragraphs { get; set; } = 6;
    public bool Throw { get; set; }
    public int Calls;
    public OutputTextStyle? LastStyle { get; private set; }
    public PromptDocumentSnapshot? LastSnapshot { get; private set; }

    public FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style)
    {
        Interlocked.Increment(ref Calls);
        if (Throw) throw new InvalidOperationException("factory failed");
        LastStyle = style;
        LastSnapshot = snapshot;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            FontSize = style.BaseFontSize,
            LineHeight = style.BaseFontSize * style.LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = style.Alignment,
            Foreground = new System.Windows.Media.SolidColorBrush(style.Ink),
        };
        for (var i = 0; i < Paragraphs; i++)
        {
            doc.Blocks.Add(new Paragraph(new Run($"Paragraph {i} of the test script, long enough to wrap onto more than one line at large sizes.")) { Margin = new Thickness(0, 0, 0, style.BaseFontSize * style.LineSpacing * style.ParagraphGapLines) });
        }

        return doc;
    }
}

/// <summary>Records each paragraph's laid-out top; proves the document was hosted with a valid layout.</summary>
internal sealed class FakeGeometryBuilder : ILayoutGeometryBuilder
{
    public int Calls;
    public List<double> LastParagraphTops { get; } = new();
    public GeometryBuildOptions? LastOptions { get; private set; }

    public LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options)
    {
        Interlocked.Increment(ref Calls);
        LastOptions = options;
        LastParagraphTops.Clear();
        foreach (var block in laidOutDocument.Blocks)
        {
            var rect = block.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            LastParagraphTops.Add(rect.IsEmpty ? double.NaN : rect.Top);
        }

        var tops = LastParagraphTops.ToArray();
        var heights = Enumerable.Repeat(options.BaseLineHeight, tops.Length).ToArray();
        var end = tops.Length == 0 ? 0 : tops[^1] + options.BaseLineHeight;
        return new LayoutGeometry(options.LayoutKey, options.Snapshot.Version, options.BaseLineHeight, end, end, tops, heights,
            Enumerable.Range(0, tops.Length).ToArray(), new int[tops.Length], (double[])tops.Clone(), Array.Empty<SectionMarker>(), options.Snapshot.WordCount);
    }
}

internal sealed class FakeMonitors : IMonitorService
{
    private readonly object _gate = new();
    private IReadOnlyList<MonitorInfo> _monitors;

    public FakeMonitors(params MonitorInfo[] monitors) => _monitors = monitors;

    public IReadOnlyList<MonitorInfo> GetMonitors() { lock (_gate) { return _monitors; } }

    public MonitorInfo? ResolveOutputMonitor(string? savedStableId) => MonitorResolver.Resolve(GetMonitors(), savedStableId);

    public event EventHandler? MonitorsChanged;

    public void Set(params MonitorInfo[] monitors)
    {
        lock (_gate) { _monitors = monitors; }
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeOutput : IOutputSurface
{
    private OutputState _state = OutputState.Initial;
    public OutputState State { get => Volatile.Read(ref _state); set { Volatile.Write(ref _state, value); StateChanged?.Invoke(this, value); } }
    public event EventHandler<OutputState>? StateChanged;
    public Task InitializeAsync() => Task.CompletedTask;
    public Task ShowAsync() => Task.CompletedTask;
    public Task HideAsync() => Task.CompletedTask;
    public void SetBlank(bool blank) => State = State with { IsBlank = blank };
    public Task ShutdownAsync() => Task.CompletedTask;
}

internal static class Wait
{
    public static bool For(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < until)
        {
            if (condition()) return true;
            Thread.Sleep(10);
        }

        return condition();
    }
}
