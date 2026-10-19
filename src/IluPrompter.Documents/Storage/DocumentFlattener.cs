using System.Globalization;
using System.Windows;
using System.Windows.Documents;

namespace IluPrompter.Documents;

/// <summary>
/// Flattens a FlowDocument to "paragraphs only, canonical order" (ARCHITECTURE §4.E SnapshotBuilder): list items
/// become paragraphs prefixed "• " or "n. ", table cells are emitted in row order, Sections are unwrapped, and
/// BlockUIContainer / InlineUIContainer / Figure / Floater are dropped. Inline formatting is kept.
/// The source document is never modified: the result is a deep copy of the paragraphs.
/// </summary>
internal static class DocumentFlattener
{
    /// <summary>True when the document is already only top-level paragraphs with no embedded UI (nothing to do).</summary>
    public static bool IsFlat(FlowDocument document)
    {
        foreach (var block in document.Blocks)
        {
            if (block is not Paragraph paragraph || HasEmbeddedUi(paragraph.Inlines)) return false;
        }

        return true;
    }

    /// <summary>True when any paragraph holds an inline UI container / anchored block, or any block is a BlockUIContainer.</summary>
    public static bool HasEmbeddedUi(FlowDocument document) => HasEmbeddedUi(document.Blocks);

    private static bool HasEmbeddedUi(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case BlockUIContainer:
                    return true;
                case Paragraph p when HasEmbeddedUi(p.Inlines):
                    return true;
                case Section s when HasEmbeddedUi(s.Blocks):
                    return true;
                case List l:
                    foreach (var item in l.ListItems)
                        if (HasEmbeddedUi(item.Blocks)) return true;
                    break;
                case Table t:
                    foreach (var group in t.RowGroups)
                        foreach (var row in group.Rows)
                            foreach (var cell in row.Cells)
                                if (HasEmbeddedUi(cell.Blocks)) return true;
                    break;
            }
        }

        return false;
    }

    private static bool HasEmbeddedUi(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer or AnchoredBlock) return true;
            if (inline is Span span && HasEmbeddedUi(span.Inlines)) return true;
        }

        return false;
    }

    /// <summary>A new document holding deep copies of every canonical paragraph of <paramref name="source"/>.</summary>
    public static FlowDocument CloneFlattened(FlowDocument source)
    {
        var result = new FlowDocument();
        Visit(source.Blocks, (paragraph, prefix) => result.Blocks.Add(CloneParagraph(paragraph, prefix)), null);
        if (result.Blocks.Count == 0) result.Blocks.Add(new Paragraph());
        return result;
    }

    /// <summary>
    /// The list marker ("1. ", "• ") that the flattened copy of <paramref name="paragraph"/> starts with, or "" when it
    /// gets none. The navigator adds its length to editor anchors so that an anchor means the same text in the editor
    /// and in the snapshot/output (where the marker is real text).
    /// </summary>
    public static string MarkerOf(FlowDocument document, Paragraph paragraph)
    {
        if (!IsInList(paragraph)) return "";
        var marker = "";
        Visit(document.Blocks, (p, prefix) =>
        {
            if (ReferenceEquals(p, paragraph)) marker = prefix ?? "";
        }, null);
        return marker;
    }

    private static bool IsInList(Paragraph paragraph)
    {
        for (var parent = paragraph.Parent; parent is TextElement element; parent = element.Parent)
        {
            if (element is ListItem) return true;
        }

        return false;
    }

    /// <summary>
    /// Visits every canonical paragraph of <paramref name="blocks"/> in navigator order (so flattened index == editor
    /// index) with the list marker its flattened copy starts with. A pending list marker goes to the first paragraph
    /// produced. Empty list items produce nothing (the navigator has no paragraph for them either).
    /// </summary>
    private static void Visit(BlockCollection blocks, Action<Paragraph, string?> visit, string? pendingPrefix)
    {
        foreach (var block in blocks)
        {
            if (DocumentNavigator.IsGenerated(block)) continue;
            switch (block)
            {
                case Paragraph paragraph:
                    visit(paragraph, pendingPrefix);
                    pendingPrefix = null;
                    break;
                case Section section:
                    Visit(section.Blocks, visit, pendingPrefix);
                    pendingPrefix = null;
                    break;
                case List list:
                    VisitList(list, visit);
                    pendingPrefix = null;
                    break;
                case Table table:
                    foreach (var group in table.RowGroups)
                        foreach (var row in group.Rows)
                            foreach (var cell in row.Cells)
                                Visit(cell.Blocks, visit, null);
                    break;
            }
        }
    }

    private static void VisitList(List list, Action<Paragraph, string?> visit)
    {
        var number = list.StartIndex < 1 ? 1 : list.StartIndex;
        foreach (var item in list.ListItems)
        {
            var prefix = PrefixFor(list.MarkerStyle, number);
            number++;
            Visit(item.Blocks, visit, prefix);
        }
    }

    private static string? PrefixFor(TextMarkerStyle style, int number) => style switch
    {
        TextMarkerStyle.None => null,
        TextMarkerStyle.Decimal => number.ToString(CultureInfo.InvariantCulture) + ". ",
        TextMarkerStyle.LowerLatin => Latin(number, 'a') + ". ",
        TextMarkerStyle.UpperLatin => Latin(number, 'A') + ". ",
        TextMarkerStyle.LowerRoman => Roman(number).ToLowerInvariant() + ". ",
        TextMarkerStyle.UpperRoman => Roman(number) + ". ",
        _ => "• ",
    };

    private static string Latin(int number, char first)
    {
        var chars = new Stack<char>();
        var n = Math.Max(1, number);
        while (n > 0)
        {
            n--;
            chars.Push((char)(first + (n % 26)));
            n /= 26;
        }

        return new string(chars.ToArray());
    }

    private static string Roman(int number)
    {
        (int Value, string Text)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var n = Math.Clamp(number, 1, 3999);
        var sb = new System.Text.StringBuilder();
        foreach (var (value, text) in table)
        {
            while (n >= value)
            {
                sb.Append(text);
                n -= value;
            }
        }

        return sb.ToString();
    }

    private static Paragraph CloneParagraph(Paragraph source, string? prefix)
    {
        var copy = new Paragraph();
        CopyLocalValues(source, copy);
        if (prefix is not null) copy.Inlines.Add(new Run(prefix));
        CloneInlines(source.Inlines, copy.Inlines);
        return copy;
    }

    private static void CloneInlines(InlineCollection source, InlineCollection into)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case Run run:
                {
                    var copy = new Run(run.Text);
                    CopyLocalValues(run, copy);
                    into.Add(copy);
                    break;
                }

                case LineBreak lineBreak:
                {
                    var copy = new LineBreak();
                    CopyLocalValues(lineBreak, copy);
                    into.Add(copy);
                    break;
                }

                case Span span:
                {
                    Span copy = span switch
                    {
                        Bold => new Bold(),
                        Italic => new Italic(),
                        Underline => new Underline(),
                        _ => new Span(),
                    };
                    CopyLocalValues(span, copy);
                    CloneInlines(span.Inlines, copy.Inlines);
                    into.Add(copy);
                    break;
                }

                // InlineUIContainer, Figure, Floater and anything else: dropped.
            }
        }
    }

    /// <summary>Copies every locally set, non-expression value that the target type owns.</summary>
    private static void CopyLocalValues(TextElement source, TextElement target)
    {
        var enumerator = source.GetLocalValueEnumerator();
        var targetType = target.GetType();
        while (enumerator.MoveNext())
        {
            var entry = enumerator.Current;
            var property = entry.Property;
            if (property.ReadOnly || entry.Value is Expression) continue;
            if (property == Run.TextProperty) continue;
            if (!property.OwnerType.IsAssignableFrom(targetType)) continue;
            var value = entry.Value;
            if (value is Freezable { IsFrozen: false } freezable) value = freezable.CloneCurrentValue();
            target.SetValue(property, value);
        }
    }
}
