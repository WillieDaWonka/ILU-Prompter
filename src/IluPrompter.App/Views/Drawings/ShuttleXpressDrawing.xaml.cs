using System.Windows.Controls;
using System.Windows.Shapes;
using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Views.Drawings;

public partial class ShuttleXpressDrawing : UserControl, IControllerDrawing
{
    private const double Cx = 160;
    private const double Cy = 100;

    private readonly DrawingPainter _painter;
    private readonly IReadOnlyList<Line> _ticks;
    private readonly (string Id, Shape Shape)[] _buttons;
    private ControllerFrame? _last;
    private bool _lastGround;

    public ShuttleXpressDrawing()
    {
        InitializeComponent();
        _painter = new DrawingPainter(this);
        _ticks = ContourRing.AddTicks(Root, Cx, Cy, 37, 47, _painter.LineStrong, _painter.TextMuted);
        _buttons = [(ControlIds.ShuttleButton(1), Button1), (ControlIds.ShuttleButton(2), Button2), (ControlIds.ShuttleButton(3), Button3), (ControlIds.ShuttleButton(4), Button4), (ControlIds.ShuttleButton(5), Button5)];
        Apply(ControllerFrame.Empty with { Kind = ControllerDrawingKind.ShuttleXpress, Shuttle = new ShuttleFrame(false, 0, IndicatorLook.Unbound, false, 0, false), Signature = "initial" }, false);
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

        // Ring: muted when bound, dim when not; the ticks follow
        var ringStroke = missing || !shuttle.RingBound ? _painter.Disabled : _painter.TextMuted;
        var ringThickness = missing || !shuttle.RingBound ? 1 : 1.5;
        _painter.Stroke(Ring, ringStroke, ringThickness);
        _painter.Stroke(Ring_Inner, ringStroke, ringThickness);
        for (var i = 0; i < _ticks.Count; i++)
        {
            _ticks[i].Stroke = missing || !shuttle.RingBound ? _painter.Disabled : i == 7 ? _painter.TextMuted : _painter.LineStrong;
        }

        ContourRing.ApplyRing(shuttle, _painter, Ring_Arc, Ring_Now, Cx, Cy, 39, 45, missing);

        if (missing)
        {
            _painter.Dim(Jog);
            DrawingPainter.Show(Jog_Dimple, false);
        }
        else
        {
            _painter.Paint(Jog, shuttle.JogBound ? PartLook.Bound : PartLook.Unbound);
            // the jog's edge lights for 400 ms after a detent
            if (shuttle.JogLit) _painter.Stroke(Jog, shuttle.JogBound ? _painter.Text : _painter.LineStrong, 1.5);
            DrawingPainter.Show(Jog_Dimple, true);
            Jog_Dimple.Fill = shuttle.JogBound ? _painter.TextMuted : _painter.LineStrong;
            ContourRing.PlaceDimple(Jog_Dimple, Cx, Cy, 22, shuttle.JogAngle);
        }
    }
}
