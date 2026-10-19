using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Theme;
using Source = IluPrompter.Documents.LayoutGeometryBuilder.LineSource;

namespace IluPrompter.Documents.Tests;

/// <summary>
/// Integration: the visual-tree fast path of <see cref="LayoutGeometryBuilder"/> must give exactly the tables of the
/// line walk (line-box tops, heights, paragraphs, character offsets), and the fast path must actually be taken for the
/// documents the output factory makes (so a WPF change that disables it shows up here, not as a slow show).
/// </summary>
public class LayoutGeometrySourceTests
{
    private readonly OutputDocumentFactory _factory = new(T.Snapshots);
    private readonly LayoutGeometryBuilder _builder = new(T.Navigator);

    private static OutputTextStyle Style(double size, bool showSections = false, TextAlignment alignment = TextAlignment.Left) =>
        Styles.Output(size, showSections, 1.3, family: ThemeResources.UiFont) with { Alignment = alignment };

    /// <summary>Hosts like the prompter surface: a detached read-only RichTextBox at the text width, unconstrained in
    /// height (every line laid out), measured and arranged explicitly.</summary>
    internal static RichTextBox HostLikeTheSurface(FlowDocument document, double width)
    {
        var host = new RichTextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Width = width,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Document = document,
        };
        host.Measure(new Size(width, double.PositiveInfinity));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        host.UpdateLayout();
        return host;
    }

    private (LayoutGeometry Walk, LayoutGeometry? Fast, Source Used) BuildBoth(FlowDocument editor, OutputTextStyle style, double width)
    {
        var snapshot = T.Snapshots.Build(editor, 3, 20, "##");
        var output = _factory.Create(snapshot, style);
        HostLikeTheSurface(output, width);
        var options = new GeometryBuildOptions("k", snapshot, style.BaseFontSize * style.LineSpacing);
        var walk = _builder.Build(output, options, Source.LineWalk, out _)!;
        var fast = _builder.Build(output, options, Source.VisualTree, out _);
        _builder.Build(output, options, Source.Auto, out var used);
        return (walk, fast, used);
    }

    private static void AssertSame(LayoutGeometry walk, LayoutGeometry? fast)
    {
        Assert.NotNull(fast);
        Assert.Equal(walk.LineCount, fast!.LineCount);
        for (var i = 0; i < walk.LineCount; i++)
        {
            Assert.True(Math.Abs(walk.LineTops[i] - fast.LineTops[i]) < 0.05, $"line {i}: walk {walk.LineTops[i]} fast {fast.LineTops[i]}");
            Assert.Equal(walk.LineHeights[i], fast.LineHeights[i], 6);
            Assert.Equal(walk.LineParagraphs[i], fast.LineParagraphs[i]);
            Assert.True(walk.LineCharOffsets[i] == fast.LineCharOffsets[i], $"line {i}: walk offset {walk.LineCharOffsets[i]} fast {fast.LineCharOffsets[i]}");
        }

        Assert.Equal(walk.ParagraphTops.Count, fast.ParagraphTops.Count);
        for (var p = 0; p < walk.ParagraphTops.Count; p++) Assert.Equal(walk.ParagraphTops[p], fast.ParagraphTops[p], 1);
        Assert.Equal(walk.EndTop, fast.EndTop, 6);
        Assert.Equal(walk.DocumentHeight, fast.DocumentHeight, 6);
        Assert.Equal(walk.Sections.Count, fast.Sections.Count);
        for (var s = 0; s < walk.Sections.Count; s++) Assert.Equal(walk.Sections[s].Top, fast.Sections[s].Top, 1);
    }

    private static FlowDocument Mixed()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("## Opening")));
        var p = new Paragraph();
        p.Inlines.Add(new Run("Normal words that run on for a while, then "));
        p.Inlines.Add(new Bold(new Run("bold words in the middle")));
        p.Inlines.Add(new Run(" and "));
        p.Inlines.Add(new Run("a large run") { FontSize = 25 });
        p.Inlines.Add(new Run(", a small one ") { FontSize = 16 });
        p.Inlines.Add(new Underline(new Run("underlined text that wraps across the line end for sure")));
        p.Inlines.Add(new Run(" highlighted words here ") { Background = Styles.Solid("highlight-yellow") });
        p.Inlines.Add(new Run("and the same words again and the same words again and the same words again."));
        doc.Blocks.Add(p);
        doc.Blocks.Add(new Paragraph());
        var breaks = new Paragraph(new Run("before the break, which is quite a long line of text"));
        breaks.Inlines.Add(new LineBreak());
        breaks.Inlines.Add(new Run("after the break"));
        doc.Blocks.Add(breaks);
        doc.Blocks.Add(new Paragraph(new Run("## Second part")));
        doc.Blocks.Add(new Paragraph(new Run("repeat repeat repeat repeat repeat repeat repeat repeat repeat repeat repeat repeat repeat repeat")));
        return doc;
    }

    [WpfTheory]
    [InlineData(40, false, TextAlignment.Left, 900)]
    [InlineData(91.53846153846153, false, TextAlignment.Left, 1792)]
    [InlineData(60, true, TextAlignment.Center, 700)]
    [InlineData(25, true, TextAlignment.Left, 400)]
    public void FastPath_EqualsTheLineWalk_ForMixedFormatting(double size, bool showSections, TextAlignment alignment, double width)
    {
        var (walk, fast, used) = BuildBoth(Mixed(), Style(size, showSections, alignment), width);
        AssertSame(walk, fast);
        Assert.Equal(Source.VisualTree, used);
    }

    [WpfFact]
    public void FastPath_EqualsTheLineWalk_ForTheTwentyPageSample()
    {
        var file = new ScriptFileService().Load(Path.Combine(T.RepoRoot(), "samples", "sample-20-pages.txt"));
        var editor = T.Load(file.DocumentXaml);
        var (walk, fast, used) = BuildBoth(editor, Style(91.53846153846153), 1792);
        Assert.True(walk.LineCount > 1000, $"{walk.LineCount} lines");
        AssertSame(walk, fast);
        Assert.Equal(Source.VisualTree, used);
    }

    [WpfFact]
    public void LineWalk_IsUsed_WhenTheDocumentIsNotHosted()
    {
        var snapshot = T.Snapshots.Build(T.Doc("one", "two"), 1, 20, "##");
        var output = _factory.Create(snapshot, Style(40));
        var geometry = _builder.Build(output, new GeometryBuildOptions("k", snapshot, 52), Source.Auto, out var used);
        Assert.Equal(Source.LineWalk, used);
        Assert.NotNull(geometry);
        Assert.Null(_builder.Build(output, new GeometryBuildOptions("k", snapshot, 52), Source.VisualTree, out _));
    }

    [WpfFact]
    public void FastPath_IsFarFasterThanTheWalk_OnTheTwentyPageSample()
    {
        var file = new ScriptFileService().Load(Path.Combine(T.RepoRoot(), "samples", "sample-20-pages.txt"));
        var snapshot = T.Snapshots.Build(T.Load(file.DocumentXaml), 1, 20, "##");
        var style = Style(91.53846153846153);
        var output = _factory.Create(snapshot, style);
        HostLikeTheSurface(output, 1792);
        {
            var options = new GeometryBuildOptions("k", snapshot, style.BaseFontSize * style.LineSpacing);
            double Best(Source source)
            {
                _builder.Build(output, options, source, out _);
                var best = double.MaxValue;
                for (var i = 0; i < 3; i++)
                {
                    var sw = Stopwatch.StartNew();
                    _builder.Build(output, options, source, out _);
                    best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
                }

                return best;
            }

            var walk = Best(Source.LineWalk);
            var fast = Best(Source.VisualTree);
            var lines = _builder.Build(output, options).LineCount;
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "ilu-geometry-sources.txt"),
                $"{lines} lines: line walk {walk:F1} ms, visual tree {fast:F1} ms");
            Assert.True(fast < walk / 2, $"visual tree {fast:F1} ms vs line walk {walk:F1} ms");
        }
    }
}
