using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using IluPrompter.App.Logic;

namespace IluPrompter.App.Views.Drawings;

/// <summary>A controller drawing (DESIGN.md "Controller drawing"): a 320 x 160 design box, parts named by ControlId.</summary>
public interface IControllerDrawing
{
    /// <summary>
    /// Show <paramref name="frame"/>. Called only when the frame changed (or the ground did). Every change is an instant
    /// step: no animation, no storyboard, no timer. <paramref name="raisedGround"/> is true in the flyout, where a bound
    /// control rests on <c>pressed</c> instead of <c>raised</c>.
    /// </summary>
    void Apply(ControllerFrame frame, bool raisedGround);
}

/// <summary>
/// Paints drawing parts from the design tokens (<c>Ilu.Brush.*</c>), by role: bound at rest, unbound at rest, lit, the
/// neutral flash, indicators and the Missing state. Never green, never <c>action</c> blue, never red. No hex colours.
/// </summary>
internal sealed class DrawingPainter
{
    private readonly Brush _raised;
    private readonly Brush _pressed;
    private readonly Brush _text;
    private readonly Brush _textStrong;
    private readonly Brush _textMuted;
    private readonly Brush _disabled;
    private readonly Brush _lineStrong;
    private readonly Brush _canvas;

    public DrawingPainter(FrameworkElement owner)
    {
        Brush Get(string key) => (Brush)owner.FindResource(key);
        _raised = Get("Ilu.Brush.Raised");
        _pressed = Get("Ilu.Brush.Pressed");
        _text = Get("Ilu.Brush.Text");
        _textStrong = Get("Ilu.Brush.TextStrong");
        _textMuted = Get("Ilu.Brush.TextMuted");
        _disabled = Get("Ilu.Brush.Disabled");
        _lineStrong = Get("Ilu.Brush.LineStrong");
        _canvas = Get("Ilu.Brush.Canvas");
    }

    public Brush Text => _text;

    public Brush TextStrong => _textStrong;

    public Brush TextMuted => _textMuted;

    public Brush Disabled => _disabled;

    public Brush LineStrong => _lineStrong;

    /// <summary>One step above the ground: <c>raised</c> on the <c>surface</c> panel, <c>pressed</c> in the flyout.</summary>
    public bool RaisedGround { get; set; }

    private Brush RestFill => RaisedGround ? _pressed : _raised;

    /// <summary>A button-like part: bound at rest, unbound (dim), lit, or the neutral flash.</summary>
    public void Paint(Shape shape, PartLook look)
    {
        switch (look)
        {
            case PartLook.Bound:
                Set(shape, RestFill, _textMuted, 1.5);
                break;
            case PartLook.Lit:
                Set(shape, _text, _text, 1.5);
                break;
            case PartLook.Flash:
                Set(shape, _lineStrong, _lineStrong, 1.5);
                break;
            default:
                Set(shape, null, _disabled, 1);
                break;
        }
    }

    /// <summary>The Xbox A, B, X and Y letters: muted on a bound button, dim on an unbound one, canvas on a lit one.</summary>
    public void Letter(TextBlock letter, PartLook look) =>
        letter.Foreground = look switch
        {
            PartLook.Lit or PartLook.Flash => _canvas,
            PartLook.Bound => _textMuted,
            _ => _disabled,
        };

    /// <summary>A part that is only an outline: the body, or something that is not an input (inert).</summary>
    public void Outline(Shape shape, bool missing, double thickness) => Set(shape, null, missing ? _disabled : _lineStrong, missing ? 1 : thickness);

    public void Indicator(Shape shape, IndicatorLook look) =>
        shape.Fill = look switch
        {
            IndicatorLook.Bound => _text,
            IndicatorLook.Idle => _textMuted,
            _ => _lineStrong,
        };

    /// <summary>Everything dim: no fill, a <c>disabled</c> stroke of 1.</summary>
    public void Dim(Shape shape) => Set(shape, null, _disabled, 1);

    public void Stroke(Shape shape, Brush stroke, double thickness) => Set(shape, shape.Fill, stroke, thickness);

    public static void Show(UIElement element, bool visible)
    {
        var wanted = visible ? Visibility.Visible : Visibility.Collapsed;
        if (element.Visibility != wanted) element.Visibility = wanted;
    }

    private static void Set(Shape shape, Brush? fill, Brush? stroke, double thickness)
    {
        shape.Fill = fill;
        shape.Stroke = stroke;
        shape.StrokeThickness = thickness;
    }

    /// <summary>Every <see cref="Shape"/> below <paramref name="root"/>, however deeply nested in canvases.</summary>
    public static IEnumerable<Shape> Shapes(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Shape shape) yield return shape;
            if (child is DependencyObject nested)
                foreach (var inner in Shapes(nested)) yield return inner;
        }
    }
}

/// <summary>The shuttle ring shared by the two Contour drawings: 15 ticks at 15 degree steps, an arc band and the tick "now".</summary>
internal static class ContourRing
{
    public const double Step = 15;

    private static readonly Dictionary<(int, int, int, int), Geometry> ArcCache = new();

    /// <summary>The 15 position ticks, -105 to +105 degrees from the top. Tick 0 is drawn longer and in <c>text-muted</c>.</summary>
    public static IReadOnlyList<Line> AddTicks(Canvas canvas, double cx, double cy, double rInner, double rOuter, Brush tick, Brush tickZero)
    {
        var ticks = new List<Line>();
        for (var k = -7; k <= 7; k++)
        {
            var line = new Line
            {
                X1 = cx, Y1 = cy - rOuter, X2 = cx, Y2 = cy - rInner,
                Stroke = k == 0 ? tickZero : tick,
                StrokeThickness = k == 0 ? 1.5 : 1,
                RenderTransform = new RotateTransform(k * Step, cx, cy),
                IsHitTestVisible = false,
            };
            canvas.Children.Add(line);
            ticks.Add(line);
        }

        return ticks;
    }

    /// <summary>The filled band from the top to 15k degrees between two radii (k from -7 to +7, not 0). Cached per k.</summary>
    public static Geometry Arc(double cx, double cy, double rInner, double rOuter, int k)
    {
        var key = (k, (int)(cx * 10) + (int)(cy * 10), (int)(rInner * 10), (int)(rOuter * 10));
        if (ArcCache.TryGetValue(key, out var cached)) return cached;

        var angle = k * Step * Math.PI / 180.0;
        var sin = Math.Sin(angle);
        var cos = Math.Cos(angle);
        var sweepOuter = k > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;
        var sweepInner = k > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(cx, cy - rOuter), isFilled: true, isClosed: true);
            context.ArcTo(new Point(cx + (rOuter * sin), cy - (rOuter * cos)), new Size(rOuter, rOuter), 0, false, sweepOuter, true, false);
            context.LineTo(new Point(cx + (rInner * sin), cy - (rInner * cos)), true, false);
            context.ArcTo(new Point(cx, cy - rInner), new Size(rInner, rInner), 0, false, sweepInner, true, false);
        }

        geometry.Freeze();
        ArcCache[key] = geometry;
        return geometry;
    }

    /// <summary>Show the ring position: the arc band, the "now" tick and the muted top tick at 0.</summary>
    public static void ApplyRing(ShuttleFrame shuttle, DrawingPainter painter, Path arc, Line now, double cx, double cy, double rInner, double rOuter, bool missing)
    {
        var showing = !missing && shuttle.RingK != 0;
        DrawingPainter.Show(arc, showing);
        DrawingPainter.Show(now, showing);
        if (!showing) return;
        arc.Data = Arc(cx, cy, rInner, rOuter, shuttle.RingK);
        painter.Indicator(arc, shuttle.RingArc);
        now.RenderTransform = new RotateTransform(shuttle.RingK * Step, cx, cy);
    }

    /// <summary>The jog wheel's dimple at a radius from the centre, <paramref name="degrees"/> clockwise from the top (see <see cref="ContourJog"/>).</summary>
    public static void PlaceDimple(Ellipse dimple, double cx, double cy, double radius, double degrees)
    {
        var angle = degrees * Math.PI / 180.0;
        Canvas.SetLeft(dimple, cx + (radius * Math.Sin(angle)) - (dimple.Width / 2));
        Canvas.SetTop(dimple, cy - (radius * Math.Cos(angle)) - (dimple.Height / 2));
    }
}
