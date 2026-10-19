using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>Everything the preview needs; registered by AddIluRendering, resolved by the shell.</summary>
public sealed record PreviewServices(
    IScrollEngine Engine,
    IPromptDocumentService Documents,
    ISettingsService Settings,
    IOutputSurface Output,
    IOutputDocumentFactory DocumentFactory,
    ILayoutGeometryBuilder GeometryBuilder,
    IAppLog Log);

/// <summary>How the preview is currently moved.</summary>
internal enum PreviewDriveMode
{
    Idle = 0,
    Frames,     // its own CompositionTarget.Rendering subscription (output not rendering)
    Pulses,     // repainted from the output thread's frame pulse (output rendering)
}

/// <summary>
/// The live preview (DESIGN.md D2): the output laid out at the output's logical size, scaled, letterboxed
/// in `backdrop`, with preview-only overlays (margin tint, reading line, BLANK dimming) and margin /
/// reading-line drag handles. The shell places it in XAML and calls Attach once.
/// Public surface frozen; stream C owns the implementation.
/// </summary>
public sealed class PrompterPreview : Control
{
    private const double BlankDim = 0.35;

    private readonly PreviewScene _scene = new();
    private PreviewServices? _services;
    private PrompterSurface? _surface;
    private FrameClock _clock = new();
    private FrameStats? _stats;
    private bool _outputRendering;
    private IFramePulse? _pulse;
    private bool _renderingSubscribed;
    private bool _pulseSubscribed;
    private bool _pulseStalled;
    private long _lastPulse;
    private int _pulsePending;
    private DispatcherTimer? _watchdog;
    private readonly Action _pulseAction;
    private OutputState _outputState = OutputState.Initial;
    private int _stateRefreshPending;
    private PreviewHandle _hover;
    private PreviewHandle _drag;
    private readonly Action _refreshAction;

    public PrompterPreview()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        _refreshAction = RefreshFromOutputState;
        _pulseAction = PaintFromPulse;
        AddVisualChild(_scene);
        AddLogicalChild(_scene);
        _scene.LayoutChanged += (_, _) => PushVisualState();
    }

    /// <summary>True while the pointer is over the scaled script (the shell shows "Wheel drives the prompt here").</summary>
    public static readonly DependencyProperty IsPointerOverScriptProperty = DependencyProperty.Register(
        nameof(IsPointerOverScript), typeof(bool), typeof(PrompterPreview), new PropertyMetadata(false));

    public bool IsPointerOverScript
    {
        get => (bool)GetValue(IsPointerOverScriptProperty);
        private set => SetValue(IsPointerOverScriptProperty, value);
    }

    public void Attach(PreviewServices services)
    {
        if (_services is not null) Detach();
        _services = services;

        var surfaceServices = new SurfaceServices(
            services.Engine, services.Documents, services.Settings, services.DocumentFactory, services.GeometryBuilder,
            services.Log, () => services.Output.State.Geometry);
        _surface = new PrompterSurface("preview", surfaceServices, snapToPixels: false);
        _surface.Rebuilt += OnSurfaceRebuilt;
        _scene.SetSurface(_surface);

        _pulse = services.Output as IFramePulse;
        _outputState = services.Output.State;
        services.Output.StateChanged += OnOutputStateChanged;
        _surface.Start();
        _surface.Rebuild();                       // the engine has a geometry from the first frame
        ApplyOutputState();

        _clock = new FrameClock();
        _stats = services.Log.DiagnosticsEnabled
            ? new FrameStats("preview", services.Log, _outputState.Geometry.RefreshHz, TimeSpan.FromSeconds(10))
            : null;
        ApplyDriveMode();
    }

    public void Detach()
    {
        SetRendering(false);
        SetPulse(false);
        StopWatchdog();
        if (_services is not null) _services.Output.StateChanged -= OnOutputStateChanged;
        if (_surface is not null)
        {
            _surface.Rebuilt -= OnSurfaceRebuilt;
            _surface.Stop();
            _scene.SetSurface(null);
        }

        _surface = null;
        _services = null;
        _pulse = null;
        _stats = null;
        _outputRendering = false;
        _pulseStalled = false;
        EndDrag();
    }

    /// <summary>The surface (tests).</summary>
    internal PrompterSurface? Surface => _surface;

    internal PreviewScene Scene => _scene;

    /// <summary>Which mechanism currently moves the picture (tests and diagnostics).</summary>
    internal PreviewDriveMode DriveMode =>
        _renderingSubscribed ? PreviewDriveMode.Frames : _pulseSubscribed ? PreviewDriveMode.Pulses : PreviewDriveMode.Idle;

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) =>
        index == 0 ? _scene : throw new ArgumentOutOfRangeException(nameof(index));

    protected override Size MeasureOverride(Size constraint)
    {
        _scene.Measure(constraint);
        return _scene.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _scene.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    // ------------------------------------------------------------------ driving the picture (UI thread)
    //
    // WPF has one render thread per process. Measured on the development laptop: while the output window animates on its
    // own thread, a CompositionTarget.Rendering subscription on the UI thread (even one that paints rarely) makes both
    // threads sync with the render thread every frame and the output drops to 30 fps. A preview painted from a pulse that
    // the output thread sends after each frame costs the output nothing, even at 60 Hz. So:
    //  - output rendering: no Rendering subscription here; the output's FramePulse repaints the preview (a watchdog falls
    //    back to Rendering if pulses stop for 500 ms);
    //  - no output rendering (closed, hidden, missing, or no pulse source): Rendering drives and ticks the engine.

    private void ApplyDriveMode()
    {
        var pulses = _outputRendering && _pulse is not null;
        SetPulse(pulses);                                  // stays subscribed while stalled, to notice the output coming back
        if (pulses)
        {
            SetRendering(_pulseStalled);
            StartWatchdog();
        }
        else
        {
            StopWatchdog();
            SetRendering(_services is not null);
        }
    }

    private void SetRendering(bool on)
    {
        if (on == _renderingSubscribed) return;
        _renderingSubscribed = on;
        if (on)
        {
            _clock = new FrameClock();
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void SetPulse(bool on)
    {
        if (_pulse is null || on == _pulseSubscribed) return;
        _pulseSubscribed = on;
        if (on)
        {
            Volatile.Write(ref _lastPulse, Stopwatch.GetTimestamp());
            _pulse.FramePulse += OnPulse;
        }
        else
        {
            _pulse.FramePulse -= OnPulse;
        }
    }

    private void StartWatchdog()
    {
        if (_watchdog is not null) return;
        _watchdog = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
        _watchdog.Tick += OnWatchdog;
        _watchdog.Start();
    }

    private void StopWatchdog()
    {
        if (_watchdog is null) return;
        _watchdog.Stop();
        _watchdog.Tick -= OnWatchdog;
        _watchdog = null;
    }

    private void OnWatchdog(object? sender, EventArgs e)
    {
        var silent = (Stopwatch.GetTimestamp() - Volatile.Read(ref _lastPulse)) / (double)Stopwatch.Frequency;
        if (silent < 0.5 || _pulseStalled) return;
        _pulseStalled = true;
        _services?.Log.Warn("preview", "No frame pulse from the output for 500 ms; the preview is driving itself until it resumes.");
        ApplyDriveMode();
    }

    /// <summary>Raised on the output thread, after a frame in which the position changed.</summary>
    private void OnPulse(object? sender, EventArgs e)
    {
        Volatile.Write(ref _lastPulse, Stopwatch.GetTimestamp());
        if (Interlocked.Exchange(ref _pulsePending, 1) == 1) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, _pulseAction);
    }

    private void PaintFromPulse()
    {
        Interlocked.Exchange(ref _pulsePending, 0);
        var services = _services;
        var surface = _surface;
        if (services is null || surface is null) return;
        if (_pulseStalled)
        {
            _pulseStalled = false;
            ApplyDriveMode();
        }

        surface.SetPosition(services.Engine.ReadPosition());
        _stats?.Record(Stopwatch.GetTimestamp());
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var services = _services;
        var surface = _surface;
        if (services is null || surface is null) return;
        var now = Stopwatch.GetTimestamp();
        if (!_clock.TryMap(((RenderingEventArgs)e).RenderingTime, now, out var frame)) return;

        // With no output ticking, the preview is the only ticker and must tick every frame to stay smooth. If the output
        // is meant to be rendering (watchdog fallback) it only steps in when the output has stalled for 50 ms.
        if (_outputRendering) services.Engine.TickIfStale(now, 0.05);
        else services.Engine.TickIfStale(frame, 0.001);

        surface.SetPosition(services.Engine.ReadPosition());
        _stats?.Record(frame);
    }

    // ------------------------------------------------------------------ state from the output thread

    private void OnOutputStateChanged(object? sender, OutputState state)
    {
        // Raised on the output thread: just remember that something changed and refresh on the UI thread.
        if (Interlocked.Exchange(ref _stateRefreshPending, 1) == 1) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, _refreshAction);
    }

    private void RefreshFromOutputState()
    {
        Interlocked.Exchange(ref _stateRefreshPending, 0);
        if (_services is null) return;
        var previous = _outputState;
        _outputState = _services.Output.State;
        if (previous.Geometry != _outputState.Geometry) _surface?.RequestRebuild();
        ApplyOutputState();
    }

    private void ApplyOutputState()
    {
        _outputRendering = _outputState.IsOpen && _outputState.Display == OutputDisplayStatus.Ready;
        if (_surface is not null) _surface.Opacity = _outputState.IsBlank ? BlankDim : 1.0;
        ApplyDriveMode();
        PushVisualState();
    }

    private void OnSurfaceRebuilt(object? sender, EventArgs e)
    {
        _scene.InvalidateMeasure();
        _scene.InvalidateArrange();
        PushVisualState();
    }

    private void PushVisualState()
    {
        var surface = _surface;
        if (surface is null) return;
        _scene.Overlay.State = new PreviewVisualState(
            _scene.Layout, surface.TextArea, surface.Output.WidthDip, surface.Output.HeightDip,
            _outputState.IsLive, _outputState.IsBlank, _hover, _drag);
    }

    // ------------------------------------------------------------------ pointer: hover, drag margins and the reading line

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_surface is null) return;
        var p = e.GetPosition(this);
        var layout = _scene.Layout;
        IsPointerOverScript = PreviewMath.IsOverStage(p, layout);

        if (_drag != PreviewHandle.None)
        {
            if (e.LeftButton == MouseButtonState.Pressed) ApplyDrag(p, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            return;
        }

        var hover = PreviewMath.HitTest(p, layout, _surface.TextArea, _surface.Output.WidthDip, _surface.Output.HeightDip);
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = CursorFor(hover);
            PushVisualState();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsPointerOverScript = false;
        if (_drag == PreviewHandle.None && _hover != PreviewHandle.None)
        {
            _hover = PreviewHandle.None;
            Cursor = null;
            PushVisualState();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        // DESIGN "Keyboard shortcuts": clicking the preview gives the keys to the transport (the shell switches the key
        // mode when focus arrives here). Without this, Space after a click still typed into the script.
        if (Focusable && !IsKeyboardFocusWithin) Focus();
        if (_surface is null) return;
        var handle = PreviewMath.HitTest(e.GetPosition(this), _scene.Layout, _surface.TextArea, _surface.Output.WidthDip, _surface.Output.HeightDip);
        if (handle == PreviewHandle.None) return;
        _drag = handle;
        _hover = handle;
        CaptureMouse();
        PushVisualState();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_drag == PreviewHandle.None) return;
        EndDrag();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_drag == PreviewHandle.None) return;
        _drag = PreviewHandle.None;
        if (IsMouseCaptured) ReleaseMouseCapture();
        PushVisualState();
    }

    /// <summary>Test hook: behave as if <paramref name="handle"/> were being dragged to <paramref name="controlPoint"/>.</summary>
    internal void DragForTest(PreviewHandle handle, Point controlPoint, bool snap)
    {
        var previous = _drag;
        _drag = handle;
        try
        {
            ApplyDrag(controlPoint, snap);
        }
        finally
        {
            _drag = previous;
        }
    }

    /// <summary>The handle under a control-space point (tests).</summary>
    internal PreviewHandle HitTestForTest(Point controlPoint) =>
        _surface is null
            ? PreviewHandle.None
            : PreviewMath.HitTest(controlPoint, _scene.Layout, _surface.TextArea, _surface.Output.WidthDip, _surface.Output.HeightDip);

    private void ApplyDrag(Point controlPoint, bool snap)
    {
        var services = _services;
        var surface = _surface;
        if (services is null || surface is null) return;

        var output = surface.Output;
        var point = PreviewMath.ToOutput(controlPoint, _scene.Layout);
        if (PreviewMath.ToEdge(_drag) is { } edge)
        {
            var px = PreviewMath.EdgePixels(edge, point, output, snap);
            services.Settings.Update(s =>
            {
                var margins = MarginMath.SetEdgePixels(s.Margins, edge, px, output.SizePx);
                return margins == s.Margins ? s : s with { Margins = margins };
            });
        }
        else if (_drag == PreviewHandle.ReadingLine)
        {
            var fraction = Math.Round(PreviewMath.ReadingLineFraction(point, surface.TextArea, output, snap), 4);
            services.Settings.Update(s =>
                Math.Abs(s.Output.ReadingLineFraction - fraction) < 1e-9 ? s : s with { Output = s.Output with { ReadingLineFraction = fraction } });
        }
    }

    private static Cursor? CursorFor(PreviewHandle handle) => handle switch
    {
        PreviewHandle.Left or PreviewHandle.Right => Cursors.SizeWE,
        PreviewHandle.Top or PreviewHandle.Bottom or PreviewHandle.ReadingLine => Cursors.SizeNS,
        _ => null,
    };
}
