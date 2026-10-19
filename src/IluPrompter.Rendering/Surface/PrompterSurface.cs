using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Theme;

namespace IluPrompter.Rendering;

/// <summary>Everything a surface needs. <see cref="GetOutputGeometry"/> returns the display size layout must use
/// (<c>IOutputSurface.State.Geometry</c>), so output and preview always lay out identically.</summary>
internal sealed record SurfaceServices(
    IScrollEngine Engine,
    IPromptDocumentService Documents,
    ISettingsService Settings,
    IOutputDocumentFactory DocumentFactory,
    ILayoutGeometryBuilder GeometryBuilder,
    IAppLog Log,
    Func<OutputGeometry> GetOutputGeometry)
{
    /// <summary>The shared layout thread (ARCHITECTURE §3). Null: the surface lays out on its own thread, synchronously.</summary>
    public IPictureSource? Pictures { get; init; }
}

/// <summary>Raised on the output thread after each frame in which the scroll position changed (and as a heartbeat at least
/// four times a second while rendering). Lets the preview follow the output without its own per-frame Rendering
/// subscription (see PrompterPreview).</summary>
internal interface IFramePulse
{
    event EventHandler? FramePulse;
}

/// <summary>Seam for a future Direct2D/DirectWrite output (RESEARCH §1.3). Only the WPF renderer exists in v1.</summary>
internal interface IOutputRenderer
{
    /// <summary>The element hosted in the output window (output DIP size).</summary>
    FrameworkElement View { get; }

    void Start();

    void Stop();

    /// <summary>Any thread. Coalesces: at most one rebuild is queued.</summary>
    void RequestRebuild();

    /// <summary>Per frame, on the renderer's own thread: show document Y <paramref name="positionDip"/> at the top of the text area.</summary>
    void SetPosition(double positionDip);
}

/// <summary>
/// The prompter picture at the output's logical size (output DIPs): script-paper background, a clipped text area showing
/// the frozen picture of the laid-out script (<see cref="PicturePresenter"/>) moved with a pre-allocated
/// <see cref="TranslateTransform"/>, the reading-line markers and the optional dim band. One instance per thread (output
/// thread, UI thread for the preview); both display the same picture from the layout thread, so line breaks are
/// identical (ARCHITECTURE §3) and neither thread ever lays the script out.
/// <para>
/// Installing a picture, its frame and <c>engine.SetLayout</c> happen in one dispatcher operation, so the first frame of
/// a new layout already has the reading line on the same text. While the engine's layout is not this surface's picture
/// (the other surface installed a new one a frame earlier), positions are mapped onto this picture
/// (<see cref="LayoutMapping"/>), so neither surface ever shows a frame at the wrong place.
/// </para>
/// </summary>
internal sealed class PrompterSurface : UserControl, IOutputRenderer
{
    public const double SlowRebuildMs = 50;
    private const int StillFramesBeforeSnap = 2;

    private readonly string _name;
    private readonly SurfaceServices _s;
    private readonly IPictureSource _pictures;
    private readonly bool _snapToPixels;
    private readonly Canvas _root = new();
    private readonly Canvas _textArea = new() { ClipToBounds = true };
    private readonly PicturePresenter _presenter = new();
    private readonly TranslateTransform _translate = new();
    private readonly Rectangle _dim = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Path _leftMarker = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Path _rightMarker = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Action _rebuildAction;
    private readonly EventHandler _pictureReady;

    private int _rebuildPending;
    private bool _started;
    private PromptPicture? _picture;
    private TextArea _area;
    private OutputGeometry _output;
    private double _lastPosition = double.NaN;
    private int _stillFrames;
    private bool _animatedHinting = true;
    private LayoutGeometry? _mapFrom;
    private AnchorMap _map = AnchorMap.Identity;

    public PrompterSurface(string name, SurfaceServices services, bool snapToPixels)
    {
        _name = name;
        _s = services;
        _snapToPixels = snapToPixels;
        _rebuildAction = OnRebuildQueued;
        _pictureReady = (_, _) => RequestRebuild();
        _pictures = services.Pictures ?? new InlinePictureSource(services.Documents, services.Settings, services.DocumentFactory,
            services.GeometryBuilder, services.Log, services.GetOutputGeometry, "surface:" + name);

        var paper = ThemeResources.GetBrush("script-paper");
        var markerBrush = ThemeResources.GetBrush("reading-marker");
        var dimColour = ThemeResources.GetColor("script-paper");
        var dimBrush = new SolidColorBrush(dimColour) { Opacity = 0.55 };
        dimBrush.Freeze();

        _dim.Fill = dimBrush;
        _leftMarker.Fill = markerBrush;
        _rightMarker.Fill = markerBrush;

        _root.Background = paper;
        _root.SnapsToDevicePixels = false;

        _presenter.RenderTransform = _translate;
        Canvas.SetLeft(_presenter, 0);
        Canvas.SetTop(_presenter, 0);
        _textArea.Children.Add(_presenter);
        _root.Children.Add(_textArea);
        _root.Children.Add(_dim);
        _root.Children.Add(_leftMarker);
        _root.Children.Add(_rightMarker);
        Content = _root;

        Focusable = false;
        IsHitTestVisible = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Animated);

        _output = services.GetOutputGeometry();
        _area = TypographyMath.ComputeTextArea(_output, services.Settings.Current.Margins, services.Settings.Current.Text, services.Settings.Current.Output);
        ApplyFrame(_output, _area, services.Settings.Current);
    }

    public FrameworkElement View => this;

    /// <summary>The text area (output DIPs) in use.</summary>
    public TextArea TextArea => _area;

    /// <summary>The output size (DIPs and pixels) in use.</summary>
    public OutputGeometry Output => _output;

    /// <summary>Geometry tables of the picture on show; null before the first.</summary>
    public LayoutGeometry? Geometry => _picture?.Geometry;

    public string? LayoutKey => _picture?.Geometry.LayoutKey;

    /// <summary>How many new pictures were installed (not counting viewport-only refreshes).</summary>
    public int DocumentBuildCount { get; private set; }

    /// <summary>How many times <see cref="Rebuild"/> ran.</summary>
    public int RebuildCount { get; private set; }

    /// <summary>The element showing the picture (tests inspect it).</summary>
    internal PicturePresenter Presenter => _presenter;

    internal IPictureSource Pictures => _pictures;

    /// <summary>Raised on the surface's thread after every rebuild (the preview redraws its overlay).</summary>
    public event EventHandler? Rebuilt;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _s.Settings.Changed += OnSettingsChanged;
        if (_pictures.IsAutonomous) _pictures.PictureReady += _pictureReady;   // the layout thread says when it has a picture
        else _s.Documents.SnapshotPublished += OnSnapshotPublished;            // an inline source builds when asked
        RequestRebuild();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _s.Settings.Changed -= OnSettingsChanged;
        if (_pictures.IsAutonomous) _pictures.PictureReady -= _pictureReady;
        else _s.Documents.SnapshotPublished -= OnSnapshotPublished;
    }

    public void RequestRebuild()
    {
        if (Interlocked.Exchange(ref _rebuildPending, 1) == 1) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, _rebuildAction);
    }

    private void OnRebuildQueued()
    {
        Interlocked.Exchange(ref _rebuildPending, 0);
        if (_started) Rebuild();
    }

    private void OnSnapshotPublished(object? sender, PromptDocumentSnapshot snapshot) => RequestRebuild();

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var o = e.Old;
        var n = e.New;
        if (!Equals(o.Output, n.Output) || !Equals(o.Margins, n.Margins) || !Equals(o.Text, n.Text) || !Equals(o.Editor, n.Editor))
        {
            RequestRebuild();
        }
    }

    /// <summary>
    /// Synchronous, on the surface's thread: asks the picture source for the current inputs (an inline source lays out
    /// now; the layout thread answers later with <c>PictureReady</c>), then installs the newest picture, the frame that
    /// goes with it and <c>engine.SetLayout</c>, and repositions. Cheap unless the source is inline. Never throws.
    /// </summary>
    public void Rebuild()
    {
        var timer = Stopwatch.StartNew();
        var installed = false;
        try
        {
            _pictures.Request();
            installed = Apply();
        }
        catch (Exception ex)
        {
            _s.Log.Error("surface:" + _name, "Rebuild failed", ex);
        }

        RebuildCount++;
        var ms = timer.Elapsed.TotalMilliseconds;
        if (ms > SlowRebuildMs)
        {
            _s.Log.Warn("surface:" + _name, string.Create(CultureInfo.InvariantCulture,
                $"Slow rebuild: {ms:0} ms ({_picture?.Geometry.LineCount ?? 0} lines, key {_picture?.Geometry.LayoutKey})"));
        }
        else if (installed && _s.Log.DiagnosticsEnabled)
        {
            _s.Log.Trace("surface:" + _name + ":install", string.Create(CultureInfo.InvariantCulture,
                $"picture installed in {ms:0.0} ms ({_presenter.AttachedCount} blocks attached, {_presenter.ReusedCount} reused, key {_picture?.Geometry.LayoutKey})"));
        }

        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Install the newest picture (if new) with its frame, tell the engine, reposition. True if a picture was installed.</summary>
    private bool Apply()
    {
        var picture = _pictures.Current;
        var settings = _s.Settings.Current;
        var output = _s.GetOutputGeometry();
        var area = TypographyMath.ComputeTextArea(output, settings.Margins, settings.Text, settings.Output);
        var installed = false;

        if (picture is not null)
        {
            // The frame must match the picture: until the picture for new margins or a new size arrives, keep the size it
            // was laid out for (only the reading line, which never changes the layout, follows the settings at once).
            var key = SurfaceLayout.ComputeLayoutKey(picture.Geometry.SnapshotVersion, area, settings.Text, settings.Output);
            if (!string.Equals(key, picture.Geometry.LayoutKey, StringComparison.Ordinal))
            {
                var fraction = area.Height > 0 ? area.ReadingLineY / area.Height : 0.33;
                area = picture.Area with { ReadingLineY = picture.Area.Height * fraction };
                output = picture.Output;
            }

            if (!ReferenceEquals(picture, _picture))
            {
                var changedContent = _picture is null || !string.Equals(_picture.ContentKey, picture.ContentKey, StringComparison.Ordinal);
                _picture = picture;
                _mapFrom = null;
                if (changedContent) DocumentBuildCount++;
                installed = true;
                TraceLineTops(picture.Geometry);
            }
        }

        _output = output;
        _area = area;
        ApplyFrame(output, area, settings);

        var position = _s.Engine.ReadPosition();
        if (_picture is not null)
        {
            _s.Engine.SetLayout(_picture.Geometry, area.ToViewport());
            position = _s.Engine.ReadPosition(out var engineGeometry);
            position = MapFromEngine(position, engineGeometry);
            if (installed) _presenter.SetPicture(_picture, position, area.Height);
        }

        _lastPosition = double.NaN;
        ShowPosition(position);
        return installed;
    }

    /// <summary>Per frame (tests and simple callers): a position already in this surface's layout.</summary>
    public void SetPosition(double positionDip) => ShowPosition(positionDip);

    /// <summary>Per frame: the engine's position and the geometry it is in (<c>IScrollEngine.ReadPosition(out)</c>). If that
    /// is not this surface's picture yet, the position is mapped onto the picture first. Allocation-free except once
    /// per layout change when no map was prepared.</summary>
    public void SetPosition(double positionDip, LayoutGeometry? engineGeometry) => ShowPosition(MapFromEngine(positionDip, engineGeometry));

    private double MapFromEngine(double position, LayoutGeometry? engineGeometry)
    {
        var mine = _picture?.Geometry;
        if (engineGeometry is null || mine is null || ReferenceEquals(engineGeometry, mine)
            || string.Equals(engineGeometry.LayoutKey, mine.LayoutKey, StringComparison.Ordinal))
        {
            return position;
        }

        if (!ReferenceEquals(_mapFrom, engineGeometry))
        {
            // Usually the newest picture is the engine's and was built with the map back to this one.
            var newest = _pictures.Current;
            _map = newest is not null && ReferenceEquals(newest.Geometry, engineGeometry) && ReferenceEquals(newest.PreviousGeometry, mine) && newest.MapToPrevious is not null
                ? newest.MapToPrevious
                : AnchorMap.Between(engineGeometry, mine);
            _mapFrom = engineGeometry;
        }

        var mapped = LayoutMapping.MapPosition(engineGeometry, mine, _map, position, _area.ReadingLineY);
        return double.IsFinite(mapped) ? mapped : position;
    }

    /// <summary>Moving: exact sub-pixel Y with animated hinting. Still for 2 frames: Y snapped to a whole device pixel and
    /// fixed hinting, so static text is razor sharp (RESEARCH §4.1).</summary>
    private void ShowPosition(double positionDip)
    {
        if (positionDip != _lastPosition)
        {
            _lastPosition = positionDip;
            _stillFrames = 0;
            _translate.Y = -positionDip;
            _presenter.UpdateViewport(positionDip, _area.Height);
            if (!_animatedHinting)
            {
                _animatedHinting = true;
                _presenter.SetHinting(TextHintingMode.Animated);
            }

            return;
        }

        if (_stillFrames < StillFramesBeforeSnap && ++_stillFrames == StillFramesBeforeSnap && _snapToPixels)
        {
            var px = _output.DpiScale;
            var absolute = Math.Round((_area.Top - positionDip) * px) / px;
            _translate.Y = absolute - _area.Top;
            _animatedHinting = false;
            _presenter.SetHinting(TextHintingMode.Fixed);
        }
    }

    /// <summary>Size, text-area position, markers and dim band: everything cheap that depends on margins and the reading line.</summary>
    private void ApplyFrame(OutputGeometry output, TextArea area, PrompterSettings settings)
    {
        _root.Width = output.WidthDip;
        _root.Height = output.HeightDip;
        Width = output.WidthDip;
        Height = output.HeightDip;

        Canvas.SetLeft(_textArea, area.Left);
        Canvas.SetTop(_textArea, area.Top);
        _textArea.Width = area.Width;
        _textArea.Height = area.Height;

        var readingLineY = area.Top + area.ReadingLineY;
        var rightMargin = output.WidthDip - area.Left - area.Width;

        if (settings.Output.ShowReadingMarker)
        {
            var markers = SurfaceLayout.ComputeMarkers(output.WidthDip, area.Left, rightMargin, area.BaseLineHeight, readingLineY);
            ApplyMarker(_leftMarker, markers.Left, pointsRight: true);
            ApplyMarker(_rightMarker, markers.Right, pointsRight: false);
        }
        else
        {
            _leftMarker.Visibility = Visibility.Collapsed;
            _rightMarker.Visibility = Visibility.Collapsed;
        }

        var band = SurfaceLayout.ComputeDimBand(area);
        if (settings.Output.DimAboveReadingLine && band.Height > 0)
        {
            Canvas.SetLeft(_dim, band.X);
            Canvas.SetTop(_dim, band.Y);
            _dim.Width = band.Width;
            _dim.Height = band.Height;
            _dim.Visibility = Visibility.Visible;
        }
        else
        {
            _dim.Visibility = Visibility.Collapsed;
        }
    }

    private static void ApplyMarker(Path path, MarkerShape shape, bool pointsRight)
    {
        var r = shape.Bounds;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (shape.Kind == MarkerKind.Triangle)
            {
                if (pointsRight)
                {
                    ctx.BeginFigure(new Point(r.Left, r.Top), true, true);
                    ctx.LineTo(new Point(r.Right, r.Top + (r.Height / 2)), true, false);
                    ctx.LineTo(new Point(r.Left, r.Bottom), true, false);
                }
                else
                {
                    ctx.BeginFigure(new Point(r.Right, r.Top), true, true);
                    ctx.LineTo(new Point(r.Right, r.Bottom), true, false);
                    ctx.LineTo(new Point(r.Left, r.Top + (r.Height / 2)), true, false);
                }
            }
            else
            {
                ctx.BeginFigure(r.TopLeft, true, true);
                ctx.LineTo(r.TopRight, true, false);
                ctx.LineTo(r.BottomRight, true, false);
                ctx.LineTo(r.BottomLeft, true, false);
            }
        }

        geometry.Freeze();
        path.Data = geometry;
        path.Visibility = Visibility.Visible;
    }

    private void TraceLineTops(LayoutGeometry geometry)
    {
        if (!_s.Log.DiagnosticsEnabled) return;
        var c = CultureInfo.InvariantCulture;
        var tops = string.Join(",", geometry.LineTops.Take(50).Select(t => t.ToString("0.0", c)));
        _s.Log.Trace("surface:" + _name, string.Create(c,
            $"layout key {geometry.LayoutKey}: {geometry.LineCount} lines, {geometry.ParagraphCount} paragraphs, base line {geometry.BaseLineHeight:0.00}, doc height {geometry.DocumentHeight:0.0}, first 50 tops [{tops}]"));
    }
}
