namespace IluPrompter.Core.Tests;

/// <summary>
/// A stand-in for the real geometry builder: lays out paragraph texts at a fixed number of characters per line, with a
/// paragraph gap, blank paragraphs as empty space (no line) and "## " paragraphs as hidden section markers (half a line,
/// no line), and fills <see cref="LayoutGeometry.ParagraphTexts"/>. Enough to simulate edits while live.
/// </summary>
internal static class ScriptLayout
{
    public const double LineHeight = 100;
    public const int CharsPerLine = 40;
    public const double GapLines = 0.5;

    public static LayoutGeometry Build(IReadOnlyList<string> paragraphs, long version, double lineHeight = LineHeight, int charsPerLine = CharsPerLine)
    {
        var tops = new List<double>();
        var heights = new List<double>();
        var lineParagraphs = new List<int>();
        var offsets = new List<int>();
        var paragraphTops = new double[paragraphs.Count];
        var sections = new List<SectionMarker>();
        var y = 0.0;
        for (var p = 0; p < paragraphs.Count; p++)
        {
            var text = paragraphs[p];
            paragraphTops[p] = y;
            if (text.StartsWith("##", StringComparison.Ordinal))
            {
                sections.Add(new SectionMarker(sections.Count, text.TrimStart('#', ' '), p, y));
                y += lineHeight * 0.5;
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                y += lineHeight * (1 + GapLines);
                continue;
            }

            var n = (text.Length + charsPerLine - 1) / charsPerLine;
            for (var j = 0; j < n; j++)
            {
                tops.Add(y + (j * lineHeight));
                heights.Add(lineHeight);
                lineParagraphs.Add(p);
                offsets.Add(j * charsPerLine);
            }

            y += (n * lineHeight) + (GapLines * lineHeight);
        }

        var endTop = y + lineHeight;
        return new LayoutGeometry(
            "v" + version + "|" + lineHeight + "|" + charsPerLine, version, lineHeight, endTop + lineHeight, endTop,
            tops.ToArray(), heights.ToArray(), lineParagraphs.ToArray(), offsets.ToArray(), paragraphTops, sections, paragraphs.Count * 10)
        {
            ParagraphTexts = paragraphs.ToArray(),
        };
    }

    /// <summary>A script of distinct paragraphs, 1 to 4 lines long, every word unique ("w{paragraph}x{word}").</summary>
    public static List<string> Script(int count = 60)
    {
        var list = new List<string>(count);
        for (var p = 0; p < count; p++) list.Add(Paragraph(p, 1 + (p % 4)));
        return list;
    }

    public static string Paragraph(int id, int lines)
    {
        var words = new List<string>();
        var length = 0;
        for (var w = 0; length < (lines * CharsPerLine) - 12; w++)
        {
            var word = "w" + id + "x" + w;
            words.Add(word);
            length += word.Length + 1;
        }

        return string.Join(" ", words);
    }
}

/// <summary>What the reading line shows: the text of the line under it and where in that line it sits.</summary>
internal readonly record struct ReadingView(int Paragraph, string LineText, double Fraction, double Outside)
{
    public static ReadingView Of(LayoutGeometry g, double position, double readingLine)
    {
        var y = position + readingLine;
        var i = g.FindLineAt(y);
        var top = g.LineTops[i];
        var h = g.LineHeights[i];
        var p = g.LineParagraphs[i];
        var start = g.LineCharOffsets[i];
        var end = i + 1 < g.LineCount && g.LineParagraphs[i + 1] == p ? g.LineCharOffsets[i + 1] : g.ParagraphTexts[p].Length;
        var text = g.ParagraphTexts[p][start..end];
        return y < top + h
            ? new ReadingView(p, text, Math.Round((y - top) / h, 6), 0)
            : new ReadingView(p, text, 1, Math.Round(y - (top + h), 6));
    }

    /// <summary>Same text at the same place (the paragraph index may differ).</summary>
    public bool SameTextAs(ReadingView other) =>
        LineText == other.LineText && Math.Abs(Fraction - other.Fraction) < 1e-6 && Math.Abs(Outside - other.Outside) < 1e-6;
}
