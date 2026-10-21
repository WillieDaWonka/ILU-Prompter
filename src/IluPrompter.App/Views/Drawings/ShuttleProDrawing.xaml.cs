using System.Windows.Controls;
using System.Windows.Shapes;
using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Views.Drawings;

public partial class ShuttleProDrawing : UserControl, IControllerDrawing
{
    private const double Cx = 160;
    private const double Cy = 68;

    private readonly DrawingPainter _painter;
    private readonly IReadOnlyList<Line> _ticks;
    private readonly (string Id, Shape Shape)[] _buttons;
    private readonly Ellipse[] _dimples;
    private ControllerFrame? _last;
    private bool _lastGround;

    public ShuttleProDrawing()
    {
        InitializeComponent();
        _painter = new DrawingPainter(this);
        _ticks = ContourRing.AddTicks(Root, Cx, Cy, 22.8, 27.2, _painter.LineStrong, _painter.TextMuted);
        _buttons = ShuttleProLayout.Buttons
            .Select(n => (ControlIds.ShuttleButton(n), (Shape)(FindName(ShuttleProLayout.PartName(ShuttleProLayout.PositionOf(n))) ?? throw new InvalidOperationException("Button " + n))))
            .ToArray();
        _dimples = [Jog_Dimple1, Jog_Dimple2, Jog_Dimple3];
        Apply(ControllerFrame.Empty with { Kind = ControllerDrawingKind.ShuttlePro, Shuttle = new ShuttleFrame(false, 0, IndicatorLook.Unbound, false, 0, false), Signature = "initial" }, false);
    }

    public void Apply(ControllerFrame frame, bool raisedGround)
    {
        if (_last is not null && _last.Equals(frame) && _lastGround == raisedGround) return;
        _last = frame;
        _lastGround = raisedGround;
        _painter.RaisedGround = raisedGround;
        var missing = frame.Missing;
        var shuttle = frame.Shuttle ?? new ShuttleFrame(false, 0, IndicatorLook.Unbound, false, 0, false);

        _painter.Outline(Body, missing, 1.5);
        foreach (var (id, shape) in _buttons)
        {
            if (missing) _painter.Dim(shape);
            else _painter.Paint(shape, frame.Look(id));
        }

        var ringStroke = missing || !shuttle.RingBound ? _painter.Disabled : _painter.TextMuted;
        var ringThickness = missing || !shuttle.RingBound ? 1 : 1.5;
        _painter.Stroke(Ring, ringStroke, ringThickness);
        _painter.Stroke(Ring_Inner, ringStroke, ringThickness);
        for (var i = 0; i < _ticks.Count; i++)
        {
            _ticks[i].Stroke = missing || !shuttle.RingBound ? _painter.Disabled : i == 7 ? _painter.TextMuted : _painter.LineStrong;
        }

        ContourRing.ApplyRing(shuttle, _painter, Ring_Arc, Ring_Now, Cx, Cy, 23.6, 26.4, missing);

        if (missing)
        {
            _painter.Dim(Jog);
            DrawingPainter.Show(Jog_Dimples, false);
            return;
        }

        _painter.Paint(Jog, shuttle.JogBound ? PartLook.Bound : PartLook.Unbound);
        if (shuttle.JogLit) _painter.Stroke(Jog, shuttle.JogBound ? _painter.Text : _painter.LineStrong, 1.5);

        DrawingPainter.Show(Jog_Dimples, true);
        Jog_Rotate.Angle = shuttle.JogAngle;
        var fill = shuttle.JogBound ? _painter.TextMuted : _painter.LineStrong;
        foreach (var dimple in _dimples) dimple.Fill = fill;
    }
}
