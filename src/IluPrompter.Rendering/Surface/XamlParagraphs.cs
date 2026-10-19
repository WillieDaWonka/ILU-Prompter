namespace IluPrompter.Rendering;

/// <summary>
/// Splits a snapshot's flattened FlowDocument XAML (as <c>TextRange.Save</c> writes it: one root element whose children
/// are only <c>Paragraph</c> elements) into the root's start tag and one string per paragraph, without parsing it into
/// WPF objects. Equal strings mean equal paragraphs (text and formatting), which is what lets the layout thread patch
/// only the paragraphs an edit changed. Anything unexpected (comments, CDATA, text between paragraphs, another element at
/// the top level) returns false and the caller lays the whole script out again, so this never has to be clever.
/// </summary>
internal static class XamlParagraphs
{
    public static bool TrySplit(string xaml, out string rootStart, out string[] paragraphs)
    {
        rootStart = "";
        paragraphs = [];
        if (string.IsNullOrEmpty(xaml)) return false;

        var i = SkipWhitespace(xaml, 0);
        if (i + 1 < xaml.Length && xaml[i] == '<' && xaml[i + 1] == '?')
        {
            var pi = xaml.IndexOf("?>", i, StringComparison.Ordinal);
            if (pi < 0) return false;
            i = SkipWhitespace(xaml, pi + 2);
        }

        if (i >= xaml.Length || xaml[i] != '<' || !IsNameStart(xaml, i + 1)) return false;
        var rootEnd = ScanTag(xaml, i, out var selfClosing);
        if (rootEnd < 0) return false;
        rootStart = xaml[i..rootEnd];
        if (selfClosing)
        {
            rootStart = rootStart[..^2].TrimEnd() + ">";
            return SkipWhitespace(xaml, rootEnd) == xaml.Length;
        }

        var list = new List<string>();
        i = rootEnd;
        while (true)
        {
            i = SkipWhitespace(xaml, i);
            if (i >= xaml.Length) return false;
            if (xaml[i] != '<') return false; // text directly under the root
            if (i + 1 < xaml.Length && xaml[i + 1] == '/')
            {
                // The root's end tag, then nothing but white space.
                var close = xaml.IndexOf('>', i);
                if (close < 0 || SkipWhitespace(xaml, close + 1) != xaml.Length) return false;
                break;
            }

            if (!StartsWithElement(xaml, i + 1, "Paragraph")) return false;
            var end = ScanElement(xaml, i);
            if (end < 0) return false;
            list.Add(xaml[i..end]);
            i = end;
        }

        paragraphs = list.ToArray();
        return true;
    }

    /// <summary>End index (exclusive) of the element starting at <paramref name="start"/>, or -1.</summary>
    private static int ScanElement(string s, int start)
    {
        var depth = 0;
        var i = start;
        while (i < s.Length)
        {
            if (s[i] != '<')
            {
                var next = s.IndexOf('<', i);
                if (next < 0) return -1;
                i = next;
                continue;
            }

            if (i + 1 >= s.Length) return -1;
            var c = s[i + 1];
            if (c == '!' || c == '?') return -1; // comment, CDATA, processing instruction: not expected here
            if (c == '/')
            {
                var close = s.IndexOf('>', i);
                if (close < 0) return -1;
                depth--;
                i = close + 1;
                if (depth == 0) return i;
                if (depth < 0) return -1;
                continue;
            }

            var end = ScanTag(s, i, out var selfClosing);
            if (end < 0) return -1;
            if (!selfClosing) depth++;
            i = end;
            if (depth == 0) return i; // a self-closing top element
        }

        return -1;
    }

    /// <summary>End index (exclusive) of the start tag at <paramref name="start"/>, honouring quoted attribute values.</summary>
    private static int ScanTag(string s, int start, out bool selfClosing)
    {
        selfClosing = false;
        var quote = '\0';
        for (var i = start + 1; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }

            if (c is '"' or '\'') quote = c;
            else if (c == '<') return -1;
            else if (c == '>')
            {
                selfClosing = s[i - 1] == '/';
                return i + 1;
            }
        }

        return -1;
    }

    private static bool StartsWithElement(string s, int at, string name) =>
        string.CompareOrdinal(s, at, name, 0, name.Length) == 0
        && at + name.Length < s.Length
        && s[at + name.Length] is ' ' or '>' or '/' or '\t' or '\r' or '\n';

    private static bool IsNameStart(string s, int at) => at < s.Length && (char.IsLetter(s[at]) || s[at] == '_');

    private static int SkipWhitespace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }
}
