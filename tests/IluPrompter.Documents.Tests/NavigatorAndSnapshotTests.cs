using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents.Tests;

public class NavigatorAndSnapshotTests
{
    private static FlowDocument MessyDocument(bool withUi = true)
    {
        var doc = new FlowDocument();
        var first = new Paragraph(new Run("Hello "));
        first.Inlines.Add(new Bold(new Run("bold")));
        first.Inlines.Add(new LineBreak());
        first.Inlines.Add(new Run("after break"));
        doc.Blocks.Add(first);
        doc.Blocks.Add(new Paragraph());
        doc.Blocks.Add(new Paragraph(new Run("## Middle")));
        var list = new List { MarkerStyle = TextMarkerStyle.Decimal };
        list.ListItems.Add(new ListItem(new Paragraph(new Run("alpha"))));
        list.ListItems.Add(new ListItem(new Paragraph(new Run("beta"))));
        doc.Blocks.Add(list);
        var bullets = new List { MarkerStyle = TextMarkerStyle.Disc };
        bullets.ListItems.Add(new ListItem(new Paragraph(new Run("dot"))));
        doc.Blocks.Add(bullets);
        var table = new Table();
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run("cell A"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("cell B"))));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        doc.Blocks.Add(table);
        var section = new Section();
        section.Blocks.Add(new Paragraph(new Run("in section")));
        doc.Blocks.Add(section);
        if (withUi) doc.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.Button()));
        doc.Blocks.Add(new Paragraph(new Run("last")));
        return doc;
    }

    /// <summary>Every caret position that lies inside a paragraph (what the editor can report).</summary>
    private static IEnumerable<TextPointer> CaretPositions(FlowDocument doc)
    {
        for (var pos = doc.ContentStart.GetInsertionPosition(LogicalDirection.Forward); pos is not null; pos = pos.GetNextInsertionPosition(LogicalDirection.Forward))
        {
            if (pos.Paragraph is not null && !DocumentNavigator.IsGenerated(pos.Paragraph)) yield return pos;
        }
    }

    [WpfFact]
    public void Navigator_WalksNestedBlocksInDocumentOrder_AndSkipsUiAndGenerated()
    {
        var doc = MessyDocument();
        doc.Blocks.Add(new Paragraph(new Run("END")) { Tag = DocumentNavigator.GeneratedTag });

        var texts = T.Texts(doc);

        Assert.Equal(["Hello bold\r\nafter break", "", "## Middle", "alpha", "beta", "dot", "cell A", "cell B", "in section", "last"], texts);
    }

    [WpfFact]
    public void Navigator_AnchorsRoundTripAtEveryCaretPosition()
    {
        var doc = MessyDocument();
        var paragraphs = T.Navigator.GetParagraphs(doc);
        var checkedPositions = 0;

        foreach (var pos in CaretPositions(doc))
        {
            var anchor = T.Navigator.GetAnchor(doc, pos);
            var back = T.Navigator.GetPointer(doc, anchor);
            var again = T.Navigator.GetAnchor(doc, back);

            Assert.Equal(anchor, again);
            var paragraph = paragraphs[anchor.ParagraphIndex];
            Assert.Equal(
                DocumentNavigator.TextBetween(paragraph.ContentStart, pos),
                DocumentNavigator.TextBetween(paragraph.ContentStart, back));
            checkedPositions++;
        }

        Assert.True(checkedPositions > 30);
    }

    [WpfFact]
    public void Navigator_GetPointer_ClampsToTheNearestValidPosition()
    {
        var doc = T.Doc("one", "two");

        var beyondText = T.Navigator.GetAnchor(doc, T.Navigator.GetPointer(doc, new DocumentAnchor(1, 999)));
        var beyondParagraphs = T.Navigator.GetAnchor(doc, T.Navigator.GetPointer(doc, new DocumentAnchor(50, 1)));
        var negative = T.Navigator.GetAnchor(doc, T.Navigator.GetPointer(doc, new DocumentAnchor(-3, -2)));

        Assert.Equal(new DocumentAnchor(1, 3), beyondText);
        Assert.Equal(new DocumentAnchor(1, 1), beyondParagraphs);
        Assert.Equal(DocumentAnchor.Start, negative);
    }

    [WpfFact]
    public void Navigator_EmptyDocument_IsHandled()
    {
        var doc = new FlowDocument();

        Assert.Empty(T.Navigator.GetParagraphs(doc));
        Assert.Equal(DocumentAnchor.Start, T.Navigator.GetAnchor(doc, doc.ContentStart));
        Assert.NotNull(T.Navigator.GetPointer(doc, new DocumentAnchor(3, 3)));
    }

    [WpfFact]
    public void Snapshot_Flatten_ListsAndTablesBecomeParagraphs_WithEqualParagraphIndices()
    {
        var doc = MessyDocument();
        var editorTexts = T.Texts(doc);

        var snap = T.Snapshots.Build(doc, 7, 20, "##");
        var flat = T.Load(snap.FlowDocumentXaml);
        var snapTexts = T.Texts(flat);

        Assert.Equal(7, snap.Version);
        Assert.Equal(20, snap.EditorBaseFontSize);
        Assert.Equal(editorTexts.Count, snap.ParagraphCount);
        Assert.Equal(editorTexts.Count, snapTexts.Count);
        Assert.All(flat.Blocks, b => Assert.IsType<Paragraph>(b));
        Assert.Equal("1. alpha", snapTexts[3]);
        Assert.Equal("2. beta", snapTexts[4]);
        Assert.Equal("• dot", snapTexts[5]);
        Assert.Equal("cell A", snapTexts[6]);
        Assert.Equal("cell B", snapTexts[7]);
        Assert.Equal("in section", snapTexts[8]);
        Assert.Equal("last", snapTexts[9]);
        Assert.Single(snap.Sections);
        Assert.Equal(new SectionInfo(0, "Middle", 2), snap.Sections[0]);
    }

    [WpfFact]
    public void Snapshot_DoesNotModifyTheEditorDocument()
    {
        var doc = MessyDocument();
        var before = T.Xaml(doc);

        _ = T.Snapshots.Build(doc, 1, 20, "##");

        Assert.Equal(before, T.Xaml(doc));
        Assert.Contains(doc.Blocks, b => b is List);
        Assert.Contains(doc.Blocks, b => b is BlockUIContainer);
    }

    [WpfFact]
    public void Snapshot_AnchorsOfEveryCaretPositionResolveToTheSameTextInTheSnapshot()
    {
        var doc = MessyDocument();
        var snap = T.Snapshots.Build(doc, 1, 20, "##");
        var flat = T.Load(snap.FlowDocumentXaml);
        var editorParagraphs = T.Navigator.GetParagraphs(doc);
        var flatParagraphs = T.Navigator.GetParagraphs(flat);
        foreach (var pos in CaretPositions(doc))
        {
            var anchor = T.Navigator.GetAnchor(doc, pos);
            var editorParagraph = editorParagraphs[anchor.ParagraphIndex];
            var snapParagraph = flatParagraphs[anchor.ParagraphIndex];
            var editorBefore = DocumentNavigator.TextBetween(editorParagraph.ContentStart, pos);

            // Inside a list item the snapshot paragraph starts with its marker ("1. ", "• "): offsets shift by its length.
            var prefixLength = T.Text(snapParagraph).Length - T.Text(editorParagraph).Length;
            var snapPointer = T.Navigator.GetPointer(flat, anchor with { CharOffset = anchor.CharOffset + prefixLength });
            var snapBefore = DocumentNavigator.TextBetween(snapParagraph.ContentStart, snapPointer);

            Assert.Equal(editorParagraph.Parent is ListItem, prefixLength > 0);
            Assert.Equal(T.Text(snapParagraph)[..prefixLength] + editorBefore, snapBefore);
        }
    }

    [WpfFact]
    public void Snapshot_PlainDocument_KeepsFormattingAndCounts()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("## Intro")));
        var p = new Paragraph();
        p.Inlines.Add(new Run("one two ") { FontSize = 25 });
        p.Inlines.Add(new Run("three") { Foreground = new SolidColorBrush(Colors.Red), Background = new SolidColorBrush(Colors.Yellow) });
        doc.Blocks.Add(p);
        doc.Blocks.Add(new Paragraph(new Run("")));
        doc.Blocks.Add(new Paragraph(new Run("four  five")));

        var snap = T.Snapshots.Build(doc, 3, 20, "##");

        Assert.Equal(4, snap.ParagraphCount);
        Assert.Equal(5, snap.WordCount); // section paragraph is not counted
        Assert.Single(snap.Sections);
        var back = T.Load(snap.FlowDocumentXaml);
        Assert.Equal(25, T.RunWithText(back, "one two").FontSize);
        Assert.Equal("#FFFF0000", T.ColourOf(T.RunWithText(back, "three").Foreground));
        Assert.Equal("#FFFFFF00", T.ColourOf(T.RunWithText(back, "three").Background));
    }

    [WpfFact]
    public void Snapshot_ParagraphCountSurvivesEmptyParagraphsAtBothEnds()
    {
        var doc = T.Doc("", "x", "", "");

        var snap = T.Snapshots.Build(doc, 1, 20, "##");

        Assert.Equal(4, snap.ParagraphCount);
        Assert.Equal(4, T.Navigator.GetParagraphs(T.Load(snap.FlowDocumentXaml)).Count);
    }

    [WpfFact]
    public void Snapshot_EmptyXaml_LoadsOneEmptyParagraph()
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("old")));

        T.Snapshots.LoadInto(doc, "");

        Assert.Equal([""], T.Texts(doc));
    }

    [WpfFact]
    public void Snapshot_ImagesAndUiContainersAreRemovedFromTheCopy()
    {
        var doc = T.Doc("before");
        var p = new Paragraph(new Run("text "));
        p.Inlines.Add(new InlineUIContainer(new System.Windows.Controls.Button()));
        p.Inlines.Add(new Run("tail"));
        doc.Blocks.Add(p);

        var snap = T.Snapshots.Build(doc, 1, 20, "##");

        Assert.DoesNotContain("InlineUIContainer", snap.FlowDocumentXaml);
        Assert.DoesNotContain("Button", snap.FlowDocumentXaml);
        Assert.Equal(["before", "text tail"], T.Texts(T.Load(snap.FlowDocumentXaml)));
    }

    [WpfFact]
    public void ToScriptFile_KeepsListsAndTables_AndRoundTripsThroughLoadInto()
    {
        var doc = MessyDocument(withUi: false);

        var file = T.Snapshots.ToScriptFile(doc, "Show", 22, new ScriptOverrides { LinesVisible = 6 });
        var back = T.Load(file.DocumentXaml);

        Assert.Equal("Show", file.Title);
        Assert.Equal(22, file.EditorBaseFontSize);
        Assert.Equal(6, file.Overrides!.LinesVisible);
        Assert.Contains("<List", file.DocumentXaml);
        Assert.Equal(T.Texts(doc), T.Texts(back));
    }

    [WpfFact]
    public void ToScriptFile_DropsEmbeddedUi()
    {
        var file = T.Snapshots.ToScriptFile(MessyDocument(withUi: true), "Show", 20, null);

        Assert.DoesNotContain("BlockUIContainer", file.DocumentXaml);
        Assert.DoesNotContain("Button", file.DocumentXaml);
        Assert.Equal(10, T.Texts(T.Load(file.DocumentXaml)).Count);
    }
}
