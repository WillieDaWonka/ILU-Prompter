using System.IO;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using IluPrompter.Core;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace IluPrompter.Documents.Tests;

public class DocxImportTests
{
    private readonly ScriptFileService _service = new();

    private static W.Run R(string text, Action<W.RunProperties>? format = null)
    {
        var run = new W.Run();
        if (format is not null)
        {
            var properties = new W.RunProperties();
            format(properties);
            run.Append(properties);
        }

        run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static W.Paragraph P(string? styleId, params OpenXmlElement[] runs)
    {
        var paragraph = new W.Paragraph();
        if (styleId is not null) paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId }));
        paragraph.Append(runs);
        return paragraph;
    }

    private static W.Style ParagraphStyle(string id, string name, string? basedOn = null, string? sizeHalfPoints = null, bool bold = false)
    {
        var style = new W.Style { Type = W.StyleValues.Paragraph, StyleId = id };
        style.Append(new W.StyleName { Val = name });
        if (basedOn is not null) style.Append(new W.BasedOn { Val = basedOn });
        var run = new W.StyleRunProperties();
        if (sizeHalfPoints is not null) run.Append(new W.FontSize { Val = sizeHalfPoints });
        if (bold) run.Append(new W.Bold());
        style.Append(run);
        return style;
    }

    /// <summary>Builds a .docx in memory with the SDK (a heading, formatted runs, a list, a table, an image-less drawing).</summary>
    private static string BuildDocx(Action<W.Body, W.Styles> fill, string defaultSizeHalfPoints = "22")
    {
        var path = T.TempFile(".docx");
        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);
            var styles = new W.Styles(new W.DocDefaults(new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(new W.FontSize { Val = defaultSizeHalfPoints }))));
            fill(body, styles);
            main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
            main.Document.Save();
        }

        return path;
    }

    private static string StandardDocx() => BuildDocx((body, styles) =>
    {
        styles.Append(ParagraphStyle("Title", "Title", sizeHalfPoints: "56", bold: true));
        styles.Append(ParagraphStyle("Heading1", "heading 1", sizeHalfPoints: "32", bold: true));
        styles.Append(ParagraphStyle("Heading2", "heading 2", basedOn: "Heading1"));
        styles.Append(ParagraphStyle("Custom", "Not A Heading", basedOn: "Heading1"));

        body.Append(P("Title", R("The Big Show")));
        body.Append(P("Heading1", R("Welcome"), R(" everyone")));
        body.Append(P(null,
            R("Normal words, "),
            R("bold", p => p.Append(new W.Bold())),
            R(" italic", p => p.Append(new W.Italic())),
            R(" underlined", p => p.Append(new W.Underline { Val = W.UnderlineValues.Single })),
            R(" struck", p => p.Append(new W.Strike())),
            R(" marked", p => p.Append(new W.Highlight { Val = W.HighlightColorValues.Yellow })),
            R(" red", p => p.Append(new W.Color { Val = "FF0000" })),
            R(" auto", p => p.Append(new W.Color { Val = "auto" })),
            R(" not bold", p => p.Append(new W.Bold { Val = false })),
            R(" huge", p => p.Append(new W.FontSize { Val = "48" })),
            R(" tiny", p => p.Append(new W.FontSize { Val = "16" }))));
        body.Append(P(null, R("")));
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 1 })),
            R("first bullet")));
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 0 })),
            R("not actually a bullet")));
        body.Append(new W.Table(
            new W.TableRow(
                new W.TableCell(P(null, R("cell A1"))),
                new W.TableCell(P(null, R("cell B1")))),
            new W.TableRow(
                new W.TableCell(P(null, R("cell A2"))),
                new W.TableCell(P("Heading2", R("Cell heading"))))));
        body.Append(new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
            R("centred line")));
        var withBreak = P(null, R("before"));
        withBreak.Append(new W.Run(new W.Break()));
        withBreak.Append(R("after"));
        body.Append(withBreak);
        var withImage = P(null, R("image caption"));
        withImage.Append(new W.Run(new W.Drawing()));
        body.Append(withImage);
        body.Append(P("Heading1")); // an empty heading is not a section
        body.Append(P("Custom", R("looks like a heading but is not")));
    });

    [WpfFact]
    public void Docx_HeadingsBecomeSections()
    {
        var file = _service.Load(StandardDocx());
        var doc = T.Load(file.DocumentXaml);
        var texts = T.Texts(doc);

        Assert.Equal("## The Big Show", texts[0]);
        Assert.Equal("## Welcome everyone", texts[1]);
        Assert.Contains("## Cell heading", texts);
        Assert.DoesNotContain(texts, t => t.StartsWith("##", StringComparison.Ordinal) && t.Contains("looks like", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.TrimEnd() == "##"); // an empty heading produces no marker
        var snapshot = T.Snapshots.Build(doc, 1, 20, "##");
        Assert.Equal(["The Big Show", "Welcome everyone", "Cell heading"], snapshot.Sections.Select(s => s.Name));
    }

    [WpfFact]
    public void Docx_KeepsBoldItalicUnderlineStrikeHighlightColourAndRelativeSize()
    {
        var doc = T.Load(_service.Load(StandardDocx()).DocumentXaml);

        Assert.Equal(FontWeights.Bold, T.RunWithText(doc, "bold").FontWeight);
        Assert.Equal(FontStyles.Italic, T.RunWithText(doc, " italic").FontStyle);
        Assert.Contains(T.RunWithText(doc, "underlined").TextDecorations, d => d.Location == TextDecorationLocation.Underline);
        Assert.Contains(T.RunWithText(doc, "struck").TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
        var marked = T.RunWithText(doc, "marked");
        Assert.Equal(Hex("highlight-yellow"), T.ColourOf(marked.Background));
        Assert.Equal(Hex("canvas"), T.ColourOf(marked.Foreground)); // a highlight carries its own ink
        Assert.Equal(Hex("ink-red"), T.ColourOf(T.RunWithText(doc, " red").Foreground));
        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, " auto").ReadLocalValue(System.Windows.Documents.TextElement.ForegroundProperty));
        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, " not bold").ReadLocalValue(System.Windows.Documents.TextElement.FontWeightProperty));
        Assert.Equal(20 * 1.5, T.RunWithText(doc, " huge").FontSize, 6); // 24 pt against an 11 pt body
        Assert.Equal(20 * 0.8, T.RunWithText(doc, " tiny").FontSize, 6); // 8 pt
        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, "Normal words").ReadLocalValue(System.Windows.Documents.TextElement.FontSizeProperty));
    }

    private static string Hex(string token) => IluPrompter.Theme.ThemeResources.GetColor(token).ToString();

    [WpfFact]
    public void Docx_ListsTablesBreaksAlignmentAndImages()
    {
        var doc = T.Load(_service.Load(StandardDocx()).DocumentXaml);
        var texts = T.Texts(doc);

        Assert.Contains("• first bullet", texts);
        Assert.Contains("not actually a bullet", texts); // numId 0 means "no list"
        var order = new[] { "cell A1", "cell B1", "cell A2", "## Cell heading" }.Select(t => texts.IndexOf(t)).ToArray();
        Assert.All(order, i => Assert.True(i >= 0));
        Assert.Equal(order.OrderBy(i => i), order); // row by row
        Assert.Equal(TextAlignment.Center, T.Navigator.GetParagraphs(doc).Single(p => T.Text(p) == "centred line").TextAlignment);
        Assert.Contains("before\r\nafter", texts);
        Assert.Contains("image caption", texts);
        Assert.Contains("", texts); // blank spacer paragraphs are kept
    }

    [WpfFact]
    public void Docx_ParagraphStyleSizesAndDefaultsDecideTheDominantSize()
    {
        // A 14 pt body with a 28 pt line: Huge relative to the body, whatever the absolute numbers are.
        var path = BuildDocx((body, styles) =>
        {
            styles.Append(ParagraphStyle("BodyText", "Body Text", sizeHalfPoints: "28"));
            body.Append(P("BodyText", R("body body body body body body")));
            body.Append(P("BodyText", R("large line", p => p.Append(new W.FontSize { Val = "56" }))));
        }, defaultSizeHalfPoints: "22");

        var doc = T.Load(_service.Load(path).DocumentXaml);

        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, "body body").ReadLocalValue(System.Windows.Documents.TextElement.FontSizeProperty));
        Assert.Equal(20 * 1.5, T.RunWithText(doc, "large line").FontSize, 6);
    }

    [WpfFact]
    public void Docx_TitleIsTheFileName()
    {
        var path = StandardDocx();
        var named = Path.Combine(Path.GetDirectoryName(path)!, "Quarterly Town Hall.docx");
        File.Move(path, named, overwrite: true);

        Assert.Equal("Quarterly Town Hall", _service.Load(named).Title);
    }

    [WpfFact]
    public void Docx_BrokenFilesAreRejectedWithAMessage()
    {
        var notZip = T.TempFile(".docx");
        File.WriteAllText(notZip, "this is not a zip file");
        var emptyZip = T.TempFile(".docx");
        using (var archive = System.IO.Compression.ZipFile.Open(emptyZip, System.IO.Compression.ZipArchiveMode.Create))
            archive.CreateEntry("hello.txt");

        Assert.Throws<ScriptFileException>(() => _service.Load(notZip));
        Assert.Throws<ScriptFileException>(() => _service.Load(emptyZip));
    }
}
