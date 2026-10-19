using System.Windows;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Builds the line, paragraph and section tables from a laid-out output document (ARCHITECTURE §2.5 / §4.E).
/// Owner thread only. O(lines): one <c>GetLineStartPosition</c> and one <c>GetCharacterRect</c> per visual line, and
/// character offsets are accumulated between consecutive line starts instead of being recomputed from the paragraph start.
/// <para>
/// Tops are line-box tops. <c>GetCharacterRect</c> returns the font box of the first character, not the line box.
/// With <see cref="LineStackingStrategy.BlockLineHeight"/> (as the output document uses) WPF scales the baseline
/// proportionally, so the font box starts <c>b * (lineHeight - boxHeight)</c> below the line top, where
/// <c>b = FontFamily.Baseline / FontFamily.LineSpacing</c> (verified to 0.01 px for Segoe UI and Poppins). The builder
/// undoes that offset. Other documents fall back to the rectangle itself. A line that starts with a smaller run than
/// its tallest run is off by a fraction of a pixel to a few pixels, which is below what the reading line can show.
/// </para>
/// </summary>
public sealed class LayoutGeometryBuilder : ILayoutGeometryBuilder
{
    private readonly IDocumentNavigator _navigator;

    public LayoutGeometryBuilder(IDocumentNavigator navigator) => _navigator = navigator;

    public LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(laidOutDocument);
        ArgumentNullException.ThrowIfNull(options);

        var paragraphs = _navigator.GetParagraphs(laidOutDocument);
        var indexOf = new Dictionary<Paragraph, int>(paragraphs.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < paragraphs.Count; i++) indexOf[paragraphs[i]] = i;

        var family = laidOutDocument.FontFamily;
        var baselineRatio = family is not null && family.LineSpacing > 0 && family.Baseline > 0
            ? Math.Clamp(family.Baseline / family.LineSpacing, 0.3, 0.95)
            : 0.8;
        var endBlock = FindEndBlock(laidOutDocument);
        var endStart = endBlock?.ContentStart;

        var tops = new List<double>(Math.Max(16, paragraphs.Count * 2));
        var heights = new List<double>(tops.Capacity);
        var lineParagraphs = new List<int>(tops.Capacity);
        var lineOffsets = new List<int>(tops.Capacity);
        var paragraphTops = new double[paragraphs.Count];
        Array.Fill(paragraphTops, double.NaN);

        var position = laidOutDocument.ContentStart.GetLineStartPosition(0);
        Paragraph? currentParagraph = null;
        TextPointer? previous = null;
        var offset = 0;
        var lineInParagraph = 0;
        var firstTop = 0.0;
        var fixedPitch = false;
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

                tops.Add(top);
                heights.Add(height);
                lineParagraphs.Add(paragraphIndex);
                lineOffsets.Add(offset);
                if (double.IsNaN(paragraphTops[paragraphIndex])) paragraphTops[paragraphIndex] = top;
            }

            position = Advance(position, ref guard);
        }

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

        return new LayoutGeometry(
            options.LayoutKey,
            options.Snapshot.Version,
            options.BaseLineHeight,
            documentHeight,
            endTop,
            tops.ToArray(),
            heights.ToArray(),
            lineParagraphs.ToArray(),
            lineOffsets.ToArray(),
            paragraphTops,
            sections,
            options.Snapshot.WordCount);
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
