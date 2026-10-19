using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Top-level and nested paragraphs; no END-tag skipping.</summary>
public sealed class DocumentNavigator : IDocumentNavigator
{
    public IReadOnlyList<Paragraph> GetParagraphs(FlowDocument document)
    {
        var list = new List<Paragraph>();
        Walk(document.Blocks, list);
        return list;
    }

    public string GetText(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n');

    public DocumentAnchor GetAnchor(FlowDocument document, TextPointer pointer)
    {
        var paragraphs = GetParagraphs(document);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (paragraphs[i].ContentEnd.CompareTo(pointer) >= 0)
                return new DocumentAnchor(i, Math.Max(0, new TextRange(paragraphs[i].ContentStart, pointer).Text.Length));
        }

        return new DocumentAnchor(Math.Max(0, paragraphs.Count - 1), 0);
    }

    public TextPointer GetPointer(FlowDocument document, DocumentAnchor anchor)
    {
        var paragraphs = GetParagraphs(document);
        if (paragraphs.Count == 0) return document.ContentStart;
        return paragraphs[Math.Clamp(anchor.ParagraphIndex, 0, paragraphs.Count - 1)].ContentStart;
    }

    private static void Walk(IEnumerable<Block> blocks, List<Paragraph> into)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph p: into.Add(p); break;
                case Section s: Walk(s.Blocks, into); break;
                case List l: foreach (var item in l.ListItems) Walk(item.Blocks, into); break;
                case Table t:
                    foreach (var group in t.RowGroups)
                        foreach (var row in group.Rows)
                            foreach (var cell in row.Cells) Walk(cell.Blocks, into);
                    break;
            }
        }
    }
}
