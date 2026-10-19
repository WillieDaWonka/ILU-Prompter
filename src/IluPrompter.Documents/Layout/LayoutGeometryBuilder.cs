using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Builds the line, paragraph and section tables from a laid-out output document (ARCHITECTURE §2.5 / §4.E).
/// Owner thread only; stateless.
/// <para>
/// <b>Tops are line-box tops</b>, not glyph boxes, and <b>lines are text lines</b>: a blank paragraph (only white space,
/// e.g. an empty line or a section marker whose name is hidden) gets its paragraph top but no line (ARCHITECTURE §2.1).
/// Two ways to read the lines, both O(lines):
/// </para>
/// <list type="number">
/// <item><b>Visual tree (fast path, about 10x faster).</b> After layout WPF has one line visual per laid-out line, grouped
/// under one paragraph visual per paragraph, each offset to its line-box top. The builder reads those offsets and finds
/// each line's start offset by locating the line's first drawn glyph run (its <c>GlyphRun.Characters</c>) in the
/// paragraph text. Used only for flat documents (the output factory always flattens) with a fixed line height, and only
/// when every check passes: one visual group per paragraph, every line's text found in order, tops increasing, and the
/// first and last paragraph tops agree with <c>GetCharacterRect</c> to 0.5 DIP. Anything unexpected (for example a
/// future WPF changing its internal visual layout) falls back to (2) silently and gives identical tables, only slower.</item>
/// <item><b>Line walk (fallback).</b> One <c>GetLineStartPosition</c> per line and one <c>GetCharacterRect</c> per
/// paragraph. <c>GetCharacterRect</c> returns the font box of the first character, not the line box. With
/// <see cref="LineStackingStrategy.BlockLineHeight"/> WPF scales the baseline proportionally, so the font box starts
/// <c>b * (lineHeight - boxHeight)</c> below the line top, where <c>b = FontFamily.Baseline / FontFamily.LineSpacing</c>
/// (verified to 0.01 px for Segoe UI and Poppins: at 91.5 DIP Poppins the raw rectangle is 13.4 DIP above the line box).
/// The walk undoes that offset. Documents without a fixed line height use the rectangle itself.</item>
/// </list>
/// </summary>
public sealed class LayoutGeometryBuilder : ILayoutGeometryBuilder
{
    /// <summary>Which line source a build may use (tests compare the two).</summary>
    internal enum LineSource
    {
        Auto,
        LineWalk,
        VisualTree,
    }

    private readonly IDocumentNavigator _navigator;

    public LayoutGeometryBuilder(IDocumentNavigator navigator) => _navigator = navigator;

    public LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options) =>
        Build(laidOutDocument, options, LineSource.Auto, out _)!;

    /// <summary>Returns null only when <paramref name="source"/> is <see cref="LineSource.VisualTree"/> and the fast path
    /// could not be used.</summary>
    internal LayoutGeometry? Build(FlowDocument laidOutDocument, GeometryBuildOptions options, LineSource source, out LineSource used)
    {
        ArgumentNullException.ThrowIfNull(laidOutDocument);
        ArgumentNullException.ThrowIfNull(options);

        var paragraphs = _navigator.GetParagraphs(laidOutDocument);
        var family = laidOutDocument.FontFamily;
        var baselineRatio = family is not null && family.LineSpacing > 0 && family.Baseline > 0
            ? Math.Clamp(family.Baseline / family.LineSpacing, 0.3, 0.95)
            : 0.8;
        var endBlock = FindEndBlock(laidOutDocument);

        var lines = new LineTable(paragraphs.Count);
        used = LineSource.VisualTree;
        if (source == LineSource.LineWalk || !TryReadVisualLines(laidOutDocument, paragraphs, baselineRatio, lines))
        {
            if (source == LineSource.VisualTree) return null;
            used = LineSource.LineWalk;
            lines = new LineTable(paragraphs.Count);
            WalkLines(laidOutDocument, paragraphs, baselineRatio, endBlock?.ContentStart, lines);
        }

        var tops = lines.Tops;
        var heights = lines.Heights;
        var paragraphTops = lines.ParagraphTops;
        FillGaps(paragraphTops, tops.Count == 0 ? 0 : tops[0]);

        double endTop, documentHeight;
        if (endBlock is not null)
        {
            var endRect = endBlock.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var lastRect = laidOutDocument.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
            endTop = endRect.IsEmpty ? LastBottom(tops, heights) : endRect.Top;
            documentHeight = lastRect.IsEmpty ? endTop + options.BaseLineHeight : Math.Max(lastRect.Bottom, endTop);
        }
        else
        {
            endTop = LastBottom(tops, heights);
            documentHeight = endTop;
        }

        var sections = new List<SectionMarker>(options.Snapshot.Sections.Count);
        foreach (var section in options.Snapshot.Sections)
        {
            if (section.ParagraphIndex < 0 || section.ParagraphIndex >= paragraphTops.Length) continue;
            sections.Add(new SectionMarker(section.Index, section.Name, section.ParagraphIndex, paragraphTops[section.ParagraphIndex]));
        }

        // Paragraph texts (what the char offsets index) let the engine carry the reading anchor through the next edit.
        var texts = new string[paragraphs.Count];
        for (var p = 0; p < texts.Length; p++) texts[p] = DocumentNavigator.TextBetween(paragraphs[p].ContentStart, paragraphs[p].ContentEnd);

        return new LayoutGeometry(
            options.LayoutKey,
            options.Snapshot.Version,
            options.BaseLineHeight,
            documentHeight,
            endTop,
            tops.ToArray(),
            heights.ToArray(),
            lines.Paragraphs.ToArray(),
            lines.Offsets.ToArray(),
            paragraphTops,
            sections,
            options.Snapshot.WordCount)
        {
            ParagraphTexts = texts,
        };
    }

    private sealed class LineTable
    {
        public LineTable(int paragraphCount)
        {
            var capacity = Math.Max(16, paragraphCount * 2);
            Tops = new List<double>(capacity);
            Heights = new List<double>(capacity);
            Paragraphs = new List<int>(capacity);
            Offsets = new List<int>(capacity);
            ParagraphTops = new double[paragraphCount];
            Array.Fill(ParagraphTops, double.NaN);
        }

        public List<double> Tops { get; }
        public List<double> Heights { get; }
        public List<int> Paragraphs { get; }
        public List<int> Offsets { get; }
        public double[] ParagraphTops { get; }

        public void Add(double top, double height, int paragraph, int offset)
        {
            Tops.Add(top);
            Heights.Add(height);
            Paragraphs.Add(paragraph);
            Offsets.Add(offset);
            if (double.IsNaN(ParagraphTops[paragraph])) ParagraphTops[paragraph] = top;
        }

        /// <summary>Drops the lines from <paramref name="index"/> on (and the paragraph tops they set).</summary>
        public void RemoveFrom(int index)
        {
            if (index >= Tops.Count) return;
            for (var i = index; i < Tops.Count; i++) ParagraphTops[Paragraphs[i]] = double.NaN;
            var count = Tops.Count - index;
            Tops.RemoveRange(index, count);
            Heights.RemoveRange(index, count);
            Paragraphs.RemoveRange(index, count);
            Offsets.RemoveRange(index, count);
        }

        /// <summary>A blank paragraph: it has a top (sections, paragraph lookups) but no reading line.</summary>
        public void SetParagraphTop(int paragraph, double top)
        {
            if (double.IsNaN(ParagraphTops[paragraph])) ParagraphTops[paragraph] = top;
        }
    }

    /// <summary>True when the paragraph has nothing to read: no text but white space. Empty lines, and section markers
    /// whose names are hidden (the factory empties them), are layout space, not lines: they get a paragraph top but no
    /// line, so the start position, line steps and section jumps always put text on the reading line.</summary>
    private static bool IsBlank(string text)
    {
        foreach (var c in text)
        {
            if (IsInk(c)) return false;
        }

        return true;
    }

    /// <summary>A character that puts something on the screen. Not white space, and not a format or control character
    /// (WPF draws an empty line as two zero-width spaces, U+200B, which is a format character).</summary>
    private static bool IsInk(char c) =>
        !char.IsWhiteSpace(c) && char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.Control);

    // ---- fast path: the laid-out line visuals ----------------------------------------------------------------

    private const string LineVisualType = "LineVisual";
    private const string DocumentViewType = "FlowDocumentView";
    private const double FrameTolerance = 0.5;

    private static bool TryReadVisualLines(FlowDocument document, IReadOnlyList<Paragraph> paragraphs, double baselineRatio, LineTable lines)
    {
        try
        {
            return ReadVisualLines(document, paragraphs, baselineRatio, lines);
        }
        catch (InvalidOperationException)
        {
            return false; // e.g. a visual that is not connected to the frame; the walk handles it
        }
    }

    private static bool ReadVisualLines(FlowDocument document, IReadOnlyList<Paragraph> paragraphs, double baselineRatio, LineTable lines)
    {
        if (paragraphs.Count == 0 || !IsFlat(document, paragraphs)) return false;
        if (document.Parent is not Visual host) return false;

        Visual? frame = null;
        var groups = new List<List<Visual>>(paragraphs.Count);
        CollectLineGroups(host, groups, ref frame);
        if (frame is null || groups.Count != paragraphs.Count) return false;

        var previousTop = double.NegativeInfinity;
        for (var p = 0; p < paragraphs.Count; p++)
        {
            var paragraph = paragraphs[p];
            if (!HasFixedLineHeight(paragraph)) return false;
            var height = paragraph.LineHeight;
            var group = groups[p];
            string? text = null;
            var cursor = 0;
            var firstLine = lines.Tops.Count;
            var hasInk = false;
            var unknownOffset = false;
            for (var j = 0; j < group.Count; j++)
            {
                var top = group[j].TransformToAncestor(frame).Transform(default).Y;
                if (!double.IsFinite(top) || top <= previousTop) return false;
                previousTop = top;

                var (first, drawn, ink) = ReadGlyphs(group[j]);
                hasInk |= ink;
                int offset;
                if (j == 0)
                {
                    offset = 0;
                }
                else if (first is null || first.Length == 0)
                {
                    // A line with nothing drawn (e.g. between two line breaks): its start is unknown. Fine if the whole
                    // paragraph turns out blank (its lines are dropped); otherwise the walk must do it.
                    unknownOffset = true;
                    offset = cursor;
                }
                else
                {
                    text ??= DocumentNavigator.TextBetween(paragraph.ContentStart, paragraph.ContentEnd);
                    if (cursor > text.Length) return false;
                    offset = text.IndexOf(first, cursor, StringComparison.Ordinal);
                    if (offset < cursor) return false;
                }

                cursor = offset + drawn;
                lines.Add(top, height, p, offset);
            }

            if (text is not null && cursor > text.Length) return false;
            if (hasInk && unknownOffset) return false;
            if (!hasInk && group.Count > 0)
            {
                // Blank paragraph (see IsBlank): keep its top, drop its lines.
                lines.RemoveFrom(firstLine);
                lines.SetParagraphTop(p, group[0].TransformToAncestor(frame).Transform(default).Y);
            }
        }

        // The frame must be the GetCharacterRect frame: check the first and the last paragraph against the rectangle.
        return AgreesWithRectangle(paragraphs[0], lines.ParagraphTops[0], baselineRatio)
               && AgreesWithRectangle(paragraphs[^1], lines.ParagraphTops[^1], baselineRatio);
    }

    /// <summary>Top-level blocks are exactly the navigator's paragraphs (plus generated blocks such as END).</summary>
    private static bool IsFlat(FlowDocument document, IReadOnlyList<Paragraph> paragraphs)
    {
        var index = 0;
        foreach (var block in document.Blocks)
        {
            if (DocumentNavigator.IsGenerated(block))
            {
                if (block is Paragraph) return false; // would add a visual group of its own
                continue;
            }

            if (block is not Paragraph paragraph || index >= paragraphs.Count || !ReferenceEquals(paragraphs[index], paragraph)) return false;
            index++;
        }

        return index == paragraphs.Count;
    }

    /// <summary>Depth-first, in document order: each visual whose children include line visuals is one group.</summary>
    private static void CollectLineGroups(DependencyObject node, List<List<Visual>> groups, ref Visual? frame)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        List<Visual>? group = null;
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            var name = child.GetType().Name;
            if (string.Equals(name, LineVisualType, StringComparison.Ordinal))
            {
                if (group is null)
                {
                    group = new List<Visual>(4);
                    groups.Add(group);
                }

                group.Add((Visual)child);
                continue;
            }

            if (frame is null && string.Equals(name, DocumentViewType, StringComparison.Ordinal)) frame = (Visual)child;
            CollectLineGroups(child, groups, ref frame);
        }
    }

    /// <summary>The characters of the line's first glyph run, the number of characters drawn on the line, and whether any
    /// of them is not white space.</summary>
    private static (string? First, int Drawn, bool Ink) ReadGlyphs(Visual line)
    {
        string? first = null;
        var drawn = 0;
        var ink = false;
        Visit(VisualTreeHelper.GetDrawing(line), ref first, ref drawn, ref ink);
        return (first, drawn, ink);

        static void Visit(Drawing? drawing, ref string? first, ref int drawn, ref bool ink)
        {
            switch (drawing)
            {
                case GlyphRunDrawing { GlyphRun.Characters: { Count: > 0 } characters }:
                    if (first is null)
                    {
                        var buffer = new char[characters.Count];
                        characters.CopyTo(buffer, 0);
                        first = new string(buffer);
                    }

                    if (!ink)
                    {
                        foreach (var c in characters)
                        {
                            if (!IsInk(c)) continue;
                            ink = true;
                            break;
                        }
                    }

                    drawn += characters.Count;
                    break;
                case DrawingGroup group:
                    foreach (var child in group.Children) Visit(child, ref first, ref drawn, ref ink);
                    break;
            }
        }
    }

    private static bool AgreesWithRectangle(Paragraph paragraph, double top, double baselineRatio)
    {
        var rect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty) return true; // nothing to compare with; the other checks still hold
        var (expected, _) = LineBox(paragraph, rect, baselineRatio);
        return Math.Abs(expected - top) <= FrameTolerance;
    }

    // ---- fallback: the line walk -----------------------------------------------------------------------------

    private static void WalkLines(FlowDocument document, IReadOnlyList<Paragraph> paragraphs, double baselineRatio, TextPointer? endStart, LineTable lines)
    {
        var indexOf = new Dictionary<Paragraph, int>(paragraphs.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < paragraphs.Count; i++) indexOf[paragraphs[i]] = i;

        var position = document.ContentStart.GetLineStartPosition(0);
        Paragraph? currentParagraph = null;
        TextPointer? previous = null;
        var offset = 0;
        var lineInParagraph = 0;
        var firstTop = 0.0;
        var fixedPitch = false;
        var blank = false;
        var guard = 0;
        while (position is not null && (endStart is null || position.CompareTo(endStart) < 0))
        {
            var paragraph = position.Paragraph;
            if (paragraph is not null && indexOf.TryGetValue(paragraph, out var paragraphIndex))
            {
                var startsParagraph = !ReferenceEquals(paragraph, currentParagraph);
                if (startsParagraph)
                {
                    currentParagraph = paragraph;
                    offset = DocumentNavigator.TextBetween(paragraph.ContentStart, position).Length;
                    lineInParagraph = 0;
                    fixedPitch = HasFixedLineHeight(paragraph);
                    blank = IsBlank(DocumentNavigator.TextBetween(paragraph.ContentStart, paragraph.ContentEnd));
                }
                else
                {
                    offset += DocumentNavigator.TextBetween(previous!, position).Length;
                    lineInParagraph++;
                }

                previous = position;

                // With BlockLineHeight every line of a paragraph is exactly LineHeight tall, so one rectangle per
                // paragraph is enough and the rest are arithmetic (a rectangle costs about as much as the line walk).
                double top, height;
                if (fixedPitch && !startsParagraph)
                {
                    height = paragraph.LineHeight;
                    top = firstTop + (lineInParagraph * height);
                }
                else
                {
                    var rect = position.GetCharacterRect(LogicalDirection.Forward);
                    if (rect.IsEmpty)
                    {
                        position = Advance(position, ref guard);
                        continue;
                    }

                    (top, height) = LineBox(paragraph, rect, baselineRatio);
                    if (startsParagraph) firstTop = top;
                }

                if (blank) lines.SetParagraphTop(paragraphIndex, top);
                else lines.Add(top, height, paragraphIndex, offset);
            }

            position = Advance(position, ref guard);
        }
    }

    /// <summary>The start of the next visual line, or null at the end (or if the walk stops moving).</summary>
    private static TextPointer? Advance(TextPointer position, ref int guard)
    {
        var next = position.GetLineStartPosition(1, out var moved);
        return moved == 0 || next is null || ++guard > 5_000_000 ? null : next;
    }

    private static bool HasFixedLineHeight(Paragraph paragraph) =>
        paragraph.LineStackingStrategy == LineStackingStrategy.BlockLineHeight && !double.IsNaN(paragraph.LineHeight) && paragraph.LineHeight > 0;

    private static Block? FindEndBlock(FlowDocument document)
    {
        Block? found = null;
        foreach (var block in document.Blocks)
        {
            if (DocumentNavigator.IsGenerated(block) && block is BlockUIContainer) found = block;
        }

        return found;
    }

    private static (double Top, double Height) LineBox(Paragraph paragraph, Rect rect, double baselineRatio)
    {
        if (HasFixedLineHeight(paragraph))
            return (rect.Top - (baselineRatio * (paragraph.LineHeight - rect.Height)), paragraph.LineHeight);

        return (rect.Top, rect.Height);
    }

    private static double LastBottom(List<double> tops, List<double> heights) =>
        tops.Count == 0 ? 0 : tops[^1] + heights[^1];

    /// <summary>Paragraphs without a line (should not happen) take the previous top so the table stays sorted.</summary>
    private static void FillGaps(double[] paragraphTops, double first)
    {
        var last = first;
        for (var i = 0; i < paragraphTops.Length; i++)
        {
            if (double.IsNaN(paragraphTops[i])) paragraphTops[i] = last;
            else last = paragraphTops[i];
        }
    }
}
