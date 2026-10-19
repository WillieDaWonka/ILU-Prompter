using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
    Func<OutputGeometry> GetOutputGeometry);

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
/// The prompter picture at the output's logical size (output DIPs): script-paper background, a clipped text area
/// hosting a read-only RichTextBox that is moved with a pre-allocated <see cref="TranslateTransform"/>, the reading-line
/// markers and the optional dim band. One instance per thread (output thread, UI thread for the preview); each owns its
/// FlowDocument built from the same snapshot, so line breaks are identical (ARCHITECTURE §3).
/// </summary>
internal sealed class PrompterSurface : UserControl, IOutputRenderer
{
    public const double SlowRebuildMs = 50;
    private const int StillFramesBeforeSnap = 2;

    private readonly string _name;
    private readonly SurfaceServices _s;
    private readonly bool _snapToPixels;
    private readonly Canvas _root = new();
    private readonly Canvas _textArea = new() { ClipToBounds = true };
    private readonly RichTextBox _host = new();
    private readonly TranslateTransform _translate = new();
    private readonly Rectangle _dim = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Path _leftMarker = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Path _rightMarker = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly Action _rebuildAction;

    private int _rebuildPending;
    private bool _started;
    private string? _contentKey;
    private TextArea _area;
    private OutputGeometry _output;
    private LayoutGeometry? _geometry;
    private double _lastPosition = double.NaN;
    private int _stillFrames;
    private bool _animatedHinting = true;

    public PrompterSurface(string name, SurfaceServices services, bool snapToPixels)
    {
        _name = name;
        _s = services;
        _snapToPixels = snapToPixels;
        _rebuildAction = OnRebuildQueued;

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

        ConfigureHost();
        _textArea.Children.Add(_host);
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

    /// <summary>The text area (output DIPs) used by the last rebuild.</summary>
    public TextArea TextArea => _area;

    /// <summary>The output size (DIPs and pixels) used by the last rebuild.</summary>
    public OutputGeometry Output => _output;

    /// <summary>Geometry tables of the last successful document build; null before the first.</summary>
    public LayoutGeometry? Geometry => _geometry;

    public string? LayoutKey => _geometry?.LayoutKey;

    /// <summary>How many times the FlowDocument was rebuilt (not counting viewport-only refreshes).</summary>
    public int DocumentBuildCount { get; private set; }

    /// <summary>How many times <see cref="Rebuild"/> ran.</summary>
    public int RebuildCount { get; private set; }

    /// <summary>The hosted read-only text box (tests inspect its layout).</summary>
    internal RichTextBox Host => _host;

    /// <summary>Raised on the surface's thread after every rebuild (the preview redraws its overlay).</summary>
    public event EventHandler? Rebuilt;

    public void Start()
    {
        if (_started) return;
        _started = true;
        _s.Documents.SnapshotPublished += OnSnapshotPublished;
        _s.Settings.Changed += OnSettingsChanged;
        RequestRebuild();
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        _s.Documents.SnapshotPublished -= OnSnapshotPublished;
        _s.Settings.Changed -= OnSettingsChanged;
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
    /// Synchronous rebuild on the surface's thread: text area, then (only when something that changes the look or the
    /// line breaks changed) a new FlowDocument, host layout and geometry tables, then <c>engine.SetLayout</c>.
    /// Never throws; failures are logged and the previous picture stays.
    /// </summary>
    public void Rebuild()
    {
        var timer = Stopwatch.StartNew();
        try
        {
            RebuildCore();
        }
        catch (Exception ex)
        {
            _s.Log.Error("surface:" + _name, "Rebuild failed", ex);
        }

        RebuildCount++;
        if (timer.Elapsed.TotalMilliseconds > SlowRebuildMs)
        {
            _s.Log.Warn("surface:" + _name, string.Create(CultureInfo.InvariantCulture,
                $"Slow rebuild: {timer.Elapsed.TotalMilliseconds:0} ms ({_geometry?.LineCount ?? 0} lines, key {_geometry?.LayoutKey})"));
        }

        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildCore()
    {
        var snapshot = _s.Documents.Current;
        var settings = _s.Settings.Current;
        var output = _s.GetOutputGeometry();
        var area = TypographyMath.ComputeTextArea(output, settings.Margins, settings.Text, settings.Output);
        var layoutKey = SurfaceLayout.ComputeLayoutKey(snapshot.Version, area, settings.Text, settings.Output);
        var contentKey = SurfaceLayout.ComputeContentKey(layoutKey, settings.Text);

        _output = output;
        _area = area;
        ApplyFrame(output, area, settings);

        if (!string.Equals(contentKey, _contentKey, StringComparison.Ordinal))
        {
            var style = BuildStyle(area, settings);
            var document = _s.DocumentFactory.Create(snapshot, style);
            _host.Document = document;
            LayoutHost(area.Width);
            _geometry = _s.GeometryBuilder.Build(document, new GeometryBuildOptions(layoutKey, snapshot, area.BaseLineHeight));
            _contentKey = contentKey;
            DocumentBuildCount++;
            TraceLineTops(_geometry);
        }
        else
        {
            LayoutHost(area.Width);
        }

        if (_geometry is not null) _s.Engine.SetLayout(_geometry, area.ToViewport());

        _lastPosition = double.NaN;
        SetPosition(_s.Engine.ReadPosition());
    }

    /// <summary>Per frame. Moving: exact sub-pixel Y with animated hinting. Still for 2 frames: Y snapped to a whole
    /// device pixel and fixed hinting, so static text is razor sharp (RESEARCH §4.1).</summary>
    public void SetPosition(double positionDip)
    {
        if (positionDip != _lastPosition)
        {
            _lastPosition = positionDip;
            _stillFrames = 0;
            _translate.Y = -positionDip;
            if (!_animatedHinting)
            {
                _animatedHinting = true;
                TextOptions.SetTextHintingMode(_host, TextHintingMode.Animated);
            }

            return;
        }

        if (_stillFrames < StillFramesBeforeSnap && ++_stillFrames == StillFramesBeforeSnap && _snapToPixels)
        {
            var px = _output.DpiScale;
            var absolute = Math.Round((_area.Top - positionDip) * px) / px;
            _translate.Y = absolute - _area.Top;
            _animatedHinting = false;
            TextOptions.SetTextHintingMode(_host, TextHintingMode.Fixed);
        }
    }

    private void ConfigureHost()
    {
        _host.IsReadOnly = true;
        _host.IsReadOnlyCaretVisible = false;
        _host.Focusable = false;
        _host.IsHitTestVisible = false;
        _host.IsTabStop = false;
        _host.IsUndoEnabled = false;
        _host.IsDocumentEnabled = false;
        _host.AllowDrop = false;
        _host.BorderThickness = new Thickness(0);
        _host.Padding = new Thickness(0);
        _host.Margin = new Thickness(0);
        _host.Background = Brushes.Transparent;
        _host.FocusVisualStyle = null;
        _host.IsInactiveSelectionHighlightEnabled = false;
        _host.SelectionOpacity = 0;
        _host.CaretBrush = Brushes.Transparent;
        _host.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _host.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _host.HorizontalAlignment = HorizontalAlignment.Left;
        _host.VerticalAlignment = VerticalAlignment.Top;
        _host.RenderTransform = _translate;
        TextOptions.SetTextFormattingMode(_host, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(_host, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(_host, TextHintingMode.Animated);   // not inherited as a property value: set where it is used
        _host.UndoLimit = 0;
        SpellCheck.SetIsEnabled(_host, false);
        Canvas.SetLeft(_host, 0);
        Canvas.SetTop(_host, 0);
    }

    private void LayoutHost(double width)
    {
        _host.Width = width;
        _host.Measure(new Size(width, double.PositiveInfinity));
        _host.Arrange(new Rect(0, 0, width, _host.DesiredSize.Height));
        _host.UpdateLayout();
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

    private static OutputTextStyle BuildStyle(TextArea area, PrompterSettings settings)
    {
        var text = settings.Text;
        var highlights = new Dictionary<Color, Color>();
        foreach (var option in ThemeResources.Highlights)
        {
            if (option.InkToken is not null && ThemeResources.TryGetColor(option.Token, out var bg) && ThemeResources.TryGetColor(option.InkToken, out var ink))
            {
                highlights[bg] = ink;
            }
        }

        return new OutputTextStyle(
            ThemeResources.ScriptFont(text.FontFace),
            FontWeight.FromOpenTypeWeight(Math.Clamp(text.FontWeight, 1, 999)),
            area.BaseFontSize,
            text.LineSpacing,
            text.ParagraphGapLines,
            settings.Output.Alignment == TextAlignmentSetting.Centre ? TextAlignment.Center : TextAlignment.Left,
            ThemeResources.TryGetColor(text.DefaultInk, out var defaultInk) ? defaultInk : ThemeResources.GetColor("ink-white"),
            ThemeResources.GetColor("ink-grey"),
            ThemeResources.GetColor("end-marker"),
            ThemeResources.GetColor("line-strong"),
            settings.Output.ShowSectionNames,
            settings.Editor.SectionMarker,
            highlights);
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
