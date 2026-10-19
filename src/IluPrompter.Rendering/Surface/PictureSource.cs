using System.Globalization;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>Where a surface gets its picture of the script from (ARCHITECTURE §3 "Layout thread").</summary>
internal interface IPictureSource
{
    /// <summary>The newest picture (volatile read, any thread); null before the first.</summary>
    PromptPicture? Current { get; }

    /// <summary>True when the source lays out by itself on its own thread (the surfaces only display); false when a
    /// surface must call <see cref="Request"/> on every snapshot or settings change.</summary>
    bool IsAutonomous { get; }

    /// <summary>Any thread. Build a new picture if the snapshot, the text settings or the output size changed (coalesced).</summary>
    void Request();

    /// <summary>Raised after <see cref="Current"/> changed, on the thread that built it.</summary>
    event EventHandler? PictureReady;
}

/// <summary>The output host offers its layout thread to the preview (as it offers its frame pulse).</summary>
internal interface IPictureSourceProvider
{
    IPictureSource Pictures { get; }
}

/// <summary>Builds on the calling thread, synchronously, inside <see cref="Request"/>. Used by a surface that has no layout
/// thread (tests, a preview without an output host).</summary>
internal sealed class InlinePictureSource : IPictureSource
{
    private readonly IPromptDocumentService _documents;
    private readonly ISettingsService _settings;
    private readonly Func<OutputGeometry> _output;
    private readonly IAppLog _log;
    private readonly string _area;
    private readonly PictureBuilder _builder;
    private PromptPicture? _current;

    public InlinePictureSource(IPromptDocumentService documents, ISettingsService settings, IOutputDocumentFactory factory,
        ILayoutGeometryBuilder geometryBuilder, IAppLog log, Func<OutputGeometry> output, string logArea)
    {
        _documents = documents;
        _settings = settings;
        _output = output;
        _log = log;
        _area = logArea;
        _builder = new PictureBuilder(factory, geometryBuilder);
    }

    public PromptPicture? Current => Volatile.Read(ref _current);

    public bool IsAutonomous => false;

    internal PictureBuilder Builder => _builder;

    public event EventHandler? PictureReady;

    public void Request()
    {
        var inputs = new PictureInputs(_documents.Current, _settings.Current, _output());
        var current = Current;
        if (current is not null && string.Equals(current.ContentKey, inputs.ContentKey, StringComparison.Ordinal)) return;
        try
        {
            Volatile.Write(ref _current, _builder.Build(inputs, current));
        }
        catch (Exception ex)
        {
            _log.Error(_area, "Rebuild failed", ex);
            return;
        }

        PictureReady?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The layout thread "ILU Layout" (ARCHITECTURE §3): an STA thread with its own Dispatcher that owns the only output
/// FlowDocument, lays it out, builds the geometry tables and a frozen picture, and hands that to the output and preview
/// surfaces. It follows snapshots and settings itself; the output host also requests a build when the output size
/// changes. Builds are coalesced (one pending at most) and read the newest inputs, so intermediate versions are skipped.
/// Nothing here runs on the output thread, so a rebuild of any size never holds up an output frame.
/// </summary>
internal sealed class PictureWorker : IPictureSource, IDisposable
{
    private const string Area = "layout";
    /// <summary>A few paragraphs with every kind of run the factory styles (bold, italic, a highlight from the palette).</summary>
    private static string WarmUpXaml() =>
        "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph>Good evening, everyone, and " +
        "<Run FontWeight=\"Bold\">welcome</Run> to the <Run FontStyle=\"Italic\">annual</Run> meeting.</Paragraph>" +
        "<Paragraph>## Warm up</Paragraph><Paragraph><Run Background=\"" + Theme.ThemeResources.GetColor("highlight-yellow") +
        "\">Please take a seat</Run> and let us begin.</Paragraph></Section>";

    private readonly IPromptDocumentService _documents;
    private readonly ISettingsService _settings;
    private readonly IOutputDocumentFactory _factory;
    private readonly ILayoutGeometryBuilder _geometryBuilder;
    private readonly IAppLog _log;
    private readonly Func<OutputGeometry> _output;
    private readonly object _gate = new();
    private readonly Action _buildAction;
    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private PictureBuilder? _builder;
    private PromptPicture? _current;
    private int _pending;
    private bool _requestedBeforeStart;
    private int _builds;

    public PictureWorker(IPromptDocumentService documents, ISettingsService settings, IOutputDocumentFactory factory,
        ILayoutGeometryBuilder geometryBuilder, IAppLog log, Func<OutputGeometry> output)
    {
        _documents = documents;
        _settings = settings;
        _factory = factory;
        _geometryBuilder = geometryBuilder;
        _log = log;
        _output = output;
        _buildAction = BuildIfNeeded;
    }

    public PromptPicture? Current => Volatile.Read(ref _current);

    public bool IsAutonomous => true;

    /// <summary>Pictures built so far (tests, diagnostics).</summary>
    public int BuildCount => Volatile.Read(ref _builds);

    public event EventHandler? PictureReady;

    /// <summary>Starts the thread (idempotent): warms the layout path up with a throwaway picture, then builds the current
    /// snapshot. Returns when the thread is ready to take requests.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_thread is not null) return;
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() => ThreadMain(ready)) { Name = "ILU Layout", IsBackground = true, Priority = ThreadPriority.BelowNormal };
            thread.SetApartmentState(ApartmentState.STA);
            _thread = thread;
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(10));
            _documents.SnapshotPublished += OnSnapshotPublished;
            _settings.Changed += OnSettingsChanged;
        }

        Request();
    }

    public void Request()
    {
        var dispatcher = Volatile.Read(ref _dispatcher);
        if (dispatcher is null)
        {
            _requestedBeforeStart = true;
            return;
        }

        if (Interlocked.Exchange(ref _pending, 1) == 1) return;
        dispatcher.BeginInvoke(DispatcherPriority.Background, _buildAction);
    }

    /// <summary>Runs <paramref name="action"/> on the layout thread and waits (tests).</summary>
    internal void Invoke(Action action) => _dispatcher?.Invoke(action);

    public void Dispose()
    {
        Dispatcher? dispatcher;
        Thread? thread;
        lock (_gate)
        {
            dispatcher = _dispatcher;
            thread = _thread;
            _dispatcher = null;
            _thread = null;
            _documents.SnapshotPublished -= OnSnapshotPublished;
            _settings.Changed -= OnSettingsChanged;
        }

        if (dispatcher is null || thread is null) return;
        dispatcher.InvokeShutdown();
        if (!thread.Join(TimeSpan.FromSeconds(2))) _log.Warn(Area, "The layout thread did not stop within 2 s; continuing.");
    }

    private void ThreadMain(ManualResetEventSlim ready)
    {
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.UnhandledException += (_, e) =>
            {
                _log.Error(Area, "Unhandled exception on the layout thread", e.Exception);
                e.Handled = true;
            };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _builder = new PictureBuilder(_factory, _geometryBuilder);
            Volatile.Write(ref _dispatcher, dispatcher);
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(WarmUp));
            ready.Set();
            Dispatcher.Run();
        }
        catch (Exception ex)
        {
            _log.Error(Area, "The layout thread failed", ex);
            ready.Set();
        }
    }

    /// <summary>One throwaway picture with every kind of run, so the first real one (and the output's first frame of it)
    /// does not pay for JIT, font loading and WPF's text caches (the "cold first rebuild").</summary>
    private void WarmUp()
    {
        try
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var snapshot = new PromptDocumentSnapshot(0, WarmUpXaml(), 20, 3, [new SectionInfo(0, "Warm up", 1)], 20);
            _builder!.Build(new PictureInputs(snapshot, _settings.Current, _output()), null);
            _log.Info(Area, string.Create(CultureInfo.InvariantCulture, $"Layout thread warmed up in {started.Elapsed.TotalMilliseconds:0} ms."));
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Warm-up failed: " + ex.Message);
        }

        if (_requestedBeforeStart) Request();
    }

    private void OnSnapshotPublished(object? sender, PromptDocumentSnapshot snapshot) => Request();

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var o = e.Old;
        var n = e.New;
        if (!Equals(o.Output, n.Output) || !Equals(o.Margins, n.Margins) || !Equals(o.Text, n.Text) || !Equals(o.Editor, n.Editor)) Request();
    }

    private void BuildIfNeeded()
    {
        Interlocked.Exchange(ref _pending, 0);
        var builder = _builder;
        if (builder is null) return;
        var inputs = new PictureInputs(_documents.Current, _settings.Current, _output());
        var current = Current;
        if (current is not null && string.Equals(current.ContentKey, inputs.ContentKey, StringComparison.Ordinal)) return;

        PromptPicture picture;
        try
        {
            picture = builder.Build(inputs, current);
        }
        catch (Exception ex)
        {
            _log.Error(Area, "Layout failed; the output keeps the previous picture", ex);
            return;
        }

        Volatile.Write(ref _current, picture);
        Interlocked.Increment(ref _builds);
        var t = picture.Timings;
        if (_log.DiagnosticsEnabled || t.Total > 1000)
        {
            _log.Trace(Area, string.Create(CultureInfo.InvariantCulture,
                $"picture v{inputs.Snapshot.Version} ({(picture.PatchedParagraphs >= 0 ? picture.PatchedParagraphs + " paragraphs patched" : "full layout")}): {t.Total:0} ms (document {t.Document:0}, layout {t.Layout:0}, geometry {t.Geometry:0}, picture {t.Picture:0}); allocated {picture.Allocated.Total:0.0} MB (document {picture.Allocated.Document:0.0}, layout {picture.Allocated.Layout:0.0}, geometry {picture.Allocated.Geometry:0.0}, picture {picture.Allocated.Picture:0.0}); {picture.Geometry.LineCount} lines, {picture.Blocks.Count} blocks, key {picture.ContentKey}"));
        }

        try
        {
            PictureReady?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(Area, "A picture subscriber failed", ex);
        }
    }
}
