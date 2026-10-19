namespace IluPrompter.Core.Tests;

/// <summary>
/// The live-edit invariant (ARCHITECTURE §2.4 "Layout change"): while the output shows, no edit anywhere may move the
/// text that is on the reading line. Each test lays a script out, puts a line on the reading line, edits the script
/// (a new snapshot version) and installs the new layout, then checks the reading line shows the same text at the same
/// place, in the same SetLayout call (no frame at the old position in between).
/// </summary>
public class EngineLiveEditTests
{
    private const double Lh = ScriptLayout.LineHeight;

    private sealed class Live
    {
        private long _version = 1;

        public Live(List<string>? script = null, ScrollMode mode = ScrollMode.Display)
        {
            Script = script ?? ScriptLayout.Script();
            H = new EngineHarness(ScriptLayout.Build(Script, _version));
            if (mode == ScrollMode.Line) H.Apply(EngineCommand.SetMode(ScrollMode.Line));
        }

        public List<string> Script { get; private set; }

        public EngineHarness H { get; }

        public ReadingView View => ReadingView.Of(H.Geometry, H.Pos, H.R);

        /// <summary>Put line <paramref name="line"/> on the reading line, <paramref name="fraction"/> of the way down it.</summary>
        public void Show(int line, double fraction = 0.3) =>
            H.Apply(EngineCommand.SeekToOffset(H.Geometry.LineTops[line] + (fraction * Lh) - H.R));

        public void Edit(Action<List<string>> edit)
        {
            var next = Script.ToList();
            edit(next);
            Script = next;
            H.SetLayout(ScriptLayout.Build(Script, ++_version));
        }

        public int LineOf(int paragraph, int lineInParagraph = 0) =>
            H.Geometry.FindLineForAnchor(new DocumentAnchor(paragraph, lineInParagraph * ScriptLayout.CharsPerLine));
    }

    private static void AssertUnmoved(ReadingView before, ReadingView after) =>
        Assert.True(before.SameTextAs(after), $"reading line moved: was '{before.LineText}' at {before.Fraction}, now '{after.LineText}' at {after.Fraction}");

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void InsertingParagraphsAbove_DoesNotMoveTheReadingLine(int count)
    {
        var live = new Live();
        live.Show(live.LineOf(30, 1));
        var before = live.View;
        live.Edit(s => s.InsertRange(10, Enumerable.Range(0, count).Select(i => ScriptLayout.Paragraph(900 + i, 2))));
        AssertUnmoved(before, live.View);
        Assert.Equal(30 + count, live.View.Paragraph);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void DeletingParagraphsAbove_DoesNotMoveTheReadingLine(int count)
    {
        var live = new Live();
        live.Show(live.LineOf(30, 2));
        var before = live.View;
        live.Edit(s => s.RemoveRange(5, count));
        AssertUnmoved(before, live.View);
    }

    [Theory]
    [InlineData(4)]   // the paragraph above gains wrapped lines
    [InlineData(1)]   // ... or loses them
    public void EditingTheParagraphAbove_SoItReflows_DoesNotMoveTheReadingLine(int lines)
    {
        var live = new Live();
        live.Show(live.LineOf(30, 0));
        var before = live.View;
        live.Edit(s => s[29] = ScriptLayout.Paragraph(29, lines) + " typed");
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void TypingInTheAnchorParagraph_AfterTheAnchor_DoesNotMoveTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(31, 1));                       // paragraph 31 has 4 lines
        var before = live.View;
        live.Edit(s => s[31] = s[31][..100] + "typed " + s[31][100..]);   // on line 2, after the anchor (line 1 starts at 40)
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void TypingInTheAnchorParagraph_BeforeTheAnchor_KeepsTheAnchorCharacterOnTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(31, 2), 0.4);
        var anchorChar = live.Script[31][80..85];             // the first characters of the line on the reading line
        live.Edit(s => s[31] = s[31][..10] + "TYPED " + s[31][10..]);
        var view = live.View;
        Assert.Equal(31, view.Paragraph);
        Assert.Contains(anchorChar, view.LineText + live.Script[31]); // still in this paragraph
        var y = live.H.Pos + live.H.R;
        var line = live.H.Geometry.FindLineAt(y);
        var start = live.H.Geometry.LineCharOffsets[line];
        Assert.InRange(86, start, start + ScriptLayout.CharsPerLine - 1); // char 80 moved to 86: it is on the reading line
        Assert.Equal(0.4, (y - live.H.Geometry.LineTops[line]) / Lh, 6);
    }

    [Fact]
    public void DeletingTheTextOnTheReadingLine_BringsUpTheTextAfterIt()
    {
        var live = new Live();
        live.Show(live.LineOf(30, 0), 0.25);
        var next = live.Script[31];
        live.Edit(s => s.RemoveAt(30));
        Assert.Equal(next[..ScriptLayout.CharsPerLine], live.View.LineText);
        Assert.Equal(0.25, live.View.Fraction, 6);
    }

    [Fact]
    public void PastingALargeBlockAbove_DoesNotMoveTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(40, 1));
        var before = live.View;
        live.Edit(s => s.InsertRange(3, ScriptLayout.Script(120).Select(t => "pasted " + t)));
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void UndoAndRedo_DoNotMoveTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(30, 1));
        var before = live.View;
        var original = live.Script.ToList();
        live.Edit(s => { s.RemoveRange(12, 3); s[20] += " more words here to reflow it"; });
        AssertUnmoved(before, live.View);
        live.Edit(s => { s.Clear(); s.AddRange(original); });                // undo
        AssertUnmoved(before, live.View);
        Assert.Equal(30, live.View.Paragraph);
        live.Edit(s => { s.RemoveRange(12, 3); s[20] += " more words here to reflow it"; }); // redo
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void AddingAndRemovingASectionMarkerAbove_DoesNotMoveTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(30, 1));
        var before = live.View;
        live.Edit(s => s.Insert(25, "## New part"));
        AssertUnmoved(before, live.View);
        Assert.Single(live.H.Geometry.Sections);
        live.Edit(s => s.RemoveAt(25));
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void EditsBelowTheReadingLine_DoNotMoveIt()
    {
        var live = new Live();
        live.Show(live.LineOf(20, 1));
        var before = live.View;
        live.Edit(s => { s.RemoveRange(40, 6); s.Insert(30, ScriptLayout.Paragraph(777, 3)); s[21] = "short"; });
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void EditsAboveAndBelowInOneStep_DoNotMoveTheReadingLine()
    {
        var live = new Live();
        live.Show(live.LineOf(30, 0));
        var before = live.View;
        live.Edit(s => { s[2] += " plus a lot more text that wraps onto further lines of the paragraph"; s.Insert(50, "late insert"); });
        AssertUnmoved(before, live.View);
    }

    [Fact]
    public void ReadingLineInTheGapBetweenParagraphs_StaysInTheSameGap()
    {
        var live = new Live();
        var last = live.LineOf(31) - 1;                       // last line of paragraph 30
        live.H.Apply(EngineCommand.SeekToOffset(live.H.Geometry.LineTops[last] + Lh + 10 - live.H.R)); // 10 DIP into the gap
        var before = live.View;
        live.Edit(s => s.Insert(4, ScriptLayout.Paragraph(901, 3)));
        AssertUnmoved(before, live.View);
        Assert.Equal(10, before.Outside, 6);
    }

    [Fact]
    public void WhilePlaying_AnEditIsInvisible_AndPlayingContinues()
    {
        var live = new Live();
        live.Show(live.LineOf(25, 0));
        live.H.Apply(EngineCommand.SetSpeed(4));
        live.H.Play();
        live.H.Run(1.37);
        var before = live.View;
        var speed = live.H.Status.EffectiveSpeed;
        live.Edit(s => s.InsertRange(8, ScriptLayout.Script(5).Select(t => "new " + t)));
        AssertUnmoved(before, live.View);
        var p = live.H.Pos;
        live.H.Run(0.5);
        Assert.Equal(TransportState.Playing, live.H.Status.Transport);
        Assert.Equal(speed, live.H.Status.EffectiveSpeed);
        Assert.True(live.H.Pos > p);
    }

    [Fact]
    public void WhilePaused_AnEditIsInvisible()
    {
        var live = new Live();
        live.Show(live.LineOf(25, 1));
        var before = live.View;
        live.Edit(s => s.RemoveRange(1, 4));
        AssertUnmoved(before, live.View);
        Assert.Equal(TransportState.Paused, live.H.Status.Transport);
    }

    [Fact]
    public void LineScrolling_KeepsTheSameLineCentredThroughAnEdit()
    {
        var live = new Live(mode: ScrollMode.Line);
        live.H.Apply(EngineCommand.StepLines(30));
        live.H.Run(0.5);
        var line = live.H.Status.LineIndex;
        var before = live.View;
        Assert.Equal(0.5, before.Fraction, 6);
        live.Edit(s => s.InsertRange(2, ScriptLayout.Script(3).Select(t => "new " + t)));
        AssertUnmoved(before, live.View);
        Assert.NotEqual(line, live.H.Status.LineIndex);         // a different line index, the same text
    }

    [Fact]
    public void LineScrollingWhilePlaying_AnEditMidStep_LetsTheStepFinishFromWhereItIs()
    {
        var live = new Live(mode: ScrollMode.Line);
        live.H.Apply(EngineCommand.StepLines(20));
        live.H.Run(0.5);
        var from = live.H.Status.LineIndex;
        var targetText = ReadingView.Of(live.H.Geometry, live.H.Geometry.LineCentre(from + 2) - live.H.R, live.H.R);
        live.H.Apply(EngineCommand.StepLines(2));
        live.H.Run(0.08);                                       // mid animation (220 ms)
        var shown = live.View;
        Assert.NotEqual(targetText.LineText, shown.LineText);
        live.Edit(s => s.Insert(3, ScriptLayout.Paragraph(905, 2)));
        AssertUnmoved(shown, live.View);                        // no jump to the step's target
        live.H.Run(0.5);
        Assert.Equal(0.5, live.View.Fraction, 6);
        Assert.Equal(targetText.LineText, live.View.LineText);  // and it still arrives where it was going
    }

    [Fact]
    public void LineScrollingPlay_StepTimerIsNotResetByAnEdit()
    {
        var live = new Live(mode: ScrollMode.Line);
        live.H.Apply(EngineCommand.SetSpeed(5));
        live.H.Play();
        var interval = live.H.Status.StepIntervalSeconds;
        live.H.Run(interval * 0.9);
        var line = live.H.Status.LineIndex;
        live.Edit(s => s.Insert(60 - 1, "an edit near the end"));
        live.H.Run(interval * 0.2);                             // the step falls due now, not a full interval later
        live.H.Run(0.3);
        Assert.Equal(line + 1, live.H.Status.LineIndex);
    }

    [Fact]
    public void AtEnd_StaysAtEndThroughAnEdit()
    {
        var live = new Live();
        live.H.Apply(EngineCommand.JumpToEnd());
        live.Edit(s => s.InsertRange(5, ScriptLayout.Script(4)));
        Assert.Equal(TransportState.AtEnd, live.H.Status.Transport);
        Assert.Equal(live.H.Pmax, live.H.Pos, 9);
    }
}
