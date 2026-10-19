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

    public static MarginPixels ToPixels(MarginSettings m, PixelSize output) =>
        new(m.Left * output.Width, m.Top * output.Height, m.Right * output.Width, m.Bottom * output.Height);

    /// <summary>Set one edge in output pixels. Clamps to 0..MaxFraction of its dimension. When m.IsLinked, all four
    /// edges get the same pixel value (each clamped in its own dimension). Records LastEdited.</summary>
    public static MarginSettings SetEdgePixels(MarginSettings m, MarginEdge edge, double pixels, PixelSize output) =>
        edge switch // STUB (stream A replaces): no clamping, ignores IsLinked
        {
            MarginEdge.Left => m with { Left = pixels / output.Width, LastEdited = edge },
            MarginEdge.Right => m with { Right = pixels / output.Width, LastEdited = edge },
            MarginEdge.Top => m with { Top = pixels / output.Height, LastEdited = edge },
            _ => m with { Bottom = pixels / output.Height, LastEdited = edge },
        };

    /// <summary>Turn the link on or off. Turning it on copies LastEdited's pixel value to all four edges.</summary>
    public static MarginSettings SetLinked(MarginSettings m, bool linked, PixelSize output) =>
        m with { IsLinked = linked }; // STUB (stream A replaces): does not equalise edges
}

/// <summary>Font size from "lines visible" and the text area. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TypographyMath
{
    /// <summary>size = textAreaHeight / (linesVisible * lineSpacing). (1080-64-64)/(8*1.3) = 91.54.</summary>
    public static double BaseFontSize(double textAreaHeightDip, double linesVisible, double lineSpacing) =>
        textAreaHeightDip / (linesVisible * lineSpacing);

    /// <summary>Compute the text area for an output display. Margins are fractions of the display; DIPs = px / DpiScale.</summary>
    public static TextArea ComputeTextArea(OutputGeometry output, MarginSettings margins, TextSettings text, OutputSettings outputSettings)
    {
        var px = MarginMath.ToPixels(margins, output.SizePx);
        var left = px.Left / output.DpiScale;
        var top = px.Top / output.DpiScale;
        var width = Math.Max(1, output.WidthDip - left - (px.Right / output.DpiScale));
        var height = Math.Max(1, output.HeightDip - top - (px.Bottom / output.DpiScale));
        var size = BaseFontSize(height, text.LinesVisible, text.LineSpacing);
        return new TextArea(left, top, width, height, height * outputSettings.ReadingLineFraction, size, size * text.LineSpacing);
    }
}

/// <summary>Speed units to lines per minute and DIPs per second. Signatures frozen; stream A owns bodies and tests.</summary>
public static class SpeedCurve
{
    public static double ToLinesPerMinute(double speed, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        Math.Sign(speed) * curve.LinesPerMinuteAtMax * Math.Pow(Math.Min(Math.Abs(speed), maxSpeed) / maxSpeed, curve.Exponent);

    public static double FromLinesPerMinute(double linesPerMinute, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        Math.Sign(linesPerMinute) * maxSpeed * Math.Pow(Math.Min(Math.Abs(linesPerMinute) / curve.LinesPerMinuteAtMax, 1.0), 1.0 / curve.Exponent);

    public static double ToDipsPerSecond(double speed, double baseLineHeightDip, SpeedCurveSettings curve, double maxSpeed = 10.0) =>
        ToLinesPerMinute(speed, curve, maxSpeed) / 60.0 * baseLineHeightDip;
}

/// <summary>Analog axis to speed units. Signature frozen; stream A owns body and tests.</summary>
public static class AxisCurve
{
    /// <summary>Returns speed units for a normalised axis value per the binding's role, deadzone, exponent, max and invert.
    /// Triggers (0..1) with SpeedReverse return negative speed.</summary>
    public static double Apply(double value, AxisBinding binding) =>
        binding.Role switch // STUB (stream A replaces): linear, no deadzone/exponent/invert
        {
            AxisRole.Speed or AxisRole.SpeedForward => value * binding.MaxSpeed,
            AxisRole.SpeedReverse => -value * binding.MaxSpeed,
            _ => 0.0,
        };
}

/// <summary>Durations and reading pace. Signatures frozen; stream A owns bodies and tests.</summary>
public static class TimeEstimator
{
    public static double SecondsForLines(int lines, double speed, SpeedCurveSettings curve)
    {
        var lpm = Math.Abs(SpeedCurve.ToLinesPerMinute(speed, curve));
        return lpm < 1e-6 ? double.PositiveInfinity : lines * 60.0 / lpm;
    }

    /// <summary>Estimated duration of each section (lines from its marker to the next marker or END) at the given speed.</summary>
    public static IReadOnlyList<TimeSpan> SectionDurations(LayoutGeometry geometry, double speed, SpeedCurveSettings curve) =>
        geometry.Sections.Select(_ => TimeSpan.Zero).ToArray(); // STUB (stream A replaces)

    public static double WordsPerMinute(LayoutGeometry geometry, double linesPerMinute) =>
        geometry.LineCount == 0 ? 0 : Math.Abs(linesPerMinute) * geometry.WordCount / geometry.LineCount;

    /// <summary>House format: "6m:42s" under an hour, "1h:05m" from an hour; "--" for infinity/NaN.</summary>
    public static string Format(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes}m:{duration.Seconds:00}s"; // STUB (stream A replaces): no hours, no infinity
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
