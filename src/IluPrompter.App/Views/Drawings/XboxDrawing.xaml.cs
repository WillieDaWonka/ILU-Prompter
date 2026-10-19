using System.Windows.Controls;
using System.Windows.Shapes;
using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Views.Drawings;

public partial class XboxDrawing : UserControl, IControllerDrawing
{
    private static readonly (double X, double Y) LeftStickCentre = (92, 58);
    private static readonly (double X, double Y) RightStickCentre = (196, 90);
    private const double TriggerTop = 4;
    private const double TriggerHeight = 60;
    private const double DotRadius = 5;

    private readonly DrawingPainter _painter;
    private readonly (string Id, Shape Shape)[] _buttons;
    private readonly (string Id, Shape Shape, TextBlock? Letter)[] _faces;
    private ControllerFrame? _last;
    private bool _lastGround;
    private bool _small;

    public XboxDrawing()
    {
        InitializeComponent();
        _painter = new DrawingPainter(this);
        _buttons =
        [
            (ControlIds.LeftShoulder, LeftShoulder), (ControlIds.RightShoulder, RightShoulder), (ControlIds.View, Back), (ControlIds.Menu, Start),
            (ControlIds.DPadUp, DPadUp), (ControlIds.DPadDown, DPadDown), (ControlIds.DPadLeft, DPadLeft), (ControlIds.DPadRight, DPadRight),
        ];
        _faces =
        [
            (ControlIds.A, A, Letter_A), (ControlIds.B, B, Letter_B), (ControlIds.X, X, Letter_X), (ControlIds.Y, Y, Letter_Y),
        ];
        Apply(ControllerFrame.Empty with { Kind = ControllerDrawingKind.Xbox, Signature = "initial" }, false);
    }

    /// <summary>The A, B, X and Y letters are in the large drawing only: text never shrinks below 13 DIP.</summary>
    public bool Small
    {
        get => _small;
        set
        {
            if (_small == value) return;
            _small = value;
            foreach (var (_, _, letter) in _faces) DrawingPainter.Show(letter!, !value);
        }
    }

    public void Apply(ControllerFrame frame, bool raisedGround)
    {
        if (_last is not null && _last.Equals(frame) && _lastGround == raisedGround) return;
        _last = frame;
        _lastGround = raisedGround;
        _painter.RaisedGround = raisedGround;
        var missing = frame.Missing;

        _painter.Outline(Body, missing, 1.5);
        _painter.Outline(Guide, missing, 1);
        _painter.Outline(DPadCentre, missing, 1);

        foreach (var (id, shape) in _buttons)
        {
            if (missing) _painter.Dim(shape);
            else _painter.Paint(shape, frame.Look(id));
        }

        foreach (var (id, shape, letter) in _faces)
        {
            var look = missing ? PartLook.Unbound : frame.Look(id);
            if (missing) _painter.Dim(shape);
            else _painter.Paint(shape, look);
            _painter.Letter(letter!, look);
        }

        foreach (var stick in new[] { (ControlIds.LeftThumbPress, LeftThumb, LeftThumb_Dz, LeftThumb_Dot, LeftStickCentre), (ControlIds.RightThumbPress, RightThumb, RightThumb_Dz, RightThumb_Dot, RightStickCentre) })
            ApplyStick(frame.Sticks.FirstOrDefault(s => s.Id == stick.Item1), stick.Item2, stick.Item3, stick.Item4, stick.Item5, missing);

        foreach (var trigger in new[] { (ControlIds.LeftTrigger, LeftTrigger, LeftTrigger_Fill, LeftTrigger_Tick), (ControlIds.RightTrigger, RightTrigger, RightTrigger_Fill, RightTrigger_Tick) })
            ApplyTrigger(frame.Triggers.FirstOrDefault(t => t.Id == trigger.Item1), trigger.Item2, trigger.Item3, trigger.Item4, missing);
    }

    private void ApplyStick(StickFrame? stick, Ellipse ring, Ellipse deadzone, Ellipse dot, (double X, double Y) centre, bool missing)
    {
        if (missing || stick is null)
        {
            _painter.Dim(ring);
            DrawingPainter.Show(dot, false);
            DrawingPainter.Show(deadzone, false);
            return;
        }

        DrawingPainter.Show(dot, true);
        DrawingPainter.Show(deadzone, true);
        _painter.Paint(ring, stick.Bound ? PartLook.Bound : PartLook.Unbound);
        // A stick press lights the ring's stroke: text when bound, line-strong when not (the neutral flash).
        if (stick.Press != PressLook.None) _painter.Stroke(ring, stick.Press == PressLook.Lit ? _painter.Text : _painter.LineStrong, 2.5);

        var r = stick.DeadzoneRadius;
        deadzone.Width = deadzone.Height = 2 * r;
        Canvas.SetLeft(deadzone, centre.X - r);
        Canvas.SetTop(deadzone, centre.Y - r);
        Canvas.SetLeft(dot, centre.X + stick.DotDx - DotRadius);
        Canvas.SetTop(dot, centre.Y + stick.DotDy - DotRadius);
        _painter.Indicator(dot, stick.Dot);
    }

    private void ApplyTrigger(TriggerFrame? trigger, Rectangle body, Rectangle fill, Line tick, bool missing)
    {
        if (missing || trigger is null)
        {
            _painter.Dim(body);
            DrawingPainter.Show(fill, false);
            tick.Stroke = _painter.Disabled;
            return;
        }

        _painter.Paint(body, trigger.Bound ? PartLook.Bound : PartLook.Unbound);
        tick.Stroke = _painter.TextMuted;
        Canvas.SetTop(tick, 0);
        tick.Y1 = tick.Y2 = TriggerTop + trigger.TickOffsetFromTop;
        DrawingPainter.Show(fill, trigger.FillPx > 0);
        fill.Height = trigger.FillPx;
        Canvas.SetTop(fill, TriggerTop + TriggerHeight - trigger.FillPx);
        _painter.Indicator(fill, trigger.Fill);
    }
}
