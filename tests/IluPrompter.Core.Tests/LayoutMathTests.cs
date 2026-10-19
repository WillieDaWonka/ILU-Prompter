namespace IluPrompter.Core.Tests;

public class SpeedCurveTests
{
    private static readonly SpeedCurveSettings Curve = new();

    [Fact]
    public void DefaultSpeed_IsAbout28LinesPerMinute()
    {
        Assert.Equal(28.3, SpeedCurve.ToLinesPerMinute(4.5, Curve), 0.3);
    }

    [Fact]
    public void DefaultSpeed_IsAbout140WordsPerMinute()
    {
        var geometry = TestGeometry.Create(lines: 100, wordCount: 500); // 5 words per line
        var lpm = SpeedCurve.ToLinesPerMinute(4.5, Curve);
        Assert.Equal(141.5, TimeEstimator.WordsPerMinute(geometry, lpm), 1.5);
    }

    [Fact]
    public void FullScale_IsLinesPerMinuteAtMax()
    {
        Assert.Equal(80.0, SpeedCurve.ToLinesPerMinute(10, Curve), 9);
        Assert.Equal(-80.0, SpeedCurve.ToLinesPerMinute(-10, Curve), 9);
    }

    [Fact]
    public void IsMonotonic()
    {
        var previous = double.NegativeInfinity;
        for (var s = -10.0; s <= 10.0; s += 0.1)
        {
            var lpm = SpeedCurve.ToLinesPerMinute(s, Curve);
            Assert.True(lpm >= previous, $"not monotonic at {s}");
            previous = lpm;
        }
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(4.5)]
    [InlineData(9.9)]
    public void IsOddSymmetric(double s)
    {
        Assert.Equal(-SpeedCurve.ToLinesPerMinute(s, Curve), SpeedCurve.ToLinesPerMinute(-s, Curve), 12);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-7.3)]
    [InlineData(-0.5)]
    [InlineData(0.1)]
    [InlineData(4.5)]
    [InlineData(10)]
    public void RoundTrips(double s)
    {
        var back = SpeedCurve.FromLinesPerMinute(SpeedCurve.ToLinesPerMinute(s, Curve), Curve);
        Assert.Equal(s, back, 9);
    }

    [Fact]
    public void Zero_IsZero()
    {
        Assert.Equal(0.0, SpeedCurve.ToLinesPerMinute(0, Curve));
        Assert.Equal(0.0, SpeedCurve.FromLinesPerMinute(0, Curve));
        Assert.Equal(0.0, SpeedCurve.ToDipsPerSecond(0, 100, Curve));
    }

    [Fact]
    public void ClampsBeyondScale()
    {
        Assert.Equal(SpeedCurve.ToLinesPerMinute(10, Curve), SpeedCurve.ToLinesPerMinute(25, Curve));
        Assert.Equal(SpeedCurve.ToLinesPerMinute(-10, Curve), SpeedCurve.ToLinesPerMinute(-25, Curve));
        Assert.Equal(10.0, SpeedCurve.FromLinesPerMinute(500, Curve), 9);
    }

    [Fact]
    public void DipsPerSecond_UsesBaseLineHeight()
    {
        var lpm = SpeedCurve.ToLinesPerMinute(4.5, Curve);
        Assert.Equal(lpm / 60.0 * 119.6, SpeedCurve.ToDipsPerSecond(4.5, 119.6, Curve), 9);
    }

    [Fact]
    public void GuardsAgainstNaNAndBadInput()
    {
        Assert.Equal(0.0, SpeedCurve.ToLinesPerMinute(double.NaN, Curve));
        Assert.Equal(0.0, SpeedCurve.FromLinesPerMinute(double.NaN, Curve));
        Assert.Equal(0.0, SpeedCurve.ToDipsPerSecond(5, double.NaN, Curve));
        Assert.Equal(0.0, SpeedCurve.ToDipsPerSecond(5, -3, Curve));
        Assert.True(double.IsFinite(SpeedCurve.ToLinesPerMinute(5, new SpeedCurveSettings { Exponent = 0 })));
        Assert.True(double.IsFinite(SpeedCurve.ToLinesPerMinute(5, Curve, maxSpeed: 0)));
        Assert.Equal(0.0, SpeedCurve.FromLinesPerMinute(10, new SpeedCurveSettings { LinesPerMinuteAtMax = 0 }));
    }

    [Fact]
    public void CustomMaxSpeed_RescalesTheScale()
    {
        Assert.Equal(80.0, SpeedCurve.ToLinesPerMinute(8, Curve, maxSpeed: 8), 9);
    }
}

public class AxisCurveTests
{
    [Fact]
    public void InsideDeadzone_IsZero()
    {
        var b = new AxisBinding(AxisRole.Speed, Deadzone: 0.2, Exponent: 2, MaxSpeed: 10);
        Assert.Equal(0.0, AxisCurve.Apply(0.0, b));
        Assert.Equal(0.0, AxisCurve.Apply(0.19, b));
        Assert.Equal(0.0, AxisCurve.Apply(-0.2, b));
        Assert.True(AxisCurve.Apply(0.21, b) > 0);
    }

    [Fact]
    public void ExponentShape_AtHalfDeflection()
    {
        var b = new AxisBinding(AxisRole.Speed, Deadzone: 0.1, Exponent: 2, MaxSpeed: 10);
        var expected = 10 * Math.Pow((0.5 - 0.1) / 0.9, 2);
        Assert.Equal(expected, AxisCurve.Apply(0.5, b), 9);
        Assert.Equal(-expected, AxisCurve.Apply(-0.5, b), 9);

        var linear = new AxisBinding(AxisRole.Speed, Deadzone: 0.0, Exponent: 1, MaxSpeed: 10);
        Assert.Equal(5.0, AxisCurve.Apply(0.5, linear), 9);
    }

    [Fact]
    public void MaxAtFullDeflection()
    {
        var b = new AxisBinding(AxisRole.Speed, Deadzone: 0.24, Exponent: 2.5, MaxSpeed: 7);
        Assert.Equal(7.0, AxisCurve.Apply(1.0, b), 9);
        Assert.Equal(-7.0, AxisCurve.Apply(-1.0, b), 9);
        Assert.Equal(7.0, AxisCurve.Apply(1.7, b), 9); // clamped
    }

    [Fact]
    public void Invert_FlipsSign()
    {
        var b = new AxisBinding(AxisRole.Speed, Deadzone: 0.1, Exponent: 1.5, MaxSpeed: 10, Invert: true);
        var plain = b with { Invert = false };
        Assert.Equal(-AxisCurve.Apply(0.6, plain), AxisCurve.Apply(0.6, b), 12);
        Assert.Equal(-AxisCurve.Apply(-0.3, plain), AxisCurve.Apply(-0.3, b), 12);
    }

    [Fact]
    public void Triggers_ForwardIsPositive_ReverseIsNegative()
    {
        var forward = new AxisBinding(AxisRole.SpeedForward, Deadzone: 0.12, Exponent: 2, MaxSpeed: 10);
        var reverse = new AxisBinding(AxisRole.SpeedReverse, Deadzone: 0.12, Exponent: 2, MaxSpeed: 10);
        Assert.Equal(10.0, AxisCurve.Apply(1.0, forward), 9);
        Assert.Equal(-10.0, AxisCurve.Apply(1.0, reverse), 9);
        Assert.Equal(0.0, AxisCurve.Apply(0.1, forward));
        Assert.Equal(0.0, AxisCurve.Apply(0.1, reverse));
        Assert.True(AxisCurve.Apply(0.5, forward) > 0);
        Assert.True(AxisCurve.Apply(0.5, reverse) < 0);
        Assert.Equal(0.0, AxisCurve.Apply(-0.5, forward)); // triggers never go negative
    }

    [Fact]
    public void TriggerInvert_FlipsDirection()
    {
        var forward = new AxisBinding(AxisRole.SpeedForward, Deadzone: 0, Exponent: 1, MaxSpeed: 10, Invert: true);
        Assert.Equal(-10.0, AxisCurve.Apply(1.0, forward), 9);
    }

    [Fact]
    public void RoleNone_AndBadInput_AreZero()
    {
        Assert.Equal(0.0, AxisCurve.Apply(1.0, new AxisBinding(AxisRole.None)));
        Assert.Equal(0.0, AxisCurve.Apply(double.NaN, new AxisBinding(AxisRole.Speed)));
        Assert.Equal(0.0, AxisCurve.Apply(0.5, new AxisBinding(AxisRole.Speed, MaxSpeed: double.NaN)));
        Assert.True(double.IsFinite(AxisCurve.Apply(0.5, new AxisBinding(AxisRole.Speed, Deadzone: 1.5, Exponent: -2))));
    }

    [Fact]
    public void ShuttleRing_DefaultBinding_UsesEveryDetent()
    {
        var b = DefaultBindings.ShuttleAxes[ControlIds.ShuttleRing];
        var previous = 0.0;
        for (var k = 1; k <= 7; k++)
        {
            var v = AxisCurve.Apply(k / 7.0, b);
            Assert.True(v > previous, $"detent {k} must be faster than {k - 1}");
            previous = v;
        }

        Assert.Equal(10.0, previous, 9);
    }

    [Fact]
    public void NeverReturnsNegativeZero()
    {
        var b = new AxisBinding(AxisRole.Speed, Deadzone: 0.5);
        Assert.False(double.IsNegative(AxisCurve.Apply(-0.1, b)));
    }
}

public class MarginMathTests
{
    private static readonly PixelSize Hd = new(1920, 1080);

    [Fact]
    public void Defaults_AreDesignPixelValues()
    {
        var px = MarginMath.ToPixels(new MarginSettings(), Hd);
        Assert.Equal(160, px.Left, 6);
        Assert.Equal(64, px.Top, 6);
        Assert.Equal(160, px.Right, 6);
        Assert.Equal(64, px.Bottom, 6);
    }

    [Fact]
    public void SetEdge_RoundsToWholePixelsAndRecordsLastEdited()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), MarginEdge.Top, 100.4, Hd);
        Assert.Equal(100, MarginMath.ToPixels(m, Hd).Top, 6);
        Assert.Equal(MarginEdge.Top, m.LastEdited);
        Assert.Equal(160, MarginMath.ToPixels(m, Hd).Left, 6); // others untouched when not linked
    }

    [Theory]
    [InlineData(MarginEdge.Left, 768)]
    [InlineData(MarginEdge.Right, 768)]
    [InlineData(MarginEdge.Top, 432)]
    [InlineData(MarginEdge.Bottom, 432)]
    public void SetEdge_ClampsTo40Percent(MarginEdge edge, double max)
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), edge, 5000, Hd);
        var px = MarginMath.ToPixels(m, Hd);
        var value = edge switch { MarginEdge.Left => px.Left, MarginEdge.Right => px.Right, MarginEdge.Top => px.Top, _ => px.Bottom };
        Assert.Equal(max, value, 6);
    }

    [Fact]
    public void SetEdge_ClampsNegativeToZero_AndIgnoresNaN()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), MarginEdge.Left, -30, Hd);
        Assert.Equal(0.0, m.Left);
        var same = MarginMath.SetEdgePixels(m, MarginEdge.Left, double.NaN, Hd);
        Assert.Equal(m, same);
    }

    [Fact]
    public void TextArea_KeepsAtLeast20Percent()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), MarginEdge.Left, 5000, Hd);
        m = MarginMath.SetEdgePixels(m, MarginEdge.Right, 5000, Hd);
        m = MarginMath.SetEdgePixels(m, MarginEdge.Top, 5000, Hd);
        m = MarginMath.SetEdgePixels(m, MarginEdge.Bottom, 5000, Hd);
        var px = MarginMath.ToPixels(m, Hd);
        Assert.True(Hd.Width - px.Left - px.Right >= 0.2 * Hd.Width - 1e-6);
        Assert.True(Hd.Height - px.Top - px.Bottom >= 0.2 * Hd.Height - 1e-6);
    }

    [Fact]
    public void TextArea_Minimum_HoldsEvenWhenTheOppositeEdgeCameFromAnOlderFile()
    {
        var odd = new MarginSettings { Right = 0.45 }; // out of range on purpose, as an old file might have it
        var grown = MarginMath.SetEdgePixels(odd, MarginEdge.Left, 768, Hd);
        var px = MarginMath.ToPixels(grown, Hd);
        Assert.True(Hd.Width - px.Left - px.Right >= 0.2 * Hd.Width - 1e-6);
        Assert.Equal(672, px.Left, 6);
    }

    [Fact]
    public void Linked_EditSetsAllFourToTheSamePixelValue()
    {
        var linked = new MarginSettings { IsLinked = true };
        var m = MarginMath.SetEdgePixels(linked, MarginEdge.Top, 80, Hd);
        var px = MarginMath.ToPixels(m, Hd);
        Assert.Equal(80, px.Left, 6);
        Assert.Equal(80, px.Top, 6);
        Assert.Equal(80, px.Right, 6);
        Assert.Equal(80, px.Bottom, 6);
        Assert.Equal(MarginEdge.Top, m.LastEdited);
    }

    [Fact]
    public void Linked_EachEdgeIsClampedInItsOwnDimension()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings { IsLinked = true }, MarginEdge.Left, 700, Hd);
        var px = MarginMath.ToPixels(m, Hd);
        Assert.Equal(700, px.Left, 6);
        Assert.Equal(700, px.Right, 6);
        Assert.Equal(432, px.Top, 6);    // 40 % of 1080
        Assert.Equal(432, px.Bottom, 6);
    }

    [Fact]
    public void LinkingOn_CopiesLastEditedEdgeToAllFour()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), MarginEdge.Bottom, 90, Hd);
        var linked = MarginMath.SetLinked(m, true, Hd);
        var px = MarginMath.ToPixels(linked, Hd);
        Assert.True(linked.IsLinked);
        Assert.Equal(90, px.Left, 6);
        Assert.Equal(90, px.Top, 6);
        Assert.Equal(90, px.Right, 6);
        Assert.Equal(90, px.Bottom, 6);
    }

    [Fact]
    public void LinkingOff_KeepsTheValues()
    {
        var linked = MarginMath.SetLinked(new MarginSettings(), true, Hd);
        var off = MarginMath.SetLinked(linked, false, Hd);
        Assert.False(off.IsLinked);
        Assert.Equal(linked.Left, off.Left);
        Assert.Equal(linked.Top, off.Top);
    }

    [Fact]
    public void Fractions_SurviveAResolutionChange()
    {
        var m = MarginMath.SetEdgePixels(new MarginSettings(), MarginEdge.Left, 200, Hd);
        var at4k = MarginMath.ToPixels(m, new PixelSize(3840, 2160));
        Assert.Equal(400, at4k.Left, 6);
        Assert.Equal(128, at4k.Top, 6); // 64 px on 1080 is 128 px on 2160: the same percentage
    }

    [Fact]
    public void ZeroSizedOutput_ReturnsInput()
    {
        var m = new MarginSettings();
        Assert.Equal(m, MarginMath.SetEdgePixels(m, MarginEdge.Left, 10, new PixelSize(0, 0)));
        Assert.True(MarginMath.SetLinked(m, true, new PixelSize(0, 0)).IsLinked);
    }
}

public class TypographyMathTests
{
    [Fact]
    public void BaseFontSize_MatchesTheDesignExample()
    {
        Assert.Equal(91.54, TypographyMath.BaseFontSize(1080 - 64 - 64, 8, 1.3), 2);
    }

    [Fact]
    public void ComputeTextArea_At1080p()
    {
        var area = TypographyMath.ComputeTextArea(OutputGeometry.Default1080, new MarginSettings(), new TextSettings(), new OutputSettings());
        Assert.Equal(160, area.Left, 6);
        Assert.Equal(64, area.Top, 6);
        Assert.Equal(1600, area.Width, 6);
        Assert.Equal(952, area.Height, 6);
        Assert.Equal(952 * 0.33, area.ReadingLineY, 6);
        Assert.Equal(91.54, area.BaseFontSize, 2);
        Assert.Equal(area.BaseFontSize * 1.3, area.BaseLineHeight, 9);
        Assert.Equal(new ViewportMetrics(1600, 952, 952 * 0.33), area.ToViewport());
    }

    [Fact]
    public void Dpi_DoesNotChangeTheDipLayout()
    {
        // 150 % scaling: the panel is 2880 x 1620 px but the same 1920 x 1080 DIPs.
        var scaled = new OutputGeometry(2880, 1620, 1.5, 60);
        var area = TypographyMath.ComputeTextArea(scaled, new MarginSettings(), new TextSettings(), new OutputSettings());
        var baseline = TypographyMath.ComputeTextArea(OutputGeometry.Default1080, new MarginSettings(), new TextSettings(), new OutputSettings());
        Assert.Equal(baseline.Width, area.Width, 6);
        Assert.Equal(baseline.Height, area.Height, 6);
        Assert.Equal(baseline.BaseFontSize, area.BaseFontSize, 6);
        Assert.Equal(baseline.Left, area.Left, 6);
    }

    [Fact]
    public void FourK_AtTwoHundredPercent_IsTheSameLayoutAs1080p()
    {
        var area = TypographyMath.ComputeTextArea(new OutputGeometry(3840, 2160, 2.0, 60), new MarginSettings(), new TextSettings(), new OutputSettings());
        Assert.Equal(91.54, area.BaseFontSize, 2);
    }

    [Fact]
    public void MoreLinesVisible_ShrinksTheFont()
    {
        var eight = TypographyMath.BaseFontSize(952, 8, 1.3);
        var twelve = TypographyMath.BaseFontSize(952, 12, 1.3);
        Assert.True(twelve < eight);
        Assert.Equal(952.0 / (12 * 1.3), twelve, 9);
    }

    [Theory]
    [InlineData(0.0, 8.0, 1.3)]
    [InlineData(952.0, 0.0, 1.3)]
    [InlineData(952.0, 8.0, 0.0)]
    [InlineData(-5.0, 8.0, 1.3)]
    [InlineData(double.NaN, 8.0, 1.3)]
    [InlineData(952.0, double.NaN, 1.3)]
    [InlineData(952.0, 8.0, double.PositiveInfinity)]
    public void BaseFontSize_IsAlwaysFiniteAndPositive(double height, double lines, double spacing)
    {
        var size = TypographyMath.BaseFontSize(height, lines, spacing);
        Assert.True(double.IsFinite(size) && size > 0);
    }

    [Fact]
    public void ComputeTextArea_GuardsDegenerateInput()
    {
        var area = TypographyMath.ComputeTextArea(
            new OutputGeometry(0, 0, 0, 0),
            new MarginSettings { Left = double.NaN },
            new TextSettings { LinesVisible = 0, LineSpacing = double.NaN },
            new OutputSettings { ReadingLineFraction = 9 });
        Assert.True(double.IsFinite(area.Width) && area.Width >= 1);
        Assert.True(double.IsFinite(area.Height) && area.Height >= 1);
        Assert.True(double.IsFinite(area.BaseFontSize) && area.BaseFontSize > 0);
        Assert.True(area.ReadingLineY <= area.Height * 0.6 + 1e-9);
    }
}

public class TimeEstimatorTests
{
    private static readonly SpeedCurveSettings Curve = new();

    [Theory]
    [InlineData(402, "6m:42s")]
    [InlineData(0, "0m:00s")]
    [InlineData(59.4, "0m:59s")]
    [InlineData(59.6, "1m:00s")]
    [InlineData(3599, "59m:59s")]
    [InlineData(3600, "1h:00m")]
    [InlineData(3900, "1h:05m")]
    [InlineData(37230, "10h:20m")]
    public void Format_UsesTheHouseFormat(double seconds, string expected)
    {
        Assert.Equal(expected, TimeEstimator.Format(TimeSpan.FromSeconds(seconds)));
        Assert.Equal(expected, TimeEstimator.Format(seconds));
    }

    [Fact]
    public void Format_InfinityAndNaN_AreDashes()
    {
        Assert.Equal("--", TimeEstimator.Format(double.PositiveInfinity));
        Assert.Equal("--", TimeEstimator.Format(double.NegativeInfinity));
        Assert.Equal("--", TimeEstimator.Format(double.NaN));
        Assert.Equal("--", TimeEstimator.Format(TimeSpan.MaxValue));
        Assert.Equal("--", TimeEstimator.Format(TimeSpan.MinValue));
    }

    [Fact]
    public void Format_NegativeIsZero() => Assert.Equal("0m:00s", TimeEstimator.Format(TimeSpan.FromSeconds(-12)));

    [Fact]
    public void SecondsForLines_FollowsTheCurve()
    {
        var lpm = SpeedCurve.ToLinesPerMinute(4.5, Curve);
        Assert.Equal(100 * 60.0 / lpm, TimeEstimator.SecondsForLines(100, 4.5, Curve), 9);
        Assert.Equal(TimeEstimator.SecondsForLines(100, 4.5, Curve), TimeEstimator.SecondsForLines(100, -4.5, Curve), 9);
        Assert.True(double.IsPositiveInfinity(TimeEstimator.SecondsForLines(10, 0, Curve)));
        Assert.Equal(0.0, TimeEstimator.SecondsForLines(0, 0, Curve));
        Assert.Equal(0.0, TimeEstimator.SecondsForLines(-3, 4.5, Curve));
    }

    [Fact]
    public void SectionDurations_SumToTheTotalFromTheFirstMarker()
    {
        var g = TestGeometry.Create(lines: 100, linesPerParagraph: 5, sectionParagraphs: [2, 6, 10]);
        var durations = TimeEstimator.SectionDurations(g, 4.5, Curve);
        Assert.Equal(3, durations.Count);

        // Section 1 = lines 10..29, section 2 = 30..49, section 3 = 50..99.
        Assert.Equal(TimeEstimator.SecondsForLines(20, 4.5, Curve), durations[0].TotalSeconds, 3);
        Assert.Equal(TimeEstimator.SecondsForLines(20, 4.5, Curve), durations[1].TotalSeconds, 3);
        Assert.Equal(TimeEstimator.SecondsForLines(50, 4.5, Curve), durations[2].TotalSeconds, 3);

        var sum = durations.Sum(d => d.TotalSeconds);
        Assert.Equal(TimeEstimator.SecondsForLines(90, 4.5, Curve), sum, 3);
    }

    [Fact]
    public void SectionDurations_AtSpeedZero_AreInfinite_AndNoSectionsIsEmpty()
    {
        var g = TestGeometry.Create(lines: 40, sectionParagraphs: [0, 4]);
        var durations = TimeEstimator.SectionDurations(g, 0, Curve);
        Assert.All(durations, d => Assert.Equal(TimeSpan.MaxValue, d));
        Assert.All(durations, d => Assert.Equal("--", TimeEstimator.Format(d)));
        Assert.Empty(TimeEstimator.SectionDurations(TestGeometry.Create(lines: 40), 4.5, Curve));
    }

    [Fact]
    public void WordsPerMinute_ScalesWithWordsPerLine()
    {
        var g = TestGeometry.Create(lines: 50, wordCount: 400); // 8 words per line
        Assert.Equal(8 * 28.0, TimeEstimator.WordsPerMinute(g, 28.0), 6);
        Assert.Equal(8 * 28.0, TimeEstimator.WordsPerMinute(g, -28.0), 6);
        Assert.Equal(0.0, TimeEstimator.WordsPerMinute(LayoutGeometry.Empty(), 28.0));
        Assert.Equal(0.0, TimeEstimator.WordsPerMinute(g, double.NaN));
    }
}

public class MotionTimingsTests
{
    [Fact]
    public void RampChange_FollowsAcceleration()
    {
        Assert.Equal(0.6, MotionTimings.RampChange(Acceleration.Gentle));
        Assert.Equal(0.3, MotionTimings.RampChange(Acceleration.Normal));
        Assert.Equal(0.15, MotionTimings.RampChange(Acceleration.Quick));
    }
}
