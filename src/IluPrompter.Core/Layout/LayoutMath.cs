using System.Globalization;

namespace IluPrompter.Core;

/// <summary>Margins in pixels of the output display (what the margin editor shows).</summary>
public readonly record struct MarginPixels(double Left, double Top, double Right, double Bottom);

/// <summary>The text area on the output in DIPs (relative to the output window's top-left).</summary>
public readonly record struct TextArea(double Left, double Top, double Width, double Height, double ReadingLineY, double BaseFontSize, double BaseLineHeight)
{
    public ViewportMetrics ToViewport() => new(Width, Height, ReadingLineY);
}

/// <summary>Margin conversion and link behaviour. Signatures frozen; stream A owns the bodies and tests.</summary>
public static class MarginMath
{
    public const double MaxFraction = 0.40;

    /// <summary>The text area must keep at least this share of the width and of the height.</summary>
    internal const double MinTextAreaFraction = 0.20;

    public static MarginPixels ToPixels(MarginSettings m, PixelSize output) =>
        new(m.Left * output.Width, m.Top * output.Height, m.Right * output.Width, m.Bottom * output.Height);

    /// <summary>Set one edge in output pixels. Clamps to 0..MaxFraction of its dimension. When m.IsLinked, all four
    /// edges get the same pixel value (each clamped in its own dimension). Records LastEdited.</summary>
    public static MarginSettings SetEdgePixels(MarginSettings m, MarginEdge edge, double pixels, PixelSize output)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (output.Width <= 0 || output.Height <= 0 || double.IsNaN(pixels))
        {
            return m;
        }

        var px = RoundPixels(pixels);
        if (m.IsLinked)
        {
            return ApplyAll(m, px, output) with { LastEdited = edge };
        }

        var current = ToPixels(m, output);
        return edge switch
        {
            MarginEdge.Left => m with { Left = Fraction(px, output.Width, current.Right), LastEdited = edge },
            MarginEdge.Right => m with { Right = Fraction(px, output.Width, current.Left), LastEdited = edge },
            MarginEdge.Top => m with { Top = Fraction(px, output.Height, current.Bottom), LastEdited = edge },
            _ => m with { Bottom = Fraction(px, output.Height, current.Top), LastEdited = edge },
        };
    }

    /// <summary>Turn the link on or off. Turning it on copies LastEdited's pixel value to all four edges.</summary>
    public static MarginSettings SetLinked(MarginSettings m, bool linked, PixelSize output)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (!linked || output.Width <= 0 || output.Height <= 0)
        {
            return m with { IsLinked = linked };
        }

        var px = ToPixels(m, output);
        var source = m.LastEdited switch
        {
            MarginEdge.Left => px.Left,
            MarginEdge.Top => px.Top,
            MarginEdge.Right => px.Right,
            _ => px.Bottom,
        };

        return ApplyAll(m, RoundPixels(source), output) with { IsLinked = true };
    }

    private static MarginSettings ApplyAll(MarginSettings m, double px, PixelSize output) =>
        m with
        {
            Left = Fraction(px, output.Width, null),
            Right = Fraction(px, output.Width, null),
            Top = Fraction(px, output.Height, null),
            Bottom = Fraction(px, output.Height, null),
        };

    /// <summary>Clamp whole pixels to the per-edge maximum (and so keep the 20 % minimum text area), then
    /// convert to a fraction of the dimension.</summary>
    private static double Fraction(double px, int dimension, double? oppositePx)
    {
        var max = Math.Floor(MaxFraction * dimension);
        if (oppositePx is { } opposite)
        {
            // Keeps text width/height >= 20 % even when the opposite edge was loaded from an older file.
            max = Math.Min(max, Math.Floor(((1.0 - MinTextAreaFraction) * dimension) - opposite));
        }

        var clamped = Math.Clamp(px, 0.0, Math.Max(0.0, max));
        return clamped / dimension;
    }

    private static double RoundPixels(double pixels) => Math.Round(pixels, MidpointRounding.AwayFromZero);
}

/// <summary>Font size from "lines visible" and the text area. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TypographyMath
{
    private const double FallbackLinesVisible = 8.0;
    private const double FallbackLineSpacing = 1.3;

    /// <summary>size = textAreaHeight / (linesVisible * lineSpacing). (1080-64-64)/(8*1.3) = 91.54.
    /// Non-finite or non-positive inputs fall back to safe values; the result is always finite and positive.</summary>
    public static double BaseFontSize(double textAreaHeightDip, double linesVisible, double lineSpacing)
    {
        var height = IsPositive(textAreaHeightDip) ? textAreaHeightDip : 1.0;
        var lines = IsPositive(linesVisible) ? linesVisible : FallbackLinesVisible;
        var spacing = IsPositive(lineSpacing) ? lineSpacing : FallbackLineSpacing;
        return height / (lines * spacing);
    }

    /// <summary>Compute the text area for an output display. Margins are fractions of the display; DIPs = px / DpiScale.</summary>
    public static TextArea ComputeTextArea(OutputGeometry output, MarginSettings margins, TextSettings text, OutputSettings outputSettings)
    {
        var dpi = IsPositive(output.DpiScale) ? output.DpiScale : 1.0;
        var size = new PixelSize(Math.Max(1, output.WidthPx), Math.Max(1, output.HeightPx));
        var px = MarginMath.ToPixels(margins, size);
        var left = Finite(px.Left) / dpi;
        var top = Finite(px.Top) / dpi;
        var width = Math.Max(1, (size.Width / dpi) - left - (Finite(px.Right) / dpi));
        var height = Math.Max(1, (size.Height / dpi) - top - (Finite(px.Bottom) / dpi));
        var fontSize = BaseFontSize(height, text.LinesVisible, text.LineSpacing);
        var spacing = IsPositive(text.LineSpacing) ? text.LineSpacing : FallbackLineSpacing;
        var fraction = double.IsFinite(outputSettings.ReadingLineFraction)
            ? Math.Clamp(outputSettings.ReadingLineFraction, 0.10, 0.60)
            : 0.33;
        return new TextArea(left, top, width, height, height * fraction, fontSize, fontSize * spacing);
    }

    private static bool IsPositive(double v) => double.IsFinite(v) && v > 0;

    private static double Finite(double v) => double.IsFinite(v) ? v : 0.0;
}

/// <summary>Speed units to lines per minute and DIPs per second. Signatures frozen; stream A owns bodies and tests.</summary>
public static class SpeedCurve
{
    private const double DefaultMaxSpeed = 10.0;

    public static double ToLinesPerMinute(double speed, SpeedCurveSettings curve, double maxSpeed = 10.0)
    {
        if (double.IsNaN(speed) || speed == 0.0)
        {
            return 0.0;
        }

        var max = Max(maxSpeed);
        var magnitude = Math.Min(Math.Abs(speed), max) / max;
        var lpm = Math.Max(0.0, curve.LinesPerMinuteAtMax) * Math.Pow(magnitude, Exponent(curve));
        return speed < 0 ? -lpm : lpm;
    }

    public static double FromLinesPerMinute(double linesPerMinute, SpeedCurveSettings curve, double maxSpeed = 10.0)
    {
        if (double.IsNaN(linesPerMinute) || linesPerMinute == 0.0)
        {
            return 0.0;
        }

        var atMax = curve.LinesPerMinuteAtMax;
        if (!(atMax > 0) || double.IsInfinity(atMax))
        {
            return 0.0;
        }

        var max = Max(maxSpeed);
        var ratio = Math.Min(Math.Abs(linesPerMinute) / atMax, 1.0);
        var speed = max * Math.Pow(ratio, 1.0 / Exponent(curve));
        return linesPerMinute < 0 ? -speed : speed;
    }

    public static double ToDipsPerSecond(double speed, double baseLineHeightDip, SpeedCurveSettings curve, double maxSpeed = 10.0)
    {
        if (!double.IsFinite(baseLineHeightDip) || baseLineHeightDip <= 0)
        {
            return 0.0;
        }

        return ToLinesPerMinute(speed, curve, maxSpeed) / 60.0 * baseLineHeightDip;
    }

    private static double Max(double maxSpeed) => double.IsFinite(maxSpeed) && maxSpeed > 0 ? maxSpeed : DefaultMaxSpeed;

    private static double Exponent(SpeedCurveSettings curve) =>
        double.IsFinite(curve.Exponent) && curve.Exponent > 0 ? curve.Exponent : 1.0;
}

/// <summary>Analog axis to speed units. Signature frozen; stream A owns body and tests.</summary>
public static class AxisCurve
{
    /// <summary>Returns speed units for a normalised axis value per the binding's role, deadzone, exponent, max and invert.
    /// Triggers (0..1) with SpeedReverse return negative speed.</summary>
    public static double Apply(double value, AxisBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Role == AxisRole.None || double.IsNaN(value) || !double.IsFinite(binding.MaxSpeed))
        {
            return 0.0;
        }

        var magnitude = Math.Min(Math.Abs(value), 1.0);
        double sign;
        switch (binding.Role)
        {
            case AxisRole.Speed:
                sign = value < 0 ? -1.0 : 1.0;
                break;
            case AxisRole.SpeedForward:
                if (value < 0) { return 0.0; }
                sign = 1.0;
                break;
            case AxisRole.SpeedReverse:
                if (value < 0) { return 0.0; }
                sign = -1.0;
                break;
            default:
                return 0.0;
        }

        var deadzone = double.IsFinite(binding.Deadzone) ? Math.Clamp(binding.Deadzone, 0.0, 0.99) : 0.0;
        if (magnitude <= deadzone)
        {
            return 0.0;
        }

        var scaled = (magnitude - deadzone) / (1.0 - deadzone);
        var exponent = double.IsFinite(binding.Exponent) && binding.Exponent > 0 ? binding.Exponent : 1.0;
        var speed = sign * binding.MaxSpeed * Math.Pow(scaled, exponent);
        if (binding.Invert)
        {
            speed = -speed;
        }

        return speed == 0.0 ? 0.0 : speed;
    }
}

/// <summary>Durations and reading pace. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TimeEstimator
{
    public static double SecondsForLines(int lines, double speed, SpeedCurveSettings curve)
    {
        if (lines <= 0)
        {
            return 0.0;
        }

        var lpm = Math.Abs(SpeedCurve.ToLinesPerMinute(speed, curve));
        return lpm < 1e-6 ? double.PositiveInfinity : lines * 60.0 / lpm;
    }

    /// <summary>Seconds to scroll <paramref name="baseLines"/> base line heights of distance (fractional) at the given
    /// speed: what Display scrolling takes, since it moves lpm x BaseLineHeight per minute whatever is on the lines.</summary>
    public static double SecondsForBaseLines(double baseLines, double speed, SpeedCurveSettings curve)
    {
        if (!(baseLines > 1e-9))
        {
            return 0.0;
        }

        var lpm = Math.Abs(SpeedCurve.ToLinesPerMinute(speed, curve));
        return lpm < 1e-6 ? double.PositiveInfinity : baseLines * 60.0 / lpm;
    }

    /// <summary>Estimated Display-scrolling duration of each section at the given speed: the distance from the section's
    /// start position (its first line on the reading line) to the next section's, or to the END position, in base
    /// lines. Paragraph gaps, empty lines and hidden markers take scrolling time too, so this is distance, not a count of
    /// text lines (integration fix). A speed of zero gives <see cref="TimeSpan.MaxValue"/> for every section with
    /// content (formatted as "--").</summary>
    public static IReadOnlyList<TimeSpan> SectionDurations(LayoutGeometry geometry, double speed, SpeedCurveSettings curve)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var sections = geometry.Sections;
        var result = new TimeSpan[sections.Count];
        var lineHeight = geometry.BaseLineHeight;
        var endCentre = geometry.EndTop + (lineHeight / 2.0);
        for (var i = 0; i < sections.Count; i++)
        {
            var from = StartCentre(geometry, sections[i].Top, endCentre);
            var to = i + 1 < sections.Count ? StartCentre(geometry, sections[i + 1].Top, endCentre) : endCentre;
            var baseLines = lineHeight > 0 && double.IsFinite(to - from) ? Math.Max(0.0, to - from) / lineHeight : 0.0;
            result[i] = ToTimeSpan(SecondsForBaseLines(baseLines, speed, curve));
        }

        return result;
    }

    /// <summary>Centre of the first line at or below <paramref name="top"/> (where a jump there puts the reading line), or
    /// the END centre if there is none.</summary>
    private static double StartCentre(LayoutGeometry geometry, double top, double endCentre)
    {
        var first = LowerBound(geometry.LineTops, top);
        return first < geometry.LineCount ? Math.Min(geometry.LineCentre(first), endCentre) : endCentre;
    }

    /// <summary>Reading pace in Display scrolling: words per base line of script height (first line to END), times lpm.
    /// Falls back to words per laid-out line when the geometry has no usable heights.</summary>
    public static double WordsPerMinute(LayoutGeometry geometry, double linesPerMinute)
    {
        if (geometry.LineCount == 0 || !double.IsFinite(linesPerMinute))
        {
            return 0;
        }

        var baseLines = geometry.BaseLineHeight > 0 ? (geometry.EndTop - geometry.LineTops[0]) / geometry.BaseLineHeight : 0.0;
        var units = double.IsFinite(baseLines) && baseLines >= 1.0 ? baseLines : geometry.LineCount;
        return Math.Abs(linesPerMinute) * geometry.WordCount / units;
    }

    /// <summary>House format: "6m:42s" under an hour, "1h:05m" from an hour; "--" for infinity/NaN
    /// (<see cref="TimeSpan.MaxValue"/> and <see cref="TimeSpan.MinValue"/> stand for infinity).</summary>
    public static string Format(TimeSpan duration)
    {
        if (duration == TimeSpan.MaxValue || duration == TimeSpan.MinValue)
        {
            return "--";
        }

        return Format(duration.TotalSeconds);
    }

    /// <summary>Same as <see cref="Format(TimeSpan)"/> but takes seconds, so infinity and NaN can be passed directly.</summary>
    public static string Format(double seconds)
    {
        if (!double.IsFinite(seconds))
        {
            return "--";
        }

        var total = (long)Math.Round(Math.Max(0.0, seconds), MidpointRounding.AwayFromZero);
        var hours = total / 3600;
        var minutes = (total % 3600) / 60;
        if (hours > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{hours}h:{minutes:00}m");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{minutes}m:{total % 60:00}s");
    }

    private static TimeSpan ToTimeSpan(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds >= TimeSpan.MaxValue.TotalSeconds)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromSeconds(Math.Max(0.0, seconds));
    }

    /// <summary>Index of the first element that is at or above <paramref name="value"/> (Count if none).</summary>
    private static int LowerBound(IReadOnlyList<double> sorted, double value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid] < value) { lo = mid + 1; } else { hi = mid; }
        }

        return lo;
    }
}

/// <summary>DESIGN.md Motion tokens (seconds). Ramps are linear in velocity; steps are cubic ease-out in position.</summary>
public static class MotionTimings
{
    public const double RampStart = 0.400;
    public const double RampStop = 0.200;
    public const double LineStep = 0.220;
    public const double ParagraphStep = 0.320;
    public const double FailSafeStop = 0.300;

    public static double RampChange(Acceleration a) => a switch
    {
        Acceleration.Gentle => 0.600,
        Acceleration.Quick => 0.150,
        _ => 0.300,
    };
}
