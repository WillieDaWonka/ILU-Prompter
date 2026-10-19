using System.Windows.Documents;

namespace IluPrompter.App.Editing;

/// <summary>
/// Plain find over a FlowDocument (Ctrl+F, F3). Matches inside one run of text; a match that straddles a formatting
/// change (half bold, half not) is not found. Case-insensitive.
/// </summary>
public static class DocumentFinder
{
    /// <summary>The next match at or after <paramref name="from"/>, wrapping to the top. Null if none.</summary>
    public static TextRange? FindNext(FlowDocument document, TextPointer from, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        return Search(from, document.ContentEnd, text) ?? Search(document.ContentStart, from, text);
    }

    /// <summary>The last match that ends at or before <paramref name="before"/>, wrapping to the bottom. Null if none.</summary>
    public static TextRange? FindPrevious(FlowDocument document, TextPointer before, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        return LastMatch(document.ContentStart, before, text) ?? LastMatch(before, document.ContentEnd, text);
    }

    private static TextRange? Search(TextPointer start, TextPointer end, string text)
    {
        var position = start;
        while (position is not null && position.CompareTo(end) < 0)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var run = position.GetTextInRun(LogicalDirection.Forward);
                var index = run.IndexOf(text, StringComparison.CurrentCultureIgnoreCase);
                if (index >= 0)
                {
                    var matchStart = position.GetPositionAtOffset(index);
                    var matchEnd = matchStart?.GetPositionAtOffset(text.Length);
                    if (matchStart is not null && matchEnd is not null && matchEnd.CompareTo(end) <= 0)
                        return new TextRange(matchStart, matchEnd);
                }
            }

            position = position.GetNextContextPosition(LogicalDirection.Forward);
        }

        return null;
    }

    private static TextRange? LastMatch(TextPointer start, TextPointer end, string text)
    {
        TextRange? last = null;
        var cursor = start;
        while (cursor.CompareTo(end) < 0)
        {
            var found = Search(cursor, end, text);
            if (found is null) break;
            last = found;
            var next = found.Start.GetPositionAtOffset(1);
            if (next is null) break;
            cursor = next;
        }

        return last;
    }
}
