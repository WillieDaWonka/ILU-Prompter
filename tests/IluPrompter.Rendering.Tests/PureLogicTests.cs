using System.Globalization;
using System.Runtime;
using System.Windows;
using IluPrompter.Core;
using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

public class LayoutKeyTests
{
    private static readonly OutputGeometry Hd = OutputGeometry.Default1080;

    private static string Key(long version = 1, MarginSettings? margins = null, TextSettings? text = null, OutputSettings? output = null, OutputGeometry? geometry = null)
    {
        var t = text ?? new TextSettings();
        var o = output ?? new OutputSettings();
        var area = TypographyMath.ComputeTextArea(geometry ?? Hd, margins ?? new MarginSettings(), t, o);
        return SurfaceLayout.ComputeLayoutKey(version, area, t, o);
    }

    [Fact]
    public void SameInputs_GiveTheSameKey() => Assert.Equal(Key(), Key());

    [Fact]
    public void KeyHasTheDocumentedShape()
    {
        // (1080-64-64)/(8*1.3) = 91.538..., width = 1920-160-160 = 1600.
        var key = Key();
        Assert.StartsWith("1|1600|", key);
        Assert.EndsWith("|1.3|Poppins|500|0.5|Left|False", key);
    }

    [Fact]
    public void EveryLayoutInputChangesTheKey()
    {
        var baseline = Key();
        var variants = new Dictionary<string, string>
        {
            ["snapshot version"] = Key(version: 2),
            ["margin left (width and size)"] = Key(margins: new MarginSettings { Left = 0.2 }),
            ["margin top (font size)"] = Key(margins: new MarginSettings { Top = 0.1 }),
            ["lines visible"] = Key(text: new TextSettings { LinesVisible = 6 }),
            ["line spacing"] = Key(text: new TextSettings { LineSpacing = 1.5 }),
            ["font face"] = Key(text: new TextSettings { FontFace = "Arial" }),
            ["font weight"] = Key(text: new TextSettings { FontWeight = 400 }),
            ["paragraph gap"] = Key(text: new TextSettings { ParagraphGapLines = 1.0 }),
            ["alignment"] = Key(output: new OutputSettings { Alignment = TextAlignmentSetting.Centre }),
            ["section names"] = Key(output: new OutputSettings { ShowSectionNames = true }),
            ["output size"] = Key(geometry: new OutputGeometry(3840, 2160, 1.5, 60)),
        };

        foreach (var (name, key) in variants)
        {
            Assert.NotEqual(baseline, key);
        }

        Assert.Equal(variants.Count, variants.Values.Distinct().Count());   // and no two variants collide
    }

    [Fact]
    public void AppearanceOnlySettings_DoNotChangeTheKey()
    {
        var baseline = Key();
        Assert.Equal(baseline, Key(text: new TextSettings { DefaultInk = "ink-yellow" }));
        Assert.Equal(baseline, Key(output: new OutputSettings { ReadingLineFraction = 0.5 }));
        Assert.Equal(baseline, Key(output: new OutputSettings { ShowReadingMarker = false, DimAboveReadingLine = true }));
        Assert.Equal(baseline, Key(output: new OutputSettings { LastWidthPx = 800 }));
    }

    [Fact]
    public void ContentKey_AddsInkOnly()
    {
        var layout = Key();
        var white = SurfaceLayout.ComputeContentKey(layout, new TextSettings());
        var yellow = SurfaceLayout.ComputeContentKey(layout, new TextSettings { DefaultInk = "ink-yellow" });
        Assert.NotEqual(white, yellow);
        Assert.StartsWith(layout, white);
    }

    [Fact]
    public void Key_IsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var german = Key();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(Key(), german);
            Assert.DoesNotContain(",", german);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

public class MarkerPlacementTests
{
    private const double Line = 100;       // base line height
    private const double Width = 1920;
    private const double ReadingY = 400;

    [Fact]
    public void WideMargins_GetTrianglesBesideTheText()
    {
        var m = SurfaceLayout.ComputeMarkers(Width, 160, 160, Line, ReadingY);
        Assert.Equal(MarkerKind.Triangle, m.Left.Kind);
        Assert.Equal(MarkerKind.Triangle, m.Right.Kind);

        // Height 0.5 x line height, centred on the reading line.
        Assert.Equal(50, m.Left.Bounds.Height, 6);
        Assert.Equal(ReadingY, m.Left.Bounds.Top + (m.Left.Bounds.Height / 2), 6);
        Assert.Equal(ReadingY, m.Right.Bounds.Top + (m.Right.Bounds.Height / 2), 6);

        // Inside the margins, a small gap from the text, mirror images of each other.
        Assert.True(m.Left.Bounds.Right < 160 && m.Left.Bounds.Left > 0);
        Assert.True(m.Right.Bounds.Left > Width - 160 && m.Right.Bounds.Right < Width);
        Assert.Equal(160 - m.Left.Bounds.Right, m.Right.Bounds.Left - (Width - 160), 6);
        Assert.Equal(m.Left.Bounds.Width, m.Right.Bounds.Width, 6);
    }

    [Theory]
    [InlineData(59.99, true)]
    [InlineData(60.0, false)]
    [InlineData(0.0, true)]
    [InlineData(300.0, false)]
    public void MarginThreshold_IsSixTenthsOfALineHeight(double margin, bool expectBar)
    {
        var expected = expectBar ? MarkerKind.Bar : MarkerKind.Triangle;
        Assert.Equal(expected, SurfaceLayout.ComputeMarkers(Width, margin, margin, Line, ReadingY).Left.Kind);
        Assert.Equal(expected, SurfaceLayout.ComputeMarkers(Width, margin, margin, Line, ReadingY).Right.Kind);
    }

    [Fact]
    public void NarrowSide_BecomesASixDipBarAtTheScreenEdge_OtherSideKeepsItsTriangle()
    {
        var m = SurfaceLayout.ComputeMarkers(Width, 20, 160, Line, ReadingY);
        Assert.Equal(MarkerKind.Bar, m.Left.Kind);
        Assert.Equal(0, m.Left.Bounds.Left);
        Assert.Equal(6, m.Left.Bounds.Width);
        Assert.Equal(MarkerKind.Triangle, m.Right.Kind);

        var swapped = SurfaceLayout.ComputeMarkers(Width, 160, 20, Line, ReadingY);
        Assert.Equal(MarkerKind.Bar, swapped.Right.Kind);
        Assert.Equal(Width, swapped.Right.Bounds.Right);
        Assert.Equal(MarkerKind.Triangle, swapped.Left.Kind);
    }

    [Fact]
    public void DimBand_StopsHalfALineAboveTheReadingLine()
    {
        var area = new TextArea(160, 64, 1600, 952, 314, 91.5, 119);
        var band = SurfaceLayout.ComputeDimBand(area);
        Assert.Equal(160, band.X);
        Assert.Equal(64, band.Y);
        Assert.Equal(1600, band.Width);
        Assert.Equal(314 - 59.5, band.Height, 6);
    }

    [Fact]
    public void DimBand_NeverNegative()
    {
        var band = SurfaceLayout.ComputeDimBand(new TextArea(0, 0, 100, 100, 10, 10, 100));
        Assert.Equal(0, band.Height);
    }
}

public class PreviewMathTests
{
    private static TextArea DefaultArea => TypographyMath.ComputeTextArea(OutputGeometry.Default1080, new MarginSettings(), new TextSettings(), new OutputSettings());

    [Fact]
    public void SixteenByNine_FillsAWideRectangleVertically_AndIsCentredHorizontally()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var inner = 392 - (2 * PreviewMath.FramePad);
        Assert.Equal(inner / 1080, layout.Scale, 9);
        Assert.Equal(1080 * layout.Scale, layout.Stage.Height, 6);
        Assert.Equal(PreviewMath.FramePad, layout.Stage.Y, 6);
        Assert.Equal((696 - layout.Stage.Width) / 2, layout.Stage.X, 6);
        Assert.Equal(1920.0 / 1080.0, layout.Stage.Width / layout.Stage.Height, 9);
    }

    [Fact]
    public void TallRectangle_LetterboxesAboveAndBelow()
    {
        var layout = PreviewMath.ComputeLayout(new Size(400, 600), 1920, 1080);
        var inner = 400 - (2 * PreviewMath.FramePad);
        Assert.Equal(inner / 1920, layout.Scale, 9);
        Assert.Equal(PreviewMath.FramePad, layout.Stage.X, 6);
        Assert.Equal((600 - layout.Stage.Height) / 2, layout.Stage.Y, 6);
    }

    [Fact]
    public void OtherAspectRatios_KeepTheOutputAspect()
    {
        var layout = PreviewMath.ComputeLayout(new Size(800, 400), 2560, 1080);   // 21:9 output
        Assert.Equal(2560.0 / 1080.0, layout.Stage.Width / layout.Stage.Height, 9);
        Assert.True(layout.Stage.Width <= 800 && layout.Stage.Height <= 400);
    }

    [Fact]
    public void Degenerate_InputsGiveAnEmptyLayout()
    {
        Assert.Equal(0, PreviewMath.ComputeLayout(new Size(0, 0), 1920, 1080).Scale);
        Assert.Equal(0, PreviewMath.ComputeLayout(new Size(3, 3), 1920, 1080).Scale);
        Assert.Equal(0, PreviewMath.ComputeLayout(new Size(100, 100), 0, 1080).Scale);
    }

    [Fact]
    public void DesiredSize_FollowsTheOutputAspectWhenOneAxisIsFree()
    {
        var byWidth = PreviewMath.ComputeDesiredSize(new Size(700, double.PositiveInfinity), 1920, 1080);
        Assert.Equal(700, byWidth.Width);
        Assert.Equal(((700 - 4) * 1080.0 / 1920) + 4, byWidth.Height, 6);
        Assert.Equal(new Size(500, 300), PreviewMath.ComputeDesiredSize(new Size(500, 300), 1920, 1080));
        Assert.Equal(new Size(1924, 1084), PreviewMath.ComputeDesiredSize(new Size(double.PositiveInfinity, double.PositiveInfinity), 1920, 1080));
    }

    [Fact]
    public void ToOutput_InvertsTheLayout()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var screen = new Point(layout.Stage.X + (layout.Scale * 160), layout.Stage.Y + (layout.Scale * 64));
        var o = PreviewMath.ToOutput(screen, layout);
        Assert.Equal(160, o.X, 6);
        Assert.Equal(64, o.Y, 6);
        Assert.True(PreviewMath.IsOverStage(screen, layout));
        Assert.False(PreviewMath.IsOverStage(new Point(1, 1), layout));
    }

    [Fact]
    public void HitTest_FindsEachMarginEdgeAndTheReadingLine()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var area = DefaultArea;
        Point At(double ox, double oy) => new(layout.Stage.X + (ox * layout.Scale), layout.Stage.Y + (oy * layout.Scale));
        PreviewHandle Hit(Point p) => PreviewMath.HitTest(p, layout, area, 1920, 1080);

        Assert.Equal(PreviewHandle.Left, Hit(At(160, 500)));
        Assert.Equal(PreviewHandle.Right, Hit(At(1760, 500)));
        Assert.Equal(PreviewHandle.Top, Hit(At(900, 64)));
        Assert.Equal(PreviewHandle.Bottom, Hit(At(900, 1016)));
        Assert.Equal(PreviewHandle.ReadingLine, Hit(At(900, 64 + area.ReadingLineY)));
        Assert.Equal(PreviewHandle.None, Hit(At(900, 500)));

        // Within the 5 px tolerance (in preview DIPs) but not beyond.
        Assert.Equal(PreviewHandle.Left, Hit(new Point(At(160, 500).X + 4.5, At(160, 500).Y)));
        Assert.Equal(PreviewHandle.None, Hit(new Point(At(160, 500).X + 6, At(160, 500).Y)));
    }

    [Fact]
    public void HitTest_ReadingLineOnlyReactsInsideTheTextArea()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var area = DefaultArea;
        var inMargin = new Point(layout.Stage.X + (80 * layout.Scale), layout.Stage.Y + ((64 + area.ReadingLineY) * layout.Scale));
        Assert.Equal(PreviewHandle.None, PreviewMath.HitTest(inMargin, layout, area, 1920, 1080));
    }

    [Fact]
    public void HitTest_MarginEdgeWinsOverTheReadingLineAtTheirCorner()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var area = DefaultArea;
        var corner = new Point(layout.Stage.X + (160 * layout.Scale), layout.Stage.Y + ((64 + area.ReadingLineY) * layout.Scale));
        Assert.Equal(PreviewHandle.Left, PreviewMath.HitTest(corner, layout, area, 1920, 1080));
    }

    [Fact]
    public void HitTest_ZeroMarginEdgeStillGrabbableAtTheStageBoundary()
    {
        var layout = PreviewMath.ComputeLayout(new Size(696, 392), 1920, 1080);
        var area = TypographyMath.ComputeTextArea(OutputGeometry.Default1080, new MarginSettings { Left = 0 }, new TextSettings(), new OutputSettings());
        var p = new Point(layout.Stage.X - 2, layout.Stage.Y + 100);
        Assert.Equal(PreviewHandle.Left, PreviewMath.HitTest(p, layout, area, 1920, 1080));
    }

    [Theory]
    [InlineData(MarginEdge.Left, 160, 500, 160)]
    [InlineData(MarginEdge.Right, 1760, 500, 160)]
    [InlineData(MarginEdge.Top, 900, 64, 64)]
    [InlineData(MarginEdge.Bottom, 900, 1016, 64)]
    public void EdgePixels_AreMeasuredFromTheirOwnOutputEdge(MarginEdge edge, double x, double y, double expected)
    {
        Assert.Equal(expected, PreviewMath.EdgePixels(edge, new Point(x, y), OutputGeometry.Default1080, snap: false), 6);
    }

    [Fact]
    public void EdgePixels_UseDeviceScale_ShiftSnapsToEight_AndNeverGoNegative()
    {
        var hiDpi = new OutputGeometry(3840, 2160, 2.0, 60);   // 1920 x 1080 DIP
        Assert.Equal(320, PreviewMath.EdgePixels(MarginEdge.Left, new Point(160, 0), hiDpi, snap: false), 6);
        Assert.Equal(1, PreviewMath.EdgePixels(MarginEdge.Left, new Point(1, 1), new OutputGeometry(1920, 1080, 1.0, 60), snap: false), 6);
        Assert.Equal(168, PreviewMath.EdgePixels(MarginEdge.Left, new Point(165, 0), OutputGeometry.Default1080, snap: true), 6);
        Assert.Equal(160, PreviewMath.EdgePixels(MarginEdge.Left, new Point(163.9, 0), OutputGeometry.Default1080, snap: true), 6);
        Assert.Equal(0, PreviewMath.EdgePixels(MarginEdge.Left, new Point(-50, 0), OutputGeometry.Default1080, snap: false));
        Assert.Equal(0, PreviewMath.EdgePixels(MarginEdge.Right, new Point(5000, 0), OutputGeometry.Default1080, snap: false));
    }

    [Fact]
    public void ReadingLineFraction_IsRelativeToTheTextArea_ClampedAndSnappable()
    {
        var area = DefaultArea;     // top 64, height 952
        var middle = PreviewMath.ReadingLineFraction(new Point(900, 64 + (952 * 0.4)), area, OutputGeometry.Default1080, snap: false);
        Assert.Equal(0.4, middle, 9);
        Assert.Equal(PreviewMath.MinReadingLine, PreviewMath.ReadingLineFraction(new Point(900, 0), area, OutputGeometry.Default1080, false));
        Assert.Equal(PreviewMath.MaxReadingLine, PreviewMath.ReadingLineFraction(new Point(900, 1080), area, OutputGeometry.Default1080, false));

        var snapped = PreviewMath.ReadingLineFraction(new Point(900, 64 + 300), area, OutputGeometry.Default1080, snap: true);
        Assert.Equal(0, ((snapped * 952) % 8), 6);   // a whole multiple of 8 px below the text-area top
    }
}

public class FrameClockTests
{
    private const long Freq = 10_000_000;   // Stopwatch frequency used by Windows; equals TimeSpan ticks

    [Fact]
    public void RepeatedRenderingTime_IsRejected()
    {
        var clock = new FrameClock(Freq);
        Assert.True(clock.TryMap(TimeSpan.FromMilliseconds(100), 1_000_000, out _));
        Assert.False(clock.TryMap(TimeSpan.FromMilliseconds(100), 1_000_000, out _));
        Assert.True(clock.TryMap(TimeSpan.FromMilliseconds(116.7), 1_160_000, out _));
    }

    [Fact]
    public void MappedTimestamps_KeepTheCompositorsFrameSpacing_EvenWhenTheHandlerRunsLate()
    {
        var clock = new FrameClock(Freq);
        Assert.True(clock.TryMap(TimeSpan.FromMilliseconds(0), 5_000_000, out var t0));
        // Handler ran 3 ms late on frame 1 and 1 ms early on frame 2; the compositor says exactly 16.667 ms apart.
        Assert.True(clock.TryMap(TimeSpan.FromTicks(166_667), 5_000_000 + 166_667 + 30_000, out var t1));
        Assert.True(clock.TryMap(TimeSpan.FromTicks(333_334), 5_000_000 + 333_334 - 10_000, out var t2));
        Assert.Equal(166_667, t1 - t0);
        Assert.Equal(166_667, t2 - t1);
    }

    [Fact]
    public void Timestamps_AreStrictlyIncreasing_EvenIfRenderingTimeGoesBackwards()
    {
        var clock = new FrameClock(Freq);
        var last = long.MinValue;
        foreach (var ms in new[] { 100.0, 116.0, 90.0, 91.0, 91.5, 400.0 })
        {
            Assert.True(clock.TryMap(TimeSpan.FromMilliseconds(ms), 2_000_000 + (long)(ms * 10_000), out var t));
            Assert.True(t > last);
            last = t;
        }
    }

    [Fact]
    public void LargeDrift_ReanchorsToTheStopwatch()
    {
        var clock = new FrameClock(Freq);
        Assert.True(clock.TryMap(TimeSpan.FromSeconds(1), 10_000_000, out _));
        // Two seconds later the compositor time only advanced 10 ms: the mapping would be far from "now".
        var now = 10_000_000 + (2 * Freq);
        Assert.True(clock.TryMap(TimeSpan.FromSeconds(1.01), now, out var t));
        Assert.InRange(t, now - 100_000, now + 100_000);
    }

    [Fact]
    public void NonDefaultStopwatchFrequency_IsScaled()
    {
        var clock = new FrameClock(1_000_000_000);   // 1 GHz counter
        Assert.True(clock.TryMap(TimeSpan.Zero, 1_000, out var a));
        Assert.True(clock.TryMap(TimeSpan.FromMilliseconds(16), 1_000 + 16_000_000, out var b));
        Assert.Equal(16_000_000, b - a);
    }
}

public class FrameStatsTests
{
    private const long Freq = 1_000_000;   // 1 tick = 1 microsecond

    [Fact]
    public void SteadyFrames_ReportTheNominalInterval_AndNoSlowFrames()
    {
        var stats = new FrameStats("t", null, 60, TimeSpan.FromSeconds(1000), Freq);
        long t = 1_000_000;
        for (var i = 0; i < 300; i++)
        {
            stats.Record(t);
            t += 16_667;
        }

        var r = stats.Summarise();
        Assert.Equal(299, r.Frames);
        Assert.Equal(16.667, r.Mean, 2);
        Assert.Equal(16.667, r.P99, 2);
        Assert.Equal(16.667, r.Max, 2);
        Assert.Equal(0, r.OverThreshold);
        Assert.Equal(1000.0 / 60, r.NominalMs, 6);
    }

    [Fact]
    public void SlowFrames_AreCounted_AndShowInThePercentiles()
    {
        var stats = new FrameStats("t", null, 60, TimeSpan.FromSeconds(1000), Freq);
        long t = 1_000_000;
        stats.Record(t);
        for (var i = 0; i < 100; i++)
        {
            t += i % 50 == 49 ? 40_000 : 16_667;     // two 40 ms hitches (> 1.5 x 16.67 = 25 ms)
            stats.Record(t);
        }

        var r = stats.Summarise();
        Assert.Equal(100, r.Frames);
        Assert.Equal(2, r.OverThreshold);
        Assert.Equal(40.0, r.Max, 2);
        Assert.Equal(40.0, r.P99, 2);
        Assert.Equal(16.667, r.P95, 2);
    }

    [Fact]
    public void LongGaps_AreNotFrameIntervals()
    {
        var stats = new FrameStats("t", null, 60, TimeSpan.FromSeconds(1000), Freq);
        stats.Record(1_000_000);
        stats.Record(1_016_667);
        stats.Record(9_000_000);     // the window was hidden for 8 s
        stats.Record(9_016_667);
        var r = stats.Summarise();
        Assert.Equal(2, r.Frames);
        Assert.Equal(16.667, r.Max, 2);
    }

    [Fact]
    public void EveryWindow_ALineIsLogged_WithGcCounts_AndTheBufferStartsAgain()
    {
        var log = new RecordingLog { DiagnosticsEnabled = true };
        var stats = new FrameStats("output", log, 60, TimeSpan.FromSeconds(1), Freq);
        long t = 1_000_000;
        for (var i = 0; i < 200; i++)
        {
            stats.Record(t);
            t += 16_667;
        }

        var lines = log.Lines.Where(l => l.StartsWith("TRACE [frames:output]", StringComparison.Ordinal)).ToList();
        Assert.True(lines.Count >= 2, string.Join("\n", log.Lines));
        Assert.Contains("p99", lines[0]);
        Assert.Contains("GC +", lines[0]);
        Assert.True(stats.LastReport.Frames is > 55 and < 65);
    }

    [Fact]
    public void RingBuffer_WrapsWithoutGrowing()
    {
        var stats = new FrameStats("t", null, 60, TimeSpan.FromSeconds(100000), Freq);
        long t = 1_000_000;
        for (var i = 0; i < FrameStats.Capacity + 500; i++)
        {
            stats.Record(t);
            t += 16_667;
        }

        Assert.Equal(FrameStats.Capacity, stats.Summarise().Frames);
    }

    [Fact]
    public void Record_DoesNotAllocate()
    {
        var stats = new FrameStats("t", null, 60, TimeSpan.FromSeconds(100000), Freq);
        long t = 1_000_000;
        for (var i = 0; i < 100; i++) { stats.Record(t); t += 16_667; }   // warm up

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { stats.Record(t); t += 16_667; }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

public class DisplaySleepGuardTests
{
    [Fact]
    public void Playing_HoldsTheDisplayAndRaisesGcLatency_ThenRestoresBoth()
    {
        var states = new List<bool>();
        var gc = GCLatencyMode.Interactive;
        var guard = new DisplaySleepGuard(states.Add, () => gc, m => gc = m);

        guard.Update(true);
        Assert.True(guard.IsHeld);
        Assert.Equal(GCLatencyMode.SustainedLowLatency, gc);

        guard.Update(true);
        guard.Update(true);
        Assert.Single(states);

        guard.Update(false);
        Assert.False(guard.IsHeld);
        Assert.Equal(GCLatencyMode.Interactive, gc);
        Assert.Equal(new[] { true, false }, states);
    }

    [Fact]
    public void NotHeld_DoesNothing_AndDisposeReleases()
    {
        var states = new List<bool>();
        var gc = GCLatencyMode.Batch;
        var guard = new DisplaySleepGuard(states.Add, () => gc, m => gc = m);
        guard.Update(false);
        Assert.Empty(states);

        guard.Update(true);
        guard.Dispose();
        Assert.Equal(new[] { true, false }, states);
        Assert.Equal(GCLatencyMode.Batch, gc);
    }

    [Fact]
    public void RealGuard_CanBeToggledOnTheCurrentThread()
    {
        // Only affects this thread's own power request, and is released again.
        var guard = new DisplaySleepGuard();
        guard.Update(true);
        guard.Update(false);
        guard.Dispose();
    }
}
