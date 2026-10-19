using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>One paragraph (or the END block) of a laid-out script: a frozen drawing in coordinates relative to
/// <see cref="Top"/>, and the vertical band it occupies in document coordinates.</summary>
internal sealed class PictureBlock
{
    public PictureBlock(Drawing drawing, double top, double bottom, int paragraph)
    {
        Drawing = drawing;
        Top = top;
        Bottom = bottom;
        Paragraph = paragraph;
    }

    /// <summary>Canonical paragraph index; -1 for the END block.</summary>
    public int Paragraph { get; }

    public Drawing Drawing { get; }

    public double Top { get; }

    public double Bottom { get; }
}

/// <summary>
/// An immutable, frozen picture of the script laid out for one output size and one set of text settings, plus its
/// geometry tables (ARCHITECTURE §3 "Layout thread"). Built on the layout thread from a FlowDocument laid out there; every
/// drawing is frozen, so the output thread and the UI thread both display the same picture without laying anything out.
/// </summary>
internal sealed class PromptPicture
{
    private readonly double[] _tops;
    private readonly double[] _maxBottoms;

    public PromptPicture(LayoutGeometry geometry, string contentKey, TextArea area, OutputGeometry output, PictureBlock[] blocks)
    {
        Geometry = geometry;
        ContentKey = contentKey;
        Area = area;
        Output = output;
        Blocks = blocks;
        _tops = new double[blocks.Length];
        _maxBottoms = new double[blocks.Length];
        var max = double.NegativeInfinity;
        for (var i = 0; i < blocks.Length; i++)
        {
            _tops[i] = blocks[i].Top;
            max = Math.Max(max, blocks[i].Bottom);
            _maxBottoms[i] = max;
        }
    }

    public LayoutGeometry Geometry { get; }

    /// <summary>Layout key plus what only changes the look (ink): equal keys = identical picture.</summary>
    public string ContentKey { get; }

    /// <summary>The text area the script was laid out for.</summary>
    public TextArea Area { get; }

    public OutputGeometry Output { get; }

    public IReadOnlyList<PictureBlock> Blocks { get; }

    /// <summary>The geometry of the picture this one replaced, and the anchor map from this one's geometry to it: a surface
    /// still showing the previous picture while the engine already has this layout maps positions with it (no
    /// allocation on that surface's frame path).</summary>
    public LayoutGeometry? PreviousGeometry { get; init; }

    public AnchorMap? MapToPrevious { get; init; }

    /// <summary>How long the layout thread took, for diagnostics.</summary>
    public PictureTimings Timings { get; init; }

    /// <summary>Paragraphs built for a patched script edit; -1 when the whole script was laid out.</summary>
    public int PatchedParagraphs { get; init; } = -1;

    /// <summary>What the layout thread allocated for it (MB, same parts as <see cref="Timings"/>), for diagnostics.</summary>
    public PictureTimings Allocated { get; init; }

    /// <summary>Blocks that intersect [top, bottom): indices [start, end). Allocation-free.</summary>
    public void FindBlocks(double top, double bottom, out int start, out int end)
    {
        // end: first block whose top is at or below `bottom`.
        int lo = 0, hi = _tops.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_tops[mid] < bottom) lo = mid + 1; else hi = mid;
        }

        end = lo;

        // start: first block whose (running maximum) bottom is below `top`.
        lo = 0;
        hi = end;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_maxBottoms[mid] <= top) lo = mid + 1; else hi = mid;
        }

        start = lo;
    }
}

/// <summary>Where the layout thread spent its time on one picture (milliseconds).</summary>
internal readonly record struct PictureTimings(double Document, double Layout, double Geometry, double Picture, double Total);
