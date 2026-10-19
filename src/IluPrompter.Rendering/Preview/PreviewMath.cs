using System.Windows;
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>Where the scaled output sits inside the preview control (control coordinates, DIPs).</summary>
internal readonly record struct PreviewLayout(double Scale, Rect Stage)
{
    public static PreviewLayout Empty => new(0, Rect.Empty);
}

internal enum PreviewHandle
{
    None = 0,
    Left,
    Top,
    Right,
    Bottom,
    ReadingLine,
}

/// <summary>Pure maths for the preview: letterbox, hit testing the draggable margins and reading line, and turning a
/// pointer position into margin pixels or a reading-line fraction. Unit tested.</summary>
internal static class PreviewMath
{
    /// <summary>Room around the stage for the 1 px / 2 px frame.</summary>
    public const double FramePad = 2.0;

    /// <summary>How close (preview DIPs) the pointer must be to an edge to grab it.</summary>
    public const double HandleTolerance = 5.0;

    /// <summary>Shift snaps to this many output pixels (DESIGN: 8 px).</summary>
    public const double SnapPixels = 8.0;

    public const double MinReadingLine = 0.10;
    public const double MaxReadingLine = 0.60;

    /// <summary>Scale <c>s = min(w/W, h/H)</c> and the stage rectangle centred in the available space, at the output's aspect ratio.</summary>
    public static PreviewLayout ComputeLayout(Size available, double outputWidthDip, double outputHeightDip)
    {
        if (outputWidthDip <= 0 || outputHeightDip <= 0) return PreviewLayout.Empty;
        var w = Math.Max(0, Finite(available.Width, outputWidthDip + (2 * FramePad)) - (2 * FramePad));
        var h = Math.Max(0, Finite(available.Height, outputHeightDip + (2 * FramePad)) - (2 * FramePad));
        var s = Math.Min(w / outputWidthDip, h / outputHeightDip);
        if (!(s > 0)) return PreviewLayout.Empty;
        var stageW = outputWidthDip * s;
        var stageH = outputHeightDip * s;
        var x = FramePad + ((w - stageW) / 2);
        var y = FramePad + ((h - stageH) / 2);
        return new PreviewLayout(s, new Rect(x, y, stageW, stageH));
    }

    /// <summary>Desired size: fills a finite axis; an unbounded axis follows the output's aspect ratio.</summary>
    public static Size ComputeDesiredSize(Size available, double outputWidthDip, double outputHeightDip)
    {
        var aspect = outputHeightDip > 0 && outputWidthDip > 0 ? outputHeightDip / outputWidthDip : 9.0 / 16.0;
        var wFinite = !double.IsInfinity(available.Width) && !double.IsNaN(available.Width);
        var hFinite = !double.IsInfinity(available.Height) && !double.IsNaN(available.Height);
        if (wFinite && hFinite) return available;
        if (wFinite) return new Size(available.Width, ((available.Width - (2 * FramePad)) * aspect) + (2 * FramePad));
        if (hFinite) return new Size(((available.Height - (2 * FramePad)) / aspect) + (2 * FramePad), available.Height);
        return new Size(outputWidthDip + (2 * FramePad), outputHeightDip + (2 * FramePad));
    }

    /// <summary>Control coordinates to output DIPs.</summary>
    public static Point ToOutput(Point control, PreviewLayout layout) =>
        layout.Scale > 0
            ? new Point((control.X - layout.Stage.X) / layout.Scale, (control.Y - layout.Stage.Y) / layout.Scale)
            : new Point(double.NaN, double.NaN);

    /// <summary>True while the pointer is over the scaled script (the whole output rectangle).</summary>
    public static bool IsOverStage(Point control, PreviewLayout layout) =>
        layout.Scale > 0 && layout.Stage.Contains(control);

    /// <summary>The margin edge or reading line under the pointer, if any. Margin edges win over the reading line when
    /// equally close; the reading line only reacts inside the text area.</summary>
    public static PreviewHandle HitTest(Point control, PreviewLayout layout, TextArea area, double outputWidthDip, double outputHeightDip,
        double tolerance = HandleTolerance)
    {
        if (layout.Scale <= 0) return PreviewHandle.None;
        var o = ToOutput(control, layout);
        var s = layout.Scale;
        var tolOut = tolerance / s;

        var withinX = o.X >= -tolOut && o.X <= outputWidthDip + tolOut;
        var withinY = o.Y >= -tolOut && o.Y <= outputHeightDip + tolOut;
        var best = PreviewHandle.None;
        var bestDistance = double.MaxValue;

        void Consider(PreviewHandle handle, double distanceOutDip)
        {
            var d = Math.Abs(distanceOutDip) * s;
            if (d <= tolerance && d < bestDistance)
            {
                best = handle;
                bestDistance = d;
            }
        }

        if (withinY)
        {
            Consider(PreviewHandle.Left, o.X - area.Left);
            Consider(PreviewHandle.Right, o.X - (area.Left + area.Width));
        }

        if (withinX)
        {
            Consider(PreviewHandle.Top, o.Y - area.Top);
            Consider(PreviewHandle.Bottom, o.Y - (area.Top + area.Height));
            if (o.X >= area.Left && o.X <= area.Left + area.Width)
            {
                Consider(PreviewHandle.ReadingLine, o.Y - (area.Top + area.ReadingLineY));
            }
        }

        return best;
    }

    public static MarginEdge? ToEdge(PreviewHandle handle) => handle switch
    {
        PreviewHandle.Left => MarginEdge.Left,
        PreviewHandle.Top => MarginEdge.Top,
        PreviewHandle.Right => MarginEdge.Right,
        PreviewHandle.Bottom => MarginEdge.Bottom,
        _ => null,
    };

    /// <summary>Output pixels from the output edge to the pointer, for <see cref="MarginMath.SetEdgePixels"/>. Never negative.</summary>
    public static double EdgePixels(MarginEdge edge, Point outputDip, OutputGeometry output, bool snap)
    {
        var dpi = output.DpiScale;
        var px = edge switch
        {
            MarginEdge.Left => outputDip.X * dpi,
            MarginEdge.Right => (output.WidthDip - outputDip.X) * dpi,
            MarginEdge.Top => outputDip.Y * dpi,
            _ => (output.HeightDip - outputDip.Y) * dpi,
        };
        if (snap) px = Math.Round(px / SnapPixels) * SnapPixels;
        return double.IsNaN(px) ? 0 : Math.Max(0, px);
    }

    /// <summary>The reading-line fraction (of the text area) for a pointer position, clamped to 0.10..0.60; Shift snaps to 8 px.</summary>
    public static double ReadingLineFraction(Point outputDip, TextArea area, OutputGeometry output, bool snap)
    {
        if (area.Height <= 0 || double.IsNaN(outputDip.Y)) return Math.Clamp(0.33, MinReadingLine, MaxReadingLine);
        var y = outputDip.Y - area.Top;
        if (snap)
        {
            var px = Math.Round(y * output.DpiScale / SnapPixels) * SnapPixels;
            y = px / output.DpiScale;
        }

        return Math.Clamp(y / area.Height, MinReadingLine, MaxReadingLine);
    }

    private static double Finite(double value, double fallback) =>
        double.IsInfinity(value) || double.IsNaN(value) ? fallback : value;
}
