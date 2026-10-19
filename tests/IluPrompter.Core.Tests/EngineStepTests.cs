namespace IluPrompter.Core.Tests;

public class EngineStepTests
{
    private const double Lh = TestGeometry.LineHeight;

    [Fact]
    public void StepLines_LandsExactlyOnTheNextLineCentreAt0Point22Seconds()
    {
        var h = new EngineHarness();
        var start = h.Pmin;
        h.Apply(EngineCommand.StepLines(1));
        Assert.Equal(start, h.Pos, 6); // animation has not progressed yet

        h.Run(0.11);
        Assert.Equal(start + (Lh * (1 - 0.125)), h.Pos, 6); // cubic ease-out: remaining = (1 - u)^3

        h.Run(0.11);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 1), h.Pos, 9);
        Assert.Equal(1, h.Status.LineIndex);

        h.Run(1);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 1), h.Pos, 9);
    }

    [Fact]
    public void StepLines_IsEasedOut_FastAtFirst()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.StepLines(1));
        h.Run(0.02);
        var early = h.Pos - h.Pmin;
        h.Run(0.20);
        var late = h.Pos - h.Pmin - early;
        Assert.True(early > Lh * 0.2, "the first 20 ms covers a large share of the move");
        Assert.True(late < Lh * 0.8);
    }

    [Fact]
    public void ThreeQuickSteps_ChainIntoPlusThree_WithOneAnimation()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.StepLines(1), EngineCommand.StepLines(1), EngineCommand.StepLines(1));
        h.Run(0.11);
        Assert.Equal(h.Pmin + (3 * Lh * (1 - 0.125)), h.Pos, 6);
        h.Run(0.11);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9);
        Assert.Equal(3, h.Status.LineIndex);
    }

    [Fact]
    public void StepDuringAnimation_RetargetsFromThePreviousTarget_WithoutAJump()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.StepLines(1));
        h.Run(0.1);
        var before = h.Pos;
        h.Apply(EngineCommand.StepLines(1));
        Assert.Equal(before, h.Pos, 3); // visible position is continuous across the retarget

        double previous = h.Pos;
        for (var i = 0; i < 40; i++)
        {
            h.Run(1.0 / 60);
            Assert.True(h.Pos >= previous - 1e-9, "never moves backwards while chaining");
            previous = h.Pos;
        }

        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 2), h.Pos, 9);
    }

    [Fact]
    public void StepLines_IsClampedToTheFirstAndLastLine()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.StepLines(-5));
        h.Run(0.3);
        Assert.Equal(h.Pmin, h.Pos, 9);

        h.Apply(EngineCommand.StepLines(1000));
        h.Run(0.3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 99), h.Pos, 9);
        Assert.Equal(99, h.Status.LineIndex);
    }

    [Fact]
    public void StepLines_KeepsThePlayState_AndIsSuperposedOnMotion()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(3);
        var p0 = h.Pos;
        var v = h.Speed(4.5);
        var k = h.Geometry.FindLineAt(p0 + h.R);
        h.Apply(EngineCommand.StepLines(2));
        Assert.Equal(TransportState.Playing, h.Status.Transport);
        h.Run(0.22);
        // The step's destination (two lines on from the line that was on the reading line) plus 0.22 s of motion.
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, k + 2) + (v * 0.22), h.Pos, 2);
    }

    [Fact]
    public void StepBackToTheStart_PausesAPlayingPrompt()
    {
        var h = new EngineHarness();
        h.SeekToLine(1);
        h.Play();
        h.Apply(EngineCommand.StepLines(-1));
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    // ---- paragraphs ----

    [Fact]
    public void ParagraphNext_GoesToTheFirstLineOfTheNextParagraph()
    {
        var h = new EngineHarness();
        h.SeekToLine(2);
        h.Apply(EngineCommand.StepParagraphs(1));
        h.Run(0.16);
        Assert.True(h.Pos < TestGeometry.PosOfLine(h.Geometry, 5), "0.32 s animation is still running at 0.16 s");
        h.Run(0.16);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 5), h.Pos, 9);
        Assert.Equal(1, h.Status.ParagraphIndex);
    }

    [Fact]
    public void ParagraphNext_AtTheLastParagraph_DoesNothing()
    {
        var h = new EngineHarness();
        h.SeekToLine(97);
        var p = h.Pos;
        h.Apply(EngineCommand.StepParagraphs(1));
        h.Run(0.5);
        Assert.Equal(p, h.Pos, 9);
    }

    [Fact]
    public void ParagraphPrevious_BelowTheFirstLineByMoreThanHalfALine_GoesToThatParagraphsStart()
    {
        var h = new EngineHarness();
        h.SeekToLine(7); // paragraph 1 (lines 5..9)
        h.Apply(EngineCommand.StepParagraphs(-1));
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 5), h.Pos, 9);

        h.Apply(EngineCommand.StepParagraphs(-1)); // now on the first line: previous paragraph
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 0), h.Pos, 9);

        var p = h.Pos;
        h.Apply(EngineCommand.StepParagraphs(-1)); // already at the start
        h.Run(0.4);
        Assert.Equal(p, h.Pos, 9);
    }

    [Fact]
    public void ParagraphPrevious_LessThanHalfALineBelow_GoesToThePreviousParagraph()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 5) + (0.3 * Lh)));
        h.Apply(EngineCommand.StepParagraphs(-1));
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 0), h.Pos, 9);
    }

    [Fact]
    public void ParagraphSteps_CanMoveSeveralParagraphs()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.StepParagraphs(3));
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 15), h.Pos, 9);
        h.Apply(EngineCommand.StepParagraphs(-2));
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 5), h.Pos, 9);
    }

    /// <summary>Paragraphs 0, 2, 4, 6 have two lines each; 1, 3, 5 are empty (space, no lines), as the geometry builder
    /// makes them for a .txt script with blank lines between paragraphs.</summary>
    private static LayoutGeometry WithEmptyParagraphs()
    {
        double[] tops = [0, 100, 300, 400, 600, 700, 900, 1000];
        int[] paragraphs = [0, 0, 2, 2, 4, 4, 6, 6];
        return new LayoutGeometry("empty-paras", 1, 100, 1200, 1100, tops, Enumerable.Repeat(100.0, 8).ToArray(), paragraphs,
            [0, 20, 0, 20, 0, 20, 0, 20], [0, 200, 300, 500, 600, 800, 900], Array.Empty<SectionMarker>(), 80);
    }

    [Fact]
    public void ParagraphSteps_SkipParagraphsWithoutLines_InBothDirections()
    {
        var h = new EngineHarness(WithEmptyParagraphs(), new ViewportMetrics(1000, 400, 150));
        double At(int line) => h.Geometry.LineCentre(line) - h.R;
        Assert.Equal(At(0), h.Pos, 9);

        h.Apply(EngineCommand.StepParagraphs(1));
        h.Run(0.4);
        Assert.Equal(At(2), h.Pos, 9); // paragraph 2, not the empty paragraph 1

        h.Apply(EngineCommand.StepParagraphs(2));
        h.Run(0.4);
        Assert.Equal(At(6), h.Pos, 9); // two paragraphs with lines further: 4, then 6

        h.Apply(EngineCommand.StepParagraphs(1));
        h.Run(0.4);
        Assert.Equal(At(6), h.Pos, 9); // last paragraph: nothing

        h.Apply(EngineCommand.StepParagraphs(-1));
        h.Run(0.4);
        Assert.Equal(At(4), h.Pos, 9); // previous paragraph with lines (the empty 5 is skipped)

        h.Apply(EngineCommand.StepParagraphs(-2));
        h.Run(0.4);
        Assert.Equal(At(0), h.Pos, 9);
    }

    // ---- sections ----

    private static EngineHarness SectionHarness(PrompterSettings? settings = null) =>
        new(TestGeometry.Create(lines: 100, sectionParagraphs: [2, 6, 10]), settings: settings); // lines 10, 30, 50

    [Fact]
    public void SectionNext_IsACut_AndLeavesThePromptPaused()
    {
        var h = SectionHarness();
        h.Play();
        h.Run(0.5);
        h.Apply(EngineCommand.StepSections(1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 10), h.Pos, 9); // immediate, no animation
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(0, h.Status.SectionIndex);
        h.Run(1);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 10), h.Pos, 9);

        h.Apply(EngineCommand.StepSections(1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 30), h.Pos, 9);
        Assert.Equal(1, h.Status.SectionIndex);
        h.Apply(EngineCommand.StepSections(1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 50), h.Pos, 9);

        h.Apply(EngineCommand.StepSections(1)); // no further section
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 50), h.Pos, 9);
    }

    [Fact]
    public void SectionPrevious_FollowsTheParagraphRule()
    {
        var h = SectionHarness();
        h.SeekToLine(33); // three lines into section 1
        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 30), h.Pos, 9); // start of this section

        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 10), h.Pos, 9); // already at its start: previous section

        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(h.Pmin, h.Pos, 9); // before the first section: the start

        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(h.Pmin, h.Pos, 9);
    }

    [Fact]
    public void SectionPrevious_LessThanHalfALineIn_GoesToThePreviousSection()
    {
        var h = SectionHarness();
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 30) + (0.3 * Lh)));
        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 10), h.Pos, 9);
    }

    [Fact]
    public void GoToSection_AndMultiStepSections()
    {
        var h = SectionHarness();
        h.Apply(EngineCommand.GoToSection(2));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 50), h.Pos, 9);
        Assert.Equal(2, h.Status.SectionIndex);

        h.Apply(EngineCommand.GoToSection(7)); // out of range: ignored
        h.Apply(EngineCommand.GoToSection(-1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 50), h.Pos, 9);

        h.Apply(EngineCommand.JumpToStart());
        h.Apply(EngineCommand.StepSections(2));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 30), h.Pos, 9);
        h.Apply(EngineCommand.StepSections(-2));
        Assert.Equal(h.Pmin, h.Pos, 9);
    }

    [Fact]
    public void SectionPrevious_WithoutSections_GoesToTheStart()
    {
        var h = new EngineHarness();
        h.SeekToLine(20);
        h.Apply(EngineCommand.StepSections(-1));
        Assert.Equal(h.Pmin, h.Pos, 9);
        h.SeekToLine(20);
        h.Apply(EngineCommand.StepSections(1));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 20), h.Pos, 9);
    }

    [Fact]
    public void Status_ReportsSectionParagraphAndAnchor()
    {
        var h = SectionHarness();
        Assert.Equal(-1, h.Status.SectionIndex);
        Assert.Equal(3, h.Status.SectionCount);
        Assert.Equal(20, h.Status.ParagraphCount);
        h.SeekToLine(42);
        Assert.Equal(1, h.Status.SectionIndex);
        Assert.Equal(8, h.Status.ParagraphIndex);
        Assert.Equal(new DocumentAnchor(8, 80), h.Status.ReadingAnchor);
        Assert.Equal(42, h.Status.LineIndex);
    }
}
