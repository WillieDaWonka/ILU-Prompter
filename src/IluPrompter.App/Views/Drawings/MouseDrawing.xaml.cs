using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Views.Drawings;

public partial class MouseDrawing : UserControl, IControllerDrawing
{
    private readonly DrawingPainter _painter;
    private ControllerFrame? _last;
    private bool _lastGround;

    public MouseDrawing()
    {
        InitializeComponent();
        _painter = new DrawingPainter(this);
        // Ten treads, 12 apart, plus one above the first so the wrap is seamless: y = 6 + 12 i + 3 p inside the wheel.
        for (var i = -1; i <= 9; i++)
        {
            Treads.Children.Add(new Line { X1 = 8, X2 = 32, Y1 = 6 + (12 * i), Y2 = 6 + (12 * i), Stroke = _painter.Text, StrokeThickness = 1.5 });
        }

        Apply(ControllerFrame.Empty with { Kind = ControllerDrawingKind.Mouse, Wheel = new WheelFrame(false, 0, false, false), Signature = "initial" }, false);
    }

    public void Apply(ControllerFrame frame, bool raisedGround)
    {
        if (_last is not null && _last.Equals(frame) && _lastGround == raisedGround) return;
        _last = frame;
        _lastGround = raisedGround;
        _painter.RaisedGround = raisedGround;

        var missing = frame.Missing;
        var wheel = frame.Wheel ?? new WheelFrame(false, 0, false, false);

        _painter.Outline(Body, missing, 1.5);
        _painter.Outline(Inert1, missing, 1);
        _painter.Outline(Inert2, missing, 1);
        _painter.Outline(Callout, missing, 1);
        foreach (var (id, shape) in new[] { (ControlIds.MiddleButton, (Shape)Middle), (ControlIds.XButton1, XButton1), (ControlIds.XButton2, XButton2) })
        {
            if (missing) _painter.Dim(shape);
            else _painter.Paint(shape, frame.Look(id));
        }

        if (missing)
        {
            _painter.Dim(Wheel);
            _painter.Dim(Wheel_Up);
            _painter.Dim(Wheel_Down);
        }
        else
        {
            _painter.Paint(Wheel, wheel.Bound ? PartLook.Bound : PartLook.Unbound);
            // the small wheel's edge lights with the chevrons
            if (wheel.UpLit || wheel.DownLit) _painter.Stroke(Middle, wheel.Bound ? _painter.Text : _painter.LineStrong, 1.5);
            _painter.Stroke(Wheel_Up, wheel.UpLit ? (wheel.Bound ? _painter.Text : _painter.LineStrong) : _painter.LineStrong, 2);
            _painter.Stroke(Wheel_Down, wheel.DownLit ? (wheel.Bound ? _painter.Text : _painter.LineStrong) : _painter.LineStrong, 2);
        }

        DrawingPainter.Show(Treads, !missing);
        TreadShift.Y = 3 * wheel.TreadPhase;
    }
}
