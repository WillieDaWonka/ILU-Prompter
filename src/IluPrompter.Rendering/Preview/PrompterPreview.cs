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

        _outputState = services.Output.State;
        services.Output.StateChanged += OnOutputStateChanged;
        _surface.Start();
        _surface.Rebuild();                       // the engine has a geometry from the first frame
        ApplyOutputState();

        _clock = new FrameClock();
        _stats = services.Log.DiagnosticsEnabled
            ? new FrameStats("preview", services.Log, _outputState.Geometry.RefreshHz, TimeSpan.FromSeconds(10))
            : null;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Detach()
    {
        CompositionTarget.Rendering -= OnRendering;
        if (_services is not null) _services.Output.StateChanged -= OnOutputStateChanged;
        if (_surface is not null)
        {
            _surface.Rebuilt -= OnSurfaceRebuilt;
            _surface.Stop();
            _scene.SetSurface(null);
        }

        _surface = null;
        _services = null;
        _stats = null;
        EndDrag();
    }

    /// <summary>The surface (tests).</summary>
    internal PrompterSurface? Surface => _surface;

    internal PreviewScene Scene => _scene;

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

    // ------------------------------------------------------------------ per frame (UI thread)

    private void OnRendering(object? sender, EventArgs e)
    {
        var services = _services;
        var surface = _surface;
        if (services is null || surface is null) return;
        if (!_clock.TryMap(((RenderingEventArgs)e).RenderingTime, Stopwatch.GetTimestamp(), out var frame)) return;

        // The output ticks the engine every frame while it renders; this only ticks when it has not for 50 ms.
        services.Engine.TickIfStale(Stopwatch.GetTimestamp());
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
        if (_surface is not null) _surface.Opacity = _outputState.IsBlank ? BlankDim : 1.0;
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
