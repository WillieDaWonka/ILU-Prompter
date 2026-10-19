using System.Text;
using System.Windows;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Canonical paragraph order and anchors (ARCHITECTURE §2.11). Stateless; call on the thread that owns the document.
/// Anchors count characters the way <c>TextRange.Text</c> does (a LineBreak is two characters, "\r\n").
/// </summary>
public sealed class DocumentNavigator : IDocumentNavigator
{
    /// <summary>Tag put on blocks the output factory generates (the END marker). The navigator skips them.</summary>
    public const string GeneratedTag = "ilu:generated";

    public static bool IsGenerated(Block block) => block.Tag is string tag && string.Equals(tag, GeneratedTag, StringComparison.Ordinal);

    public IReadOnlyList<Paragraph> GetParagraphs(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var list = new List<Paragraph>();
        Walk(document.Blocks, list);
        return list;
    }

    public string GetText(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        return TextBetween(paragraph.ContentStart, paragraph.ContentEnd);
    }

    public DocumentAnchor GetAnchor(FlowDocument document, TextPointer pointer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pointer);
        var paragraphs = GetParagraphs(document);
        if (paragraphs.Count == 0) return DocumentAnchor.Start;

        var owner = pointer.Paragraph ?? pointer.GetInsertionPosition(LogicalDirection.Forward).Paragraph;
        var index = owner is null ? -1 : IndexOf(paragraphs, owner);
        if (index >= 0)
        {
            var paragraph = paragraphs[index];
            if (pointer.CompareTo(paragraph.ContentStart) <= 0) return new DocumentAnchor(index, 0);
            var end = pointer.CompareTo(paragraph.ContentEnd) >= 0 ? paragraph.ContentEnd : pointer;
            return new DocumentAnchor(index, TextBetween(paragraph.ContentStart, end).Length);
        }

        // Between blocks (or inside something we skip): snap to the first paragraph that ends at or after the pointer.
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (paragraphs[i].ContentEnd.CompareTo(pointer) < 0) continue;
            if (pointer.CompareTo(paragraphs[i].ContentStart) <= 0) return new DocumentAnchor(i, 0);
            return new DocumentAnchor(i, TextBetween(paragraphs[i].ContentStart, pointer).Length);
        }

        var last = paragraphs[^1];
        return new DocumentAnchor(paragraphs.Count - 1, TextBetween(last.ContentStart, last.ContentEnd).Length);
    }

    public TextPointer GetPointer(FlowDocument document, DocumentAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(document);
        var paragraphs = GetParagraphs(document);
        if (paragraphs.Count == 0) return document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        var paragraph = paragraphs[Math.Clamp(anchor.ParagraphIndex, 0, paragraphs.Count - 1)];
        return PointerAtCharacter(paragraph, anchor.CharOffset);
    }

    /// <summary>
    /// The plain text between two pointers, counted the way anchors count it: text runs as they are, a LineBreak as
    /// CR LF (what <c>TextRange.Text</c> gives), embedded UI as nothing. Unlike <c>TextRange.Text</c> it never
    /// adds list markers (a number and a tab) or table cell separators, so offsets are stable between editor and output documents.
    /// </summary>
    internal static string TextBetween(TextPointer start, TextPointer end)
    {
        if (start.CompareTo(end) >= 0) return "";
        var builder = new StringBuilder();
        var position = start;
        while (position is not null && position.CompareTo(end) < 0)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                {
                    var available = Math.Min(position.GetTextRunLength(LogicalDirection.Forward), position.GetOffsetToPosition(end));
                    if (available <= 0) return builder.ToString();
                    var buffer = new char[available];
                    var copied = position.GetTextInRun(LogicalDirection.Forward, buffer, 0, available);
                    builder.Append(buffer, 0, copied);
                    position = position.GetPositionAtOffset(Math.Max(copied, 1));
                    break;
                }

                case TextPointerContext.ElementStart:
                    if (position.GetAdjacentElement(LogicalDirection.Forward) is LineBreak) builder.Append("\r\n");
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>The insertion position inside <paramref name="paragraph"/> after <paramref name="characters"/> characters
    /// of its text (clamped to the paragraph). One forward walk.</summary>
    internal static TextPointer PointerAtCharacter(Paragraph paragraph, int characters)
    {
        var end = paragraph.ContentEnd;
        var position = paragraph.ContentStart;
        var remaining = characters;
        while (remaining > 0 && position is not null && position.CompareTo(end) < 0)
        {
            switch (position.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                {
                    var run = Math.Min(position.GetTextRunLength(LogicalDirection.Forward), position.GetOffsetToPosition(end));
                    if (run <= 0) return end.GetInsertionPosition(LogicalDirection.Backward);
                    if (remaining <= run) return position.GetPositionAtOffset(remaining)!.GetInsertionPosition(LogicalDirection.Forward);
                    remaining -= run;
                    position = position.GetPositionAtOffset(run);
                    break;
                }

                case TextPointerContext.ElementStart when position.GetAdjacentElement(LogicalDirection.Forward) is LineBreak:
                {
                    // The two characters of the break (CR LF) are one indivisible object: land after it.
                    remaining -= 2;
                    position = position.GetNextContextPosition(LogicalDirection.Forward); // inside the break
                    position = position?.GetNextContextPosition(LogicalDirection.Forward); // after it
                    break;
                }

                default:
                    position = position.GetNextContextPosition(LogicalDirection.Forward);
                    break;
            }
        }

        if (position is null || position.CompareTo(end) > 0) return end.GetInsertionPosition(LogicalDirection.Backward);
        return position.GetInsertionPosition(LogicalDirection.Forward);
    }

    private static int IndexOf(IReadOnlyList<Paragraph> paragraphs, Paragraph paragraph)
    {
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (ReferenceEquals(paragraphs[i], paragraph)) return i;
        }

        return -1;
    }

    private static void Walk(BlockCollection blocks, List<Paragraph> into)
    {
        foreach (var block in blocks)
        {
            if (IsGenerated(block)) continue;
            switch (block)
            {
                case Paragraph p:
                    into.Add(p);
                    break;
                case Section s:
                    Walk(s.Blocks, into);
                    break;
                case List l:
                    foreach (var item in l.ListItems) Walk(item.Blocks, into);
                    break;
                case Table t:
                    foreach (var group in t.RowGroups)
                        foreach (var row in group.Rows)
                            foreach (var cell in row.Cells) Walk(cell.Blocks, into);
                    break;
            }
        }
    }
}
