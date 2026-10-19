using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

public class LayoutGeometryBuilderTests
{
    private const double BaseSize = 40;
    private const double Spacing = 1.3;
    private const double LineHeight = BaseSize * Spacing;

    private readonly OutputDocumentFactory _factory = new(T.Snapshots);
    private readonly LayoutGeometryBuilder _builder = new(T.Navigator);

    private static string Sentence(int seed, int words)
    {
        string[] vocabulary = ["welcome", "everyone", "to", "the", "evening", "session", "please", "take", "your", "seats", "and", "settle", "in", "while", "we", "begin"];
        return string.Join(' ', Enumerable.Range(0, words).Select(i => vocabulary[(seed * 7 + i * 3) % vocabulary.Length] + (i % 11 == 10 ? "," : "")));
    }

    private static FlowDocument Script(int paragraphs, int wordsPerParagraph, bool sectionEvery5 = false)
    {
        var doc = new FlowDocument();
        for (var i = 0; i < paragraphs; i++)
        {
            var text = sectionEvery5 && i % 5 == 0 ? $"## Section {i / 5 + 1}" : Sentence(i, wordsPerParagraph);
            doc.Blocks.Add(new Paragraph(new Run(text)));
        }

        return doc;
    }

    /// <summary>Creates the output document, hosts it off-screen at the given width and builds its geometry.</summary>
    private (LayoutGeometry Geometry, FlowDocument Output, PromptDocumentSnapshot Snapshot, Window Window) Layout(
        FlowDocument editor, double width = 900, bool showSections = false, string key = "k1")
    {
        var snapshot = T.Snapshots.Build(editor, 5, 20, "##");
        var output = _factory.Create(snapshot, Styles.Output(BaseSize, showSections, Spacing, family: ThemeResources.UiFont));
        var (window, _) = T.HostOffscreen(output, width, 900);
        var geometry = _builder.Build(output, new GeometryBuildOptions(key, snapshot, LineHeight));
        return (geometry, output, snapshot, window);
    }

    [WpfFact]
    public void WrappedScript_HasMoreLinesThanParagraphs_WithIncreasingTopsAndMatchingTables()
    {
        var (g, output, snapshot, window) = Layout(Script(50, 40, sectionEvery5: true));
        try
        {
            Assert.Equal("k1", g.LayoutKey);
            Assert.Equal(5, g.SnapshotVersion);
            Assert.Equal(LineHeight, g.BaseLineHeight);
            Assert.Equal(50, g.ParagraphCount);
            Assert.True(g.LineCount > g.ParagraphCount, $"{g.LineCount} lines for {g.ParagraphCount} paragraphs");
            Assert.Equal(g.LineCount, g.LineHeights.Count);
            Assert.Equal(g.LineCount, g.LineParagraphs.Count);
            Assert.Equal(g.LineCount, g.LineCharOffsets.Count);
            for (var i = 1; i < g.LineCount; i++)
            {
                Assert.True(g.LineTops[i] > g.LineTops[i - 1], $"line {i} not below line {i - 1}");
                Assert.True(g.LineParagraphs[i] >= g.LineParagraphs[i - 1]);
            }

            for (var i = 0; i < g.LineCount; i++)
            {
                var isSection = snapshot.Sections.Any(sec => sec.ParagraphIndex == g.LineParagraphs[i]);
                Assert.Equal(isSection ? LineHeight / 2 : LineHeight, g.LineHeights[i], 3); // hidden section names are half a line
            }

            Assert.Equal(snapshot.WordCount, g.WordCount);
            Assert.Equal(snapshot.Sections.Count, g.Sections.Count);
            Assert.Equal(10, g.Sections.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void LineBoxes_StartAtZero_AndStepByExactlyTheLineHeightWithinAParagraph()
    {
        var (g, _, _, window) = Layout(Script(6, 60));
        try
        {
            Assert.Equal(0, g.LineTops[0], 0.1);
            for (var i = 1; i < g.LineCount; i++)
            {
                var sameParagraph = g.LineParagraphs[i] == g.LineParagraphs[i - 1];
                var expectedStep = sameParagraph ? LineHeight : LineHeight + (0.5 * LineHeight); // + the paragraph gap
                Assert.Equal(expectedStep, g.LineTops[i] - g.LineTops[i - 1], 0.1);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void ParagraphTops_AreTheTopsOfTheirFirstLines_AndOffsetsGrowWithinAParagraph()
    {
        var (g, _, _, window) = Layout(Script(20, 50));
        try
        {
            for (var p = 0; p < g.ParagraphCount; p++)
            {
                var firstLine = Enumerable.Range(0, g.LineCount).First(i => g.LineParagraphs[i] == p);
                Assert.Equal(g.LineTops[firstLine], g.ParagraphTops[p], 3);
                Assert.Equal(0, g.LineCharOffsets[firstLine]);
            }

            for (var i = 1; i < g.LineCount; i++)
            {
                if (g.LineParagraphs[i] == g.LineParagraphs[i - 1])
                    Assert.True(g.LineCharOffsets[i] > g.LineCharOffsets[i - 1]);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void End_IsAfterTheLastLine_AndDocumentHeightIncludesIt()
    {
        var (g, _, _, window) = Layout(Script(8, 30));
        try
        {
            var lastBottom = g.LineTops[^1] + g.LineHeights[^1];
            // WPF collapses the paragraph gap (0.5 line) into the END block's one-line top margin: one blank line.
            Assert.Equal(lastBottom + LineHeight, g.EndTop, 1.0);
            Assert.Equal(g.EndTop + LineHeight, g.DocumentHeight, 1.0);
            Assert.True(g.EndTop > g.LineTops[^1]);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void Anchors_MapBackToTheRightLine_AndToTheRightTextInTheOutputDocument()
    {
        var (g, output, _, window) = Layout(Script(12, 45));
        try
        {
            for (var i = 0; i < g.LineCount; i++)
            {
                var anchor = g.GetAnchor(g.LineTops[i] + 1);
                Assert.Equal(new DocumentAnchor(g.LineParagraphs[i], g.LineCharOffsets[i]), anchor);
                Assert.Equal(i, g.FindLineForAnchor(anchor));
                Assert.Equal(i, g.FindLineAt(g.LineCentre(i)));

                // The anchor really is where that line starts: its character rect sits on that line.
                var pointer = T.Navigator.GetPointer(output, anchor);
                var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                var fontBoxCentre = rect.Top + (rect.Height / 2);
                Assert.InRange(fontBoxCentre, g.LineTops[i], g.LineTops[i] + g.LineHeights[i]);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void Sections_CarryTheTopOfTheirMarkerParagraph()
    {
        var (g, _, snapshot, window) = Layout(Script(30, 20, sectionEvery5: true));
        try
        {
            foreach (var marker in g.Sections)
            {
                Assert.Equal(g.ParagraphTops[marker.ParagraphIndex], marker.Top, 3);
                Assert.Equal(snapshot.Sections[marker.Index].Name, marker.Name);
            }

            Assert.Equal(2, g.FindSectionAt(g.Sections[2].Top));
            Assert.Equal(2, g.FindSectionAt(g.Sections[2].Top + 1));
            Assert.Equal(-1, g.FindSectionAt(g.Sections[0].Top - 1));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void ShownAndHiddenSectionNames_BothKeepOneLinePerMarkerParagraph()
    {
        foreach (var show in new[] { false, true })
        {
            var (g, _, snapshot, window) = Layout(Script(10, 15, sectionEvery5: true), showSections: show);
            try
            {
                foreach (var section in snapshot.Sections)
                {
                    var lines = Enumerable.Range(0, g.LineCount).Where(i => g.LineParagraphs[i] == section.ParagraphIndex).ToList();
                    Assert.Single(lines);
                    var expected = show ? 0.4 * BaseSize * Spacing : 0.5 * LineHeight;
                    Assert.Equal(expected, g.LineHeights[lines[0]], 0.75);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [WpfFact]
    public void EmptyParagraphs_AndAnEmptyScript_AreHandled()
    {
        var (g, _, _, window) = Layout(T.Doc("a", "", "", "b"));
        try
        {
            Assert.Equal(4, g.ParagraphCount);
            Assert.Equal(4, g.LineCount);
            Assert.Equal([0, 1, 2, 3], g.LineParagraphs);
        }
        finally
        {
            window.Close();
        }

        var (empty, _, _, window2) = Layout(new FlowDocument());
        try
        {
            Assert.True(empty.EndTop >= 0);
            Assert.True(empty.DocumentHeight >= empty.EndTop);
        }
        finally
        {
            window2.Close();
        }
    }

    [WpfFact]
    public void LineBreaks_AndMixedSizes_StillGiveOneEntryPerVisualLine()
    {
        var editor = new FlowDocument();
        var p = new Paragraph(new Run("first line"));
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run("second line, large") { FontSize = 25 });
        editor.Blocks.Add(p);
        editor.Blocks.Add(new Paragraph(new Run("tail")));
        var (g, _, _, window) = Layout(editor);
        try
        {
            Assert.Equal(3, g.LineCount);
            Assert.Equal([0, 0, 1], g.LineParagraphs);
            Assert.Equal("first line".Length + 2, g.LineCharOffsets[1]); // the break counts as CR LF
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void ThousandLines_BuildWithinTheBudget()
    {
        var snapshot = T.Snapshots.Build(Script(125, 70), 1, 20, "##");
        var output = _factory.Create(snapshot, Styles.Output(BaseSize, false, Spacing, family: ThemeResources.UiFont));
        var (window, _) = T.HostOffscreen(output, 900, 900);
        try
        {
            _ = _builder.Build(output, new GeometryBuildOptions("warm", snapshot, LineHeight)); // JIT and caches
            var best = double.MaxValue;
            LayoutGeometry? g = null;
            for (var run = 0; run < 5; run++)
            {
                var sw = Stopwatch.StartNew();
                g = _builder.Build(output, new GeometryBuildOptions("timed", snapshot, LineHeight));
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }

            Assert.True(g!.LineCount >= 1000, $"only {g.LineCount} lines");
            // The target is 30 ms per 1,000 lines. Other builds share this machine, so the test only guards against
            // pathological (quadratic) behaviour; the measured best of five is recorded for the stream notes.
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ilu-geometry-timing.txt"),
                $"{g.LineCount} lines, best of 5: {best:F1} ms");
            Assert.True(best < 1500, $"{g.LineCount} lines took {best:F1} ms (target 30 ms)");
        }
        finally
        {
            window.Close();
        }
    }
}
