namespace IluPrompter.Core.Tests;

public class EngineLayoutTests
{
    private const double Lh = TestGeometry.LineHeight;

    [Fact]
    public void FontChange_KeepsTheSameAnchorOnTheReadingLine()
    {
        var oldGeometry = TestGeometry.Create(lines: 100, lineHeight: Lh, linesPerParagraph: 5, key: "a", charsPerLine: 40);
        var h = new EngineHarness(oldGeometry);
        h.SeekToLine(23); // paragraph 4, offset 120
        var anchor = h.Status.ReadingAnchor;
        Assert.Equal(new DocumentAnchor(4, 120), anchor);

        // A larger font: lines are twice as tall and half as many; each paragraph wraps to two lines.
        var newGeometry = TestGeometry.Create(lines: 40, lineHeight: Lh * 2, linesPerParagraph: 2, key: "b", charsPerLine: 100);
        h.SetLayout(newGeometry);

        var expectedLine = newGeometry.FindLineForAnchor(anchor);
        Assert.Equal(9, expectedLine);
        Assert.Equal(expectedLine, h.Status.LineIndex);
        Assert.Equal(newGeometry.LineCentre(expectedLine), h.Pos + h.R, 6); // still centred, as before
        Assert.Same(newGeometry, h.Engine.Geometry);
        Assert.Equal("b", h.Status.LayoutKey);
        Assert.Equal(40, h.Status.LineCount);
    }

    [Fact]
    public void FontChange_KeepsTheFractionalPositionInsideTheLine()
    {
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 23) + (0.25 * Lh))); // 75 % down line 23
        var newGeometry = TestGeometry.Create(lines: 100, lineHeight: 150, linesPerParagraph: 5, key: "b");
        h.SetLayout(newGeometry);
        Assert.Equal(23, h.Status.LineIndex);
        Assert.Equal(newGeometry.LineTops[23] + (0.75 * 150), h.Pos + h.R, 6);
    }

    [Fact]
    public void LinesHalving_AnchorSurvives_WhenWrappingChanges()
    {
        var h = new EngineHarness(TestGeometry.Create(lines: 100, key: "a"));
        h.SeekToLine(60);
        var anchor = h.Status.ReadingAnchor;
        h.SetLayout(TestGeometry.Create(lines: 50, lineHeight: 239.2, linesPerParagraph: 3, key: "b", charsPerLine: 80));
        var line = h.Geometry.FindLineForAnchor(anchor);
        Assert.Equal(line, h.Status.LineIndex);
    }

    [Fact]
    public void ViewportOnlyChange_KeepsTheSameDocumentYOnTheReadingLine()
    {
        var h = new EngineHarness();
        h.SeekToLine(10);
        var y = h.Pos + h.R;
        h.SetLayout(h.Geometry, new ViewportMetrics(1600, 800, 400));
        Assert.Equal(y, h.Pos + 400, 6);
        Assert.Equal(new ViewportMetrics(1600, 800, 400), h.Engine.Viewport);
        Assert.Equal(10, h.Status.LineIndex);
    }

    [Fact]
    public void ViewportChange_RecomputesTheBounds()
    {
        var h = new EngineHarness();
        h.SetLayout(h.Geometry, new ViewportMetrics(1600, 900, 200));
        h.Apply(EngineCommand.JumpToStart());
        Assert.Equal(TestGeometry.Pmin(h.Geometry, 200), h.Pos, 9);
        h.Apply(EngineCommand.JumpToEnd());
        Assert.Equal(TestGeometry.Pmax(h.Geometry, 200), h.Pos, 9);
    }

    [Fact]
    public void SameLayoutKey_IsIgnored_ExceptTheViewport()
    {
        var h = new EngineHarness();
        h.SeekToLine(12);
        var first = h.Engine.Geometry;
        var p = h.Pos;

        // Same key but different content: the engine must keep what it has.
        var other = TestGeometry.Create(lines: 5, key: "k1");
        h.Engine.SetLayout(other, TestGeometry.Viewport());
        Assert.Same(first, h.Engine.Geometry);
        Assert.Equal(p, h.Pos, 12);
        Assert.Equal(100, h.Status.LineCount);
    }

    [Fact]
    public void RunningStepAnimation_KeepsRunningFromWhereItIs_ThroughAReLayout()
    {
        // Changed at the live-edit fix: snapping to the target made the text on the reading line jump mid-step.
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.Apply(EngineCommand.StepLines(3));
        h.Run(0.05);
        var shown = h.Pos;
        Assert.True(shown < TestGeometry.PosOfLine(h.Geometry, 3) - 1);
        h.SetLayout(TestGeometry.Create(key: "b")); // same content, new key
        Assert.Equal(shown, h.Pos, 6);              // no jump
        h.Run(0.5);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 6); // and it arrives
    }

    [Fact]
    public void AtEnd_SurvivesAReLayout()
    {
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.Apply(EngineCommand.JumpToEnd());
        h.SetLayout(TestGeometry.Create(lines: 160, lineHeight: 90, key: "b"));
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
        Assert.Equal(h.Pmax, h.Pos, 9);
    }

    [Fact]
    public void FirstLayout_StartsAtPmin()
    {
        var engine = new ScrollEngine(new FakeSettings());
        var g = TestGeometry.Create();
        engine.SetLayout(g, TestGeometry.Viewport());
        Assert.Equal(TestGeometry.Pmin(g), engine.ReadPosition(), 9);
        Assert.Same(g, engine.Geometry);
        Assert.Equal(100, engine.Status.LineCount);
    }

    [Fact]
    public void ShrinkingScript_ClampsInsideTheNewBounds()
    {
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.SeekToLine(90);
        h.SetLayout(TestGeometry.Create(lines: 20, key: "b"));
        Assert.InRange(h.Pos, h.Pmin, h.Pmax);
    }

    [Fact]
    public void EmptyScript_IsSafe()
    {
        var h = new EngineHarness(TestGeometry.Create(lines: 0, key: "e"));
        Assert.True(double.IsFinite(h.Pos));
        h.Play();
        h.Run(3);
        h.Apply(EngineCommand.StepLines(1), EngineCommand.StepParagraphs(1), EngineCommand.StepSections(-1), EngineCommand.SeekToAnchor(new DocumentAnchor(5, 5)));
        h.Run(1);
        Assert.InRange(h.Pos, h.Pmin, h.Pmax);
        Assert.Equal(-1, h.Status.LineIndex);
        Assert.Equal(0, h.Status.LineCount);
        Assert.Equal(0.0, h.Status.SecondsRemaining);
    }

    [Fact]
    public void ReLayout_WhilePlaying_ContinuesPlaying()
    {
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.SeekToLine(10);
        h.Play();
        h.Run(2);
        h.SetLayout(TestGeometry.Create(lineHeight: 140, key: "b"));
        var p = h.Pos;
        h.Run(1);
        Assert.Equal(TransportState.Playing, h.Status.Transport);
        Assert.True(h.Pos > p);
    }
}

public class EngineStatusTests
{
    [Fact]
    public void Status_IsNotReplaced_WhileNothingChanges()
    {
        var h = new EngineHarness();
        var events = 0;
        h.Engine.StatusChanged += (_, _) => events++;
        var status = h.Status;
        h.Run(2);
        Assert.Same(status, h.Status);
        Assert.Equal(0, events);
    }

    [Fact]
    public void StatusChanged_IsRaised_WhenAFieldChanges_AndCarriesTheNewStatus()
    {
        var h = new EngineHarness();
        EngineStatus? last = null;
        var events = 0;
        h.Engine.StatusChanged += (_, s) => { events++; last = s; };
        h.Play();
        Assert.Equal(1, events);
        Assert.Same(h.Status, last);
        Assert.Equal(TransportState.Playing, last!.Transport);
    }

    [Fact]
    public void Status_ReplacedOnlyWhenARoundedFieldChanges_AtSteadySpeed()
    {
        // Lines far taller than the distance travelled, so the line index cannot change.
        var geometry = HugeLines();
        var h = new EngineHarness(geometry);
        h.Play();
        h.Run(3); // ramp finished
        var status = h.Status;
        var events = 0;
        h.Engine.StatusChanged += (_, _) => events++;
        h.Run(5);
        Assert.Equal(0, events);
        Assert.Same(status, h.Status);
    }

    [Fact]
    public void Status_AtSteadySpeed_IsReplacedExactlyWhenTheLineChanges()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(3); // let the ramp finish
        var seen = new List<int> { h.Status.LineIndex };
        h.Engine.StatusChanged += (_, s) => seen.Add(s.LineIndex); // asserting here would be swallowed by Tick
        h.Run(10);
        Assert.InRange(seen.Count - 1, 3, 6); // 28 lpm is one line every 2.1 s
        for (var i = 1; i < seen.Count; i++)
        {
            Assert.NotEqual(seen[i - 1], seen[i]); // every replacement is a real line change
        }

        Assert.Equal(seen[^1], h.Status.LineIndex);
    }

    [Fact]
    public void Status_SecondsRemaining_IsTheDistanceToEndAtTheSetSpeed_InDisplayScrolling()
    {
        // Display scrolling: base lines of distance from the current line's centre to END's centre (100 lines of 119.6,
        // END right after the last line: 100 base lines from line 0, 50 from line 50).
        var h = new EngineHarness();
        var curve = h.Settings.Current.Scroll.Curve;
        Assert.Equal(Math.Round(TimeEstimator.SecondsForBaseLines(100, 4.5, curve)), h.Status.SecondsRemaining);
        h.SeekToLine(50);
        Assert.Equal(Math.Round(TimeEstimator.SecondsForBaseLines(50, 4.5, curve)), h.Status.SecondsRemaining);
        h.Apply(EngineCommand.SetSpeed(-9));
        Assert.Equal(Math.Round(TimeEstimator.SecondsForBaseLines(50, -9, curve)), h.Status.SecondsRemaining);
        h.Apply(EngineCommand.SetSpeed(0));
        Assert.True(double.IsPositiveInfinity(h.Status.SecondsRemaining));
        h.Apply(EngineCommand.JumpToEnd());
        Assert.Equal(0.0, h.Status.SecondsRemaining);
    }

    [Fact]
    public void Status_SecondsRemaining_CountsTextLines_InLineScrolling()
    {
        var h = new EngineHarness();
        var curve = h.Settings.Current.Scroll.Curve;
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Run(0.5);
        h.SeekToLine(50);
        Assert.Equal(Math.Round(TimeEstimator.SecondsForLines(49, 4.5, curve)), h.Status.SecondsRemaining);
    }

    [Fact]
    public void Status_SecondsRemaining_IncludesGapsBetweenLines_InDisplayScrolling()
    {
        // Real scripts have paragraph gaps and empty lines that take scrolling time but are not lines.
        var tops = new double[] { 0, 100, 300, 400 };
        var geometry = new LayoutGeometry("gaps", 1, 100, 600, 500, tops, [100, 100, 100, 100], [0, 0, 2, 2], [0, 10, 0, 10],
            [0, 200, 300], Array.Empty<SectionMarker>(), 40);
        var h = new EngineHarness(geometry, new ViewportMetrics(1000, 300, 100));
        var curve = h.Settings.Current.Scroll.Curve;
        Assert.Equal(0, h.Status.LineIndex);
        Assert.Equal(Math.Round(TimeEstimator.SecondsForBaseLines(5, 4.5, curve)), h.Status.SecondsRemaining); // 50 -> 550
    }

    [Fact]
    public void Status_ReportsSpeedUnits()
    {
        var h = new EngineHarness(TestGeometry.Create(wordCount: 500));
        h.Play();
        h.Run(2);
        var s = h.Status;
        Assert.Equal(4.5, s.EffectiveSpeed, 1);
        Assert.Equal(28.3, s.LinesPerMinute, 0.06);
        Assert.Equal(28.3 * 5, s.WordsPerMinute, 0.5);
        Assert.Equal(0.0, s.StepIntervalSeconds);
        Assert.False(s.IsAxisActive);
        Assert.Equal(100, s.LineCount);
        Assert.False(s.IsAtStart);
    }

    [Fact]
    public void Status_ReverseSpeed_HasNegativeLinesPerMinute()
    {
        var h = new EngineHarness();
        h.SeekToLine(50);
        h.Apply(EngineCommand.SetSpeed(-4.5));
        h.Play();
        h.Run(2);
        Assert.True(h.Status.LinesPerMinute < 0);
        Assert.True(h.Status.WordsPerMinute > 0);
        Assert.Equal(-4.5, h.Status.EffectiveSpeed, 1);
    }

    [Fact]
    public void StatusChanged_IsRaisedOnTheTickingThread_AndASubscriberCannotBreakTick()
    {
        var h = new EngineHarness();
        var tickingThread = Environment.CurrentManagedThreadId;
        var observed = -1;
        h.Engine.StatusChanged += (_, _) => observed = Environment.CurrentManagedThreadId;
        h.Engine.StatusChanged += (_, _) => throw new InvalidOperationException("faulty UI subscriber");
        h.Play();
        h.Run(0.5);
        Assert.Equal(tickingThread, observed);
        Assert.Equal(TransportState.Playing, h.Status.Transport);
    }

    private static LayoutGeometry HugeLines() =>
        new("huge", 1, 100, 3e9, 3e9, [0.0, 1e9, 2e9], [1e9, 1e9, 1e9], [0, 1, 2], [0, 0, 0], [0.0, 1e9, 2e9], Array.Empty<SectionMarker>(), 15);
}
