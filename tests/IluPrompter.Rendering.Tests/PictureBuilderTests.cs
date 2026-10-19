using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering.Tests;

/// <summary>
/// The layout thread's picture builder with the real snapshot builder, factory and geometry builder: a script edit is
/// patched into the live document (only changed paragraphs built, unchanged drawings kept), and the result must be exactly
/// what a fresh full layout of the edited script gives, tables and pixels.
/// </summary>
public class PictureBuilderTests
{
    private static readonly DocumentNavigator Navigator = new();
    private static readonly SnapshotBuilder Snapshots = new(Navigator, new SectionParser());
    private static readonly OutputGeometry Output = OutputGeometry.Default1080;

    private static PictureBuilder NewBuilder() => new(new OutputDocumentFactory(Snapshots), new LayoutGeometryBuilder(Navigator));

    private static FlowDocument Script()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("## Welcome")));
        doc.Blocks.Add(new Paragraph(new Run("Good evening, everyone, and welcome to the annual meeting of the Riverside Community Garden Trust.")));
        var mixed = new Paragraph(new Run("Please take a seat, "));
        mixed.Inlines.Add(new Bold(new Run("find a cup of tea")));
        mixed.Inlines.Add(new Run(" if you can, and let us begin with a look back over the year."));
        doc.Blocks.Add(mixed);
        doc.Blocks.Add(new Paragraph());
        doc.Blocks.Add(new Paragraph(new Run("## Money")));
        for (var i = 0; i < 14; i++)
        {
            doc.Blocks.Add(new Paragraph(new Run($"Paragraph {i}: then we will talk about the money. There will be one chart, and it has a very simple shape indeed.")));
        }

        doc.Blocks.Add(new Paragraph(new Run("Thank you.")));
        return doc;
    }

    private static PictureInputs Inputs(FlowDocument editor, long version, PrompterSettings? settings = null) =>
        new(Snapshots.Build(editor, version, 20, "##"), settings ?? PrompterSettings.Default, Output);

    private static void AssertSameLayout(LayoutGeometry expected, LayoutGeometry actual)
    {
        Assert.Equal(expected.LineCount, actual.LineCount);
        Assert.Equal(expected.LineParagraphs, actual.LineParagraphs);
        Assert.Equal(expected.LineCharOffsets, actual.LineCharOffsets);
        for (var i = 0; i < expected.LineCount; i++) Assert.Equal(expected.LineTops[i], actual.LineTops[i], 3);
        Assert.Equal(expected.ParagraphCount, actual.ParagraphCount);
        for (var i = 0; i < expected.ParagraphCount; i++) Assert.Equal(expected.ParagraphTops[i], actual.ParagraphTops[i], 3);
        Assert.Equal(expected.ParagraphTexts, actual.ParagraphTexts);
        Assert.Equal(expected.Sections.Select(s => (s.Name, s.ParagraphIndex)), actual.Sections.Select(s => (s.Name, s.ParagraphIndex)));
        Assert.Equal(expected.EndTop, actual.EndTop, 3);
        Assert.Equal(expected.DocumentHeight, actual.DocumentHeight, 3);
    }

    /// <summary>The picture drawn the way a surface draws it, at 1:1.</summary>
    private static byte[] Pixels(PromptPicture picture)
    {
        var presenter = new PicturePresenter();
        presenter.SetPicture(picture, 0, 100_000);
        var height = (int)Math.Ceiling(picture.Geometry.DocumentHeight) + 10;
        var width = (int)Math.Ceiling(picture.Area.Width);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            foreach (var block in picture.Blocks)
            {
                dc.PushTransform(new TranslateTransform(0, block.Top));
                dc.DrawDrawing(block.Drawing);
                dc.Pop();
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    public static TheoryData<string> Edits => new()
    {
        "insert-above", "insert-many", "delete-one", "delete-range", "reflow", "bold", "add-marker", "remove-marker",
        "first", "last", "two-places", "split", "join", "blank-line",
    };

    private static void Apply(FlowDocument doc, string edit)
    {
        var p = Navigator.GetParagraphs(doc);
        switch (edit)
        {
            case "insert-above": doc.Blocks.InsertBefore(p[8], new Paragraph(new Run("A new paragraph typed while live, long enough to wrap onto a second line."))); break;
            case "insert-many":
                for (var i = 0; i < 5; i++) doc.Blocks.InsertAfter(p[6], new Paragraph(new Run($"Pasted {i}: more and more words to read out loud.")));
                break;
            case "delete-one": doc.Blocks.Remove(p[9]); break;
            case "delete-range":
                for (var i = 6; i < 12; i++) doc.Blocks.Remove(p[i]);
                break;
            case "reflow": ((Run)p[7].Inlines.FirstInline).Text += " and a great many more words, enough to wrap onto a further line or two of the output."; break;
            case "bold": new TextRange(p[10].ContentStart.GetPositionAtOffset(3), p[10].ContentStart.GetPositionAtOffset(12)).ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold); break;
            case "add-marker": doc.Blocks.InsertBefore(p[12], new Paragraph(new Run("## Questions"))); break;
            case "remove-marker": doc.Blocks.Remove(p[4]); break;
            case "first": ((Run)p[1].Inlines.FirstInline).Text = "Good evening."; break;
            case "last": ((Run)p[^1].Inlines.FirstInline).Text = "Thank you, and good night."; break;
            case "two-places":
                ((Run)p[2].Inlines.LastInline).Text += " Really.";
                doc.Blocks.InsertAfter(p[15], new Paragraph(new Run("Late addition.")));
                break;
            case "split":
            {
                var run = (Run)p[9].Inlines.FirstInline;
                var text = run.Text;
                var cut = Math.Min(30, text.Length / 2);
                run.Text = text[..cut];
                doc.Blocks.InsertAfter(p[9], new Paragraph(new Run(text[cut..])));
                break;
            }

            case "join":
                ((Run)p[8].Inlines.FirstInline).Text += ((Run)p[9].Inlines.FirstInline).Text;
                doc.Blocks.Remove(p[9]);
                break;
            case "blank-line": doc.Blocks.InsertAfter(p[10], new Paragraph()); break;
        }
    }

    [WpfTheory]
    [MemberData(nameof(Edits))]
    public void PatchedEdit_IsExactlyAFreshLayout_AndKeepsUnchangedDrawings(string edit)
    {
        var editor = Script();
        var builder = NewBuilder();
        var first = builder.Build(Inputs(editor, 1), null);
        Assert.Equal(-1, first.PatchedParagraphs);

        Apply(editor, edit);
        var inputs = Inputs(editor, 2);
        var patched = builder.Build(inputs, first);
        var fresh = NewBuilder().Build(inputs, null);

        Assert.True(patched.PatchedParagraphs >= 0, "the edit was laid out from scratch instead of patched");
        AssertSameLayout(fresh.Geometry, patched.Geometry);
        Assert.Equal(Pixels(fresh), Pixels(patched));

        // Paragraphs the edit did not touch keep their frozen drawing (their visuals stay on the render thread).
        var kept = patched.Blocks.Count(b => first.Blocks.Any(o => ReferenceEquals(o.Drawing, b.Drawing)));
        Assert.True(kept >= patched.Blocks.Count - 8, $"only {kept} of {patched.Blocks.Count} drawings kept");
        Assert.Contains(patched.Blocks, b => b.Paragraph == -1 && first.Blocks.Any(o => ReferenceEquals(o.Drawing, b.Drawing))); // END
    }

    [WpfFact]
    public void ManyEditsInARow_StayExactlyAFreshLayout()
    {
        var editor = Script();
        var builder = NewBuilder();
        var picture = builder.Build(Inputs(editor, 1), null);
        var version = 1;
        foreach (var edit in new[] { "insert-above", "bold", "delete-one", "add-marker", "reflow", "split", "join", "remove-marker", "two-places" })
        {
            Apply(editor, edit);
            picture = builder.Build(Inputs(editor, ++version), picture);
        }

        AssertSameLayout(NewBuilder().Build(Inputs(editor, version), null).Geometry, picture.Geometry);
    }

    [WpfFact]
    public void StyleChange_LaysOutTheWholeScriptAgain()
    {
        var editor = Script();
        var builder = NewBuilder();
        var first = builder.Build(Inputs(editor, 1), null);
        var bigger = PrompterSettings.Default with { Text = PrompterSettings.Default.Text with { LinesVisible = 6 } };
        var second = builder.Build(Inputs(editor, 1, bigger), first);
        Assert.Equal(-1, second.PatchedParagraphs);
        AssertSameLayout(NewBuilder().Build(Inputs(editor, 1, bigger), null).Geometry, second.Geometry);
    }

    [WpfFact]
    public void TwentyPageSample_SplitsIntoItsParagraphs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "IluPrompter.sln"))) dir = dir.Parent;
        var file = new ScriptFileService().Load(Path.Combine(dir.FullName, "samples", "sample-20-pages.txt"));
        var editor = new FlowDocument();
        Snapshots.LoadInto(editor, file.DocumentXaml);
        var snapshot = Snapshots.Build(editor, 1, 20, "##");
        Assert.True(XamlParagraphs.TrySplit(snapshot.FlowDocumentXaml, out var root, out var paragraphs));
        Assert.StartsWith("<Section", root, StringComparison.Ordinal);
        Assert.Equal(snapshot.ParagraphCount, paragraphs.Length);
    }

    [Theory]
    [InlineData("<Section xmlns=\"x\"><Paragraph>a</Paragraph><!-- c --></Section>")]
    [InlineData("<Section xmlns=\"x\">text<Paragraph>a</Paragraph></Section>")]
    [InlineData("<Section xmlns=\"x\"><List><ListItem/></List></Section>")]
    [InlineData("<Section xmlns=\"x\"><Paragraph>a</Section>")]
    [InlineData("")]
    public void UnexpectedXaml_IsNotSplit(string xaml) => Assert.False(XamlParagraphs.TrySplit(xaml, out _, out _));

    [Fact]
    public void Split_HonoursQuotesAndNesting()
    {
        const string xaml = "<Section xmlns=\"x\" a='>'><Paragraph Tag=\"a>b\"><Run>1 &gt; 0</Run><Span><Run/></Span></Paragraph><Paragraph /></Section>";
        Assert.True(XamlParagraphs.TrySplit(xaml, out var root, out var paragraphs));
        Assert.Equal("<Section xmlns=\"x\" a='>'>", root);
        Assert.Equal(["<Paragraph Tag=\"a>b\"><Run>1 &gt; 0</Run><Span><Run/></Span></Paragraph>", "<Paragraph />"], paragraphs);
    }
}
