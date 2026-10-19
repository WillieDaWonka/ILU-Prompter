using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents.Tests;

public class XamlSanitizerTests
{
    private const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"";

    private static string Wrap(string inner, string rootAttributes = "") =>
        $"<Section {Ns} xml:space=\"preserve\" {rootAttributes}>{inner}</Section>";

    [Theory]
    [InlineData("<Button/>")]
    [InlineData("<Paragraph><InlineUIContainer><Button/></InlineUIContainer></Paragraph>")]
    [InlineData("<BlockUIContainer><Image/></BlockUIContainer>")]
    [InlineData("<Paragraph><Run.Foreground><SolidColorBrush Color=\"Red\"/></Run.Foreground></Paragraph>")]
    [InlineData("<Paragraph><Floater/></Paragraph>")]
    [InlineData("<ObjectDataProvider ObjectType=\"{x:Type Process}\" MethodName=\"Start\"/>")]
    public void RejectsElementsOutsideTheAllowList(string inner)
    {
        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(Wrap(inner)));
    }

    [Fact]
    public void Rejects_ObjectDataProvider_InItsOwnNamespaceToo()
    {
        var xaml = $"<Section {Ns} xmlns:sys=\"clr-namespace:System.Diagnostics;assembly=System\"><Paragraph><Run>x</Run></Paragraph>" +
                   "<ObjectDataProvider ObjectType=\"sys:Process\" MethodName=\"Start\"/></Section>";

        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(xaml));
    }

    [Theory]
    [InlineData("<Paragraph><Run Foreground=\"{x:Static SystemColors.ControlBrush}\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run Foreground=\"{Binding Path=Foo}\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run FontSize=\"{StaticResource Big}\">x</Run></Paragraph>")]
    [InlineData("<Paragraph Margin=\" {}{0}\">x</Paragraph>")]
    public void RejectsMarkupExtensionsInAttributeValues(string inner)
    {
        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(Wrap(inner)));
    }

    [Theory]
    [InlineData("<Paragraph><Run x:Name=\"a\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">x</Run></Paragraph>")]
    [InlineData("<Paragraph xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><Run>x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run Tag=\"clr-namespace:Foo\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run Foreground=\"clr-namespace:Foo;assembly=Bar\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run Language=\"en\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run FontFamily=\"http://example.test/fonts/#Evil\">x</Run></Paragraph>")]
    [InlineData("<Paragraph><Run FontFamily=\"C:\\Windows\\Fonts\\x.ttf\">x</Run></Paragraph>")]
    public void RejectsForeignNamespacesUnknownAttributesAndFontPaths(string inner)
    {
        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(Wrap(inner)));
    }

    [Fact]
    public void RejectsDtd_AndEntityTricks()
    {
        var xxe = "<?xml version=\"1.0\"?><!DOCTYPE s [<!ENTITY e SYSTEM \"file:///C:/Windows/win.ini\">]>" +
                  $"<Section {Ns}><Paragraph><Run>&e;</Run></Paragraph></Section>";
        var bomb = "<!DOCTYPE lolz [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;\">]>" +
                   $"<Section {Ns}><Paragraph><Run>&b;</Run></Paragraph></Section>";

        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(xxe));
        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(bomb));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<Section")]
    [InlineData("<Section><Paragraph/></Section>")] // no namespace
    [InlineData("<Section xmlns=\"urn:other\"><Paragraph/></Section>")]
    public void RejectsMalformedOrForeignDocuments(string xaml)
    {
        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(xaml));
    }

    [Fact]
    public void RejectsExcessiveNesting()
    {
        var deep = string.Concat(Enumerable.Repeat("<Span>", 200)) + "x" + string.Concat(Enumerable.Repeat("</Span>", 200));

        Assert.Throws<ScriptFileException>(() => XamlSanitizer.Sanitize(Wrap("<Paragraph>" + deep + "</Paragraph>")));
    }

    [WpfFact]
    public void AcceptsWhatTextRangeSaveWrites_AndStripsHostDocumentAttributes()
    {
        var doc = new FlowDocument { FontSize = 20, Foreground = Brushes.White };
        var p = new Paragraph(new Run("Hello "));
        p.Inlines.Add(new Run("big") { FontSize = 25, Foreground = Brushes.Red, Background = Brushes.Yellow });
        p.Inlines.Add(new Bold(new Run("bold")));
        p.Inlines.Add(new Underline(new Run("under")));
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run("strike") { TextDecorations = TextDecorations.Strikethrough });
        doc.Blocks.Add(p);
        doc.Blocks.Add(new Paragraph());
        var saved = T.Xaml(doc);

        var clean = XamlSanitizer.Sanitize(saved);

        Assert.Contains("TextDecorations=\"Strikethrough\"", clean);
        Assert.Contains("FontSize=\"25\"", clean);
        Assert.DoesNotContain("Typography.", clean);
        Assert.DoesNotContain("Foreground=\"" + Colors.White + "\"", clean); // the host document's own foreground
        Assert.StartsWith("<Section", clean);
        var back = T.Load(saved);
        Assert.Equal(T.Texts(doc), T.Texts(back));
        Assert.Equal(25, T.RunWithText(back, "big").FontSize);
        Assert.DoesNotContain(T.Navigator.GetParagraphs(back), paragraph => paragraph.FontSize == 20 || paragraph.Foreground is SolidColorBrush { Color.A: 255 } s && s.Color == Colors.White);
    }

    [Fact]
    public void KeepsSignificantWhitespace()
    {
        var xaml = Wrap("<Paragraph><Run>a </Run><Run> b</Run><Run> </Run></Paragraph>");

        var clean = XamlSanitizer.Sanitize(xaml);

        Assert.Contains("<Run>a </Run><Run> b</Run><Run> </Run>", clean);
    }

    [Fact]
    public void HyperlinkBecomesAPlainSpan_WithoutItsTarget()
    {
        var clean = XamlSanitizer.Sanitize(Wrap("<Paragraph><Hyperlink NavigateUri=\"http://example.test\" FontSize=\"30\"><Run>link</Run></Hyperlink></Paragraph>"));

        Assert.DoesNotContain("Hyperlink", clean);
        Assert.DoesNotContain("NavigateUri", clean);
        Assert.Contains("<Span FontSize=\"30\"><Run>link</Run></Span>", clean);
    }

    [Fact]
    public void EscapesTextContent()
    {
        var clean = XamlSanitizer.Sanitize(Wrap("<Paragraph><Run>1 &lt; 2 &amp; &lt;Button/&gt;</Run></Paragraph>"));

        Assert.Contains("1 &lt; 2 &amp; &lt;Button/&gt;", clean);
    }

    [WpfFact]
    public void LoadInto_NeverInstantiatesRejectedContent()
    {
        var doc = T.Doc("keep me");

        var ex = Assert.Throws<ScriptFileException>(() => T.Snapshots.LoadInto(doc, Wrap("<Button/>")));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.Equal(["keep me"], T.Texts(doc)); // a rejected script leaves the target untouched
    }
}
