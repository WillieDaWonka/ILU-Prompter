namespace IluPrompter.Core;

/// <summary>
/// A stable position in the script, independent of layout: the N-th paragraph in canonical order
/// (every Paragraph in document order, descending into List/ListItem/Section/Table cells; see
/// Documents.IDocumentNavigator) and a character offset within that paragraph's plain text.
/// </summary>
public readonly record struct DocumentAnchor(int ParagraphIndex, int CharOffset)
{
    public static DocumentAnchor Start => new(0, 0);
}

/// <summary>A section marker as laid out on the output.</summary>
public sealed record SectionMarker(int Index, string Name, int ParagraphIndex, double Top);

/// <summary>
/// Immutable layout tables for one layout of one snapshot, in output DIPs, document coordinates
/// (y = 0 is the top of the first block; the host uses PagePadding 0). Built on a WPF thread by
/// Documents.ILayoutGeometryBuilder, then shared read-only with any thread.
/// Arrays are owned by this instance and must not be mutated after construction.
/// </summary>
public sealed class LayoutGeometry
{
    private readonly double[] _lineTops;
    private readonly double[] _lineHeights;
    private readonly int[] _lineParagraphs;
    private readonly int[] _lineCharOffsets;
    private readonly double[] _paragraphTops;
    private readonly double[] _sectionTops;

    public LayoutGeometry(
        string layoutKey,
        long snapshotVersion,
        double baseLineHeight,
        double documentHeight,
        double endTop,
        double[] lineTops,
        double[] lineHeights,
        int[] lineParagraphs,
        int[] lineCharOffsets,
        double[] paragraphTops,
        IReadOnlyList<SectionMarker> sections,
        int wordCount)
    {
        ArgumentNullException.ThrowIfNull(layoutKey);
        if (lineHeights.Length != lineTops.Length || lineParagraphs.Length != lineTops.Length || lineCharOffsets.Length != lineTops.Length)
            throw new ArgumentException("Line arrays must have equal length.");

        LayoutKey = layoutKey;
        SnapshotVersion = snapshotVersion;
        BaseLineHeight = baseLineHeight;
        DocumentHeight = documentHeight;
        EndTop = endTop;
        _lineTops = lineTops;
        _lineHeights = lineHeights;
        _lineParagraphs = lineParagraphs;
        _lineCharOffsets = lineCharOffsets;
        _paragraphTops = paragraphTops;
        Sections = sections;
        _sectionTops = new double[sections.Count];
        for (var i = 0; i < sections.Count; i++) _sectionTops[i] = sections[i].Top;
        WordCount = wordCount;
    }

    /// <summary>Identity of the layout inputs (snapshot version, width, font, spacing...). Equal keys = equal layout.</summary>
    public string LayoutKey { get; }
    public long SnapshotVersion { get; }

    /// <summary>Height of one Normal-size line (base font size * line spacing). Used for lpm to px/s.</summary>
    public double BaseLineHeight { get; }

    /// <summary>Total height including the END block and nothing after it.</summary>
    public double DocumentHeight { get; }

    /// <summary>Top of the END marker line. Scrolling stops when the END line's centre reaches the reading line.</summary>
    public double EndTop { get; }

    public IReadOnlyList<double> LineTops => _lineTops;
    public IReadOnlyList<double> LineHeights => _lineHeights;
    /// <summary>Paragraph index (canonical order) of each line.</summary>
    public IReadOnlyList<int> LineParagraphs => _lineParagraphs;
    /// <summary>Char offset within its paragraph at which each line starts.</summary>
    public IReadOnlyList<int> LineCharOffsets => _lineCharOffsets;
    /// <summary>Top of each paragraph, indexed by canonical paragraph index.</summary>
    public IReadOnlyList<double> ParagraphTops => _paragraphTops;
    public IReadOnlyList<SectionMarker> Sections { get; }
    public int WordCount { get; }

    public int LineCount => _lineTops.Length;
    public int ParagraphCount => _paragraphTops.Length;

    /// <summary>Index of the line whose [top, top+height) contains y; nearest line if y is outside all lines; -1 if no lines.</summary>
    public int FindLineAt(double y)
    {
        if (_lineTops.Length == 0) return -1;
        var i = LastAtOrBefore(_lineTops, y);
        return i < 0 ? 0 : i;
    }

    /// <summary>Index of the last paragraph whose top is at or above y (0 if y is above the first); -1 if none.</summary>
    public int FindParagraphAt(double y)
    {
        if (_paragraphTops.Length == 0) return -1;
        var i = LastAtOrBefore(_paragraphTops, y);
        return i < 0 ? 0 : i;
    }

    /// <summary>Index of the last section whose top is at or above y; -1 if y is before the first section.</summary>
    public int FindSectionAt(double y) => _sectionTops.Length == 0 ? -1 : LastAtOrBefore(_sectionTops, y);

    /// <summary>Vertical centre of line i.</summary>
    public double LineCentre(int i) => _lineTops[i] + (_lineHeights[i] / 2);

    /// <summary>The anchor at the start of the line containing y.</summary>
    public DocumentAnchor GetAnchor(double y)
    {
        var i = FindLineAt(y);
        return i < 0 ? DocumentAnchor.Start : new DocumentAnchor(_lineParagraphs[i], _lineCharOffsets[i]);
    }

    /// <summary>Index of the line containing the anchor (last line in that paragraph starting at or before CharOffset).
    /// Clamps to the nearest existing paragraph.</summary>
    public int FindLineForAnchor(DocumentAnchor anchor)
    {
        if (_lineTops.Length == 0) return -1;
        int lo = 0, hi = _lineTops.Length - 1, best = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            var cmp = _lineParagraphs[mid] != anchor.ParagraphIndex
                ? _lineParagraphs[mid].CompareTo(anchor.ParagraphIndex)
                : _lineCharOffsets[mid].CompareTo(anchor.CharOffset);
            if (cmp <= 0) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
        }
        return best;
    }

    public static LayoutGeometry Empty(string layoutKey = "empty", double baseLineHeight = 100) =>
        new(layoutKey, 0, baseLineHeight, 0, 0, [], [], [], [], [], Array.Empty<SectionMarker>(), 0);

    private static int LastAtOrBefore(double[] sorted, double y)
    {
        var i = Array.BinarySearch(sorted, y);
        if (i >= 0)
        {
            while (i + 1 < sorted.Length && sorted[i + 1] == y) i++;
            return i;
        }
        return ~i - 1;
    }
}
