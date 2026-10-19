using System.Globalization;
using System.Windows;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.Rendering;

/// <summary>Everything the overlay draws from; replaced as a whole so redraws only happen on real change.</summary>
internal readonly record struct PreviewVisualState(
    PreviewLayout Layout,
    TextArea Area,
    double OutputWidthDip,
    double OutputHeightDip,
    bool IsLive,
    bool IsBlank,
    PreviewHandle Hover,
    PreviewHandle Dragging);

/// <summary>
/// Preview-only drawing on top of the scaled output (DESIGN "Preview pane with margin overlay"): the frame, the margin
/// tint with its dashed inner edge, the reading line across the margins with its caption, the 4 px drag handle, and the
/// BLANK chip. Drawn in control coordinates at 1:1, so lines stay 1 px whatever the preview scale. Redrawn only when
/// <see cref="State"/> changes, never per frame.
/// </summary>
internal sealed class PreviewOverlay : FrameworkElement
{
    private const string BlankText = "■ BLANK · the presenter sees black";
    private const string ReadingLineText = "Reading line";

    private readonly Brush _marginTint = ThemeResources.GetBrush("margin-overlay");
    private readonly Brush _muted = ThemeResources.GetBrush("text-muted");
    private readonly Brush _handleBrush = ThemeResources.GetBrush("action-pressed");
    private readonly Brush _chipFill = ThemeResources.GetBrush("active");
    private readonly Brush _chipText = ThemeResources.GetBrush("on-active");
    private readonly Pen _frame = MakePen("line-strong", 1, dashed: false);
    private readonly Pen _frameLive = MakePen("live", 2, dashed: false);
    private readonly Pen _focusDash = MakePen("focus", 1, dashed: true);
    private readonly Pen _readingDash = MakePen("text-muted", 1, dashed: true);
    private readonly Typeface _caption = new(ThemeResources.UiFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private readonly Typeface _label = new(ThemeResources.UiFont, FontStyles.Normal, FontWeight.FromOpenTypeWeight(500), FontStretches.Normal);
    private PreviewVisualState _state;

    public PreviewOverlay()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public PreviewVisualState State
    {
        get => _state;
        set
        {
            if (_state.Equals(value)) return;
            _state = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var s = _state;
        if (s.Layout.Scale <= 0 || s.Layout.Stage.IsEmpty) return;

        var scale = s.Layout.Scale;
        var stage = s.Layout.Stage;
        var area = s.Area;
        var ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Frame: 1 px line-strong, 2 px live while the presenter can see the script. Outside the stage, so it never covers text.
        var framePen = s.IsLive ? _frameLive : _frame;
        var half = framePen.Thickness / 2;
        dc.DrawRectangle(null, framePen, new Rect(stage.X - half, stage.Y - half, stage.Width + framePen.Thickness, stage.Height + framePen.Thickness));

        // Margin tint: four bands around the text area.
        var left = stage.X + (area.Left * scale);
        var top = stage.Y + (area.Top * scale);
        var right = left + (area.Width * scale);
        var bottom = top + (area.Height * scale);
        dc.DrawRectangle(_marginTint, null, new Rect(stage.Left, stage.Top, stage.Width, Math.Max(0, top - stage.Top)));
        dc.DrawRectangle(_marginTint, null, new Rect(stage.Left, bottom, stage.Width, Math.Max(0, stage.Bottom - bottom)));
        dc.DrawRectangle(_marginTint, null, new Rect(stage.Left, top, Math.Max(0, left - stage.Left), Math.Max(0, bottom - top)));
        dc.DrawRectangle(_marginTint, null, new Rect(right, top, Math.Max(0, stage.Right - right), Math.Max(0, bottom - top)));
        dc.DrawRectangle(null, _focusDash, new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));

        // Reading line: dashed across the whole width (the output itself never shows it), caption at the left end.
        var lineY = top + (area.ReadingLineY * scale);
        dc.DrawLine(_readingDash, new Point(stage.Left, lineY), new Point(stage.Right, lineY));
        if (stage.Width >= 240)
        {
            var caption = new FormattedText(ReadingLineText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, _caption, 13, _muted, ppd);
            dc.DrawText(caption, new Point(stage.Left + 6, lineY - caption.Height - 1));
        }

        // Drag handle (4 px action-pressed) on the hovered or dragged edge.
        var active = s.Dragging != PreviewHandle.None ? s.Dragging : s.Hover;
        switch (active)
        {
            case PreviewHandle.Left:
                dc.DrawRectangle(_handleBrush, null, new Rect(left - 2, stage.Top, 4, stage.Height));
                break;
            case PreviewHandle.Right:
                dc.DrawRectangle(_handleBrush, null, new Rect(right - 2, stage.Top, 4, stage.Height));
                break;
            case PreviewHandle.Top:
                dc.DrawRectangle(_handleBrush, null, new Rect(stage.Left, top - 2, stage.Width, 4));
                break;
            case PreviewHandle.Bottom:
                dc.DrawRectangle(_handleBrush, null, new Rect(stage.Left, bottom - 2, stage.Width, 4));
                break;
            case PreviewHandle.ReadingLine:
                dc.DrawRectangle(_handleBrush, null, new Rect(left, lineY - 2, right - left, 4));
                break;
        }

        // BLANK: the operator still sees the script (dimmed by the host) plus the chip.
        if (s.IsBlank)
        {
            var text = new FormattedText(BlankText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, _label, 13, _chipText, ppd);
            var chipWidth = text.WidthIncludingTrailingWhitespace + 16;   // padding 0 8
            var chipHeight = 28.0;                                         // Ilu.Size.Chip
            var chip = new Rect(stage.X + ((stage.Width - chipWidth) / 2), stage.Y + ((stage.Height - chipHeight) / 2), chipWidth, chipHeight);
            dc.DrawRoundedRectangle(_chipFill, null, chip, 2, 2);
            dc.DrawText(text, new Point(chip.X + 8, chip.Y + ((chipHeight - text.Height) / 2)));
        }
    }

    private static Pen MakePen(string token, double thickness, bool dashed)
    {
        var pen = new Pen(ThemeResources.GetBrush(token), thickness);
        if (dashed)
        {
            pen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
        }

        pen.Freeze();
        return pen;
    }
}
