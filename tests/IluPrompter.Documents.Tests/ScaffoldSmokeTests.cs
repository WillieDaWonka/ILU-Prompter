using System.Windows.Documents;
using IluPrompter.Documents;

namespace IluPrompter.Documents.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void SectionParser_FindsMarkers()
    {
        var sections = new SectionParser().Parse(["Hello", "## Welcome", "Text", "##"], "##");
        Assert.Equal(2, sections.Count);
        Assert.Equal("Welcome", sections[0].Name);
        Assert.Equal(3, sections[1].ParagraphIndex);
    }

    [WpfFact]
    public void SnapshotBuilder_RoundTripsXaml()
    {
        var doc = new FlowDocument(new Paragraph(new Run("## One")));
        doc.Blocks.Add(new Paragraph(new Bold(new Run("Read this"))));
        var builder = new SnapshotBuilder(new DocumentNavigator(), new SectionParser());
        var snap = builder.Build(doc, 1, 20, "##");
        Assert.Equal(2, snap.ParagraphCount);
        Assert.Single(snap.Sections);
        var copy = new FlowDocument();
        builder.LoadInto(copy, snap.FlowDocumentXaml);
        Assert.Equal(2, new DocumentNavigator().GetParagraphs(copy).Count);
    }
}
