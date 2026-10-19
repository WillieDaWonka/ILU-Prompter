namespace IluPrompter.Core;

/// <summary>
/// Maps a <see cref="DocumentAnchor"/> from one version of the script to another, through whatever edit happened in
/// between (ARCHITECTURE §2.5, "Anchors across edits"). Built once per relayout from the two versions' paragraph texts;
/// <see cref="Map"/> is then allocation-free (the surfaces call it per frame while they catch up with a new layout).
/// <para>
/// The diff is layered, like a patience diff: paragraphs equal at the start and at the end are kept; inside the middle,
/// paragraphs whose text is unique in both versions are matched in order (longest increasing run), which splits the
/// middle into small regions; inside a region, equal leading/trailing paragraphs are kept again and the rest is compared
/// character by character (common prefix and suffix of the region's text, paragraphs joined with '\n'). An anchor in
/// unchanged text maps exactly; an anchor inside text that was replaced or deleted maps to where the change starts in the
/// new text (the nearest surviving position). Splitting and joining paragraphs (Enter, Backspace) map exactly.
/// </para>
/// </summary>
public sealed class AnchorMap
{
    private readonly IReadOnlyList<string> _from;
    private readonly IReadOnlyList<string> _to;
    private readonly int _prefix;
    private readonly int _suffix;
    private readonly int _middleFrom;   // first paragraph of the "from" middle; middle = [_middleFrom, _from.Count - _suffix)
    private readonly int[] _matched = [];          // from-middle index -> to index, or -1
    private readonly Region[] _regions = [];       // in order; every unmatched from-middle paragraph is in exactly one
    private readonly long[] _cumFrom = [];         // cumulative (length + 1) of from paragraphs
    private readonly long[] _cumTo = [];

    /// <summary>Maps every anchor to itself (paragraph indices unchanged): same snapshot, or no texts to compare.</summary>
    public static AnchorMap Identity { get; } = new();

    private AnchorMap()
    {
        _from = Array.Empty<string>();
        _to = Array.Empty<string>();
        IsIdentity = true;
    }

    public AnchorMap(IReadOnlyList<string> from, IReadOnlyList<string> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        _from = from;
        _to = to;
        var nF = from.Count;
        var nT = to.Count;
        var max = Math.Min(nF, nT);
        while (_prefix < max && string.Equals(from[_prefix], to[_prefix], StringComparison.Ordinal)) _prefix++;
        while (_suffix < max - _prefix && string.Equals(from[nF - 1 - _suffix], to[nT - 1 - _suffix], StringComparison.Ordinal)) _suffix++;
        _middleFrom = _prefix;
        if (_prefix == nF && nF == nT)
        {
            IsIdentity = true;
            return;
        }

        var fromEnd = nF - _suffix;
        var toEnd = nT - _suffix;
        if (fromEnd <= _prefix) return; // pure insertion: nothing of "from" is in the middle

        _cumFrom = Cumulative(from);
        _cumTo = Cumulative(to);
        _matched = new int[fromEnd - _prefix];
        Array.Fill(_matched, -1);
        var pairs = MatchUnique(from, _prefix, fromEnd, to, _prefix, toEnd);
        foreach (var (f, t) in pairs) _matched[f - _prefix] = t;

        // Regions between consecutive matches (and the middle's ends).
        var regions = new List<Region>(pairs.Count + 1);
        int fa = _prefix, ta = _prefix;
        foreach (var (f, t) in pairs)
        {
            if (f > fa) regions.Add(MakeRegion(fa, f, ta, t));
            fa = f + 1;
            ta = t + 1;
        }

        if (fromEnd > fa) regions.Add(MakeRegion(fa, fromEnd, ta, toEnd));
        _regions = regions.ToArray();
    }

    /// <summary>True when every anchor maps to itself.</summary>
    public bool IsIdentity { get; }

    /// <summary>The map between two layouts: identity for the same snapshot (a layout-only change) or when either has no
    /// paragraph texts (tests, synthetic geometry), otherwise a diff of their texts.</summary>
    public static AnchorMap Between(LayoutGeometry from, LayoutGeometry to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (ReferenceEquals(from, to) || from.SnapshotVersion == to.SnapshotVersion) return Identity;
        var a = from.ParagraphTexts;
        var b = to.ParagraphTexts;
        if (a.Count == 0 || b.Count == 0 || a.Count != from.ParagraphCount || b.Count != to.ParagraphCount) return Identity;
        return new AnchorMap(a, b);
    }

    /// <summary>Where <paramref name="anchor"/> (in the "from" version) is in the "to" version. Allocation-free.</summary>
    public DocumentAnchor Map(DocumentAnchor anchor)
    {
        if (IsIdentity) return anchor;
        var nF = _from.Count;
        var nT = _to.Count;
        if (nT == 0) return DocumentAnchor.Start;
        if (nF == 0) return DocumentAnchor.Start;
        var p = Math.Clamp(anchor.ParagraphIndex, 0, nF - 1);
        var off = Math.Clamp(anchor.CharOffset, 0, _from[p].Length);
        if (p < _prefix) return new DocumentAnchor(p, off);
        if (p >= nF - _suffix) return new DocumentAnchor(p + (nT - nF), off);

        var t = _matched[p - _middleFrom];
        if (t >= 0) return new DocumentAnchor(t, off);

        var r = FindRegion(p);
        ref readonly var region = ref _regions[r];
        if (p < region.FromStart + region.ParagraphPrefix) return new DocumentAnchor(region.ToStart + (p - region.FromStart), off);
        if (p >= region.FromEnd - region.ParagraphSuffix) return new DocumentAnchor(region.ToEnd - (region.FromEnd - p), off);

        // Character level inside the region's changed paragraphs.
        var fs = region.FromStart + region.ParagraphPrefix;
        var ts = region.ToStart + region.ParagraphPrefix;
        var te = region.ToEnd - region.ParagraphSuffix;
        var g = _cumFrom[p] - _cumFrom[fs] + off;
        long mapped;
        if (g < region.CharPrefix) mapped = g;
        else if (g >= region.FromLength - region.CharSuffix) mapped = g + (region.ToLength - region.FromLength);
        else mapped = region.CharPrefix; // inside replaced/deleted text: where the change starts

        if (te <= ts)
        {
            // The whole changed part was deleted: the text after it (or the end of the text before it).
            return te < nT ? new DocumentAnchor(te, 0) : new DocumentAnchor(nT - 1, _to[nT - 1].Length);
        }

        var absolute = _cumTo[ts] + mapped;
        var k = LastAtOrBefore(_cumTo, ts, te, absolute);
        var offset = (int)Math.Min(absolute - _cumTo[k], _to[k].Length);
        return new DocumentAnchor(k, offset);
    }

    private readonly record struct Region(
        int FromStart, int FromEnd, int ToStart, int ToEnd,
        int ParagraphPrefix, int ParagraphSuffix,
        long FromLength, long ToLength, long CharPrefix, long CharSuffix);

    private Region MakeRegion(int fs, int fe, int ts, int te)
    {
        var max = Math.Min(fe - fs, te - ts);
        var pp = 0;
        while (pp < max && string.Equals(_from[fs + pp], _to[ts + pp], StringComparison.Ordinal)) pp++;
        var ps = 0;
        while (ps < max - pp && string.Equals(_from[fe - 1 - ps], _to[te - 1 - ps], StringComparison.Ordinal)) ps++;

        var ifs = fs + pp;
        var ife = fe - ps;
        var its = ts + pp;
        var ite = te - ps;
        var fromLength = JoinedLength(_cumFrom, ifs, ife);
        var toLength = JoinedLength(_cumTo, its, ite);
        var limit = Math.Min(fromLength, toLength);

        var a = new Cursor(_from, ifs, ife, forward: true);
        var b = new Cursor(_to, its, ite, forward: true);
        long cp = 0;
        while (cp < limit && a.Next() == b.Next()) cp++;

        var c = new Cursor(_from, ifs, ife, forward: false);
        var d = new Cursor(_to, its, ite, forward: false);
        long cs = 0;
        while (cs < limit - cp && c.Next() == d.Next()) cs++;

        return new Region(fs, fe, ts, te, pp, ps, fromLength, toLength, cp, cs);
    }

    private int FindRegion(int p)
    {
        int lo = 0, hi = _regions.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >>> 1;
            if (_regions[mid].FromStart <= p) lo = mid; else hi = mid - 1;
        }

        return lo;
    }

    private static long JoinedLength(long[] cum, int start, int end) => end > start ? cum[end] - cum[start] - 1 : 0;

    private static long[] Cumulative(IReadOnlyList<string> texts)
    {
        var cum = new long[texts.Count + 1];
        for (var i = 0; i < texts.Count; i++) cum[i + 1] = cum[i] + texts[i].Length + 1;
        return cum;
    }

    /// <summary>Index k in [start, end) of the last paragraph whose cumulative start is at or before value.</summary>
    private static int LastAtOrBefore(long[] cum, int start, int end, long value)
    {
        int lo = start, hi = end - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) >>> 1;
            if (cum[mid] <= value) lo = mid; else hi = mid - 1;
        }

        return lo;
    }

    /// <summary>Pairs (from, to) of paragraphs whose text occurs exactly once in each middle, kept only where both indices
    /// increase together (longest increasing subsequence), in order.</summary>
    private static List<(int From, int To)> MatchUnique(IReadOnlyList<string> from, int fs, int fe, IReadOnlyList<string> to, int ts, int te)
    {
        var result = new List<(int, int)>();
        if (fe <= fs || te <= ts) return result;

        var counts = new Dictionary<string, (int FromCount, int FromIndex, int ToCount, int ToIndex)>(StringComparer.Ordinal);
        for (var i = fs; i < fe; i++)
        {
            counts.TryGetValue(from[i], out var c);
            counts[from[i]] = (c.FromCount + 1, i, c.ToCount, c.ToIndex);
        }

        for (var j = ts; j < te; j++)
        {
            if (!counts.TryGetValue(to[j], out var c)) continue;
            counts[to[j]] = (c.FromCount, c.FromIndex, c.ToCount + 1, j);
        }

        var candidates = new List<(int From, int To)>();
        for (var i = fs; i < fe; i++)
        {
            var c = counts[from[i]];
            if (c.FromCount == 1 && c.ToCount == 1) candidates.Add((i, c.ToIndex));
        }

        if (candidates.Count == 0) return result;

        // Longest increasing subsequence on To (candidates are already in From order).
        var tails = new int[candidates.Count];
        var previous = new int[candidates.Count];
        var length = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            int lo = 0, hi = length;
            while (lo < hi)
            {
                var mid = (lo + hi) >>> 1;
                if (candidates[tails[mid]].To < candidates[i].To) lo = mid + 1; else hi = mid;
            }

            previous[i] = lo > 0 ? tails[lo - 1] : -1;
            tails[lo] = i;
            if (lo == length) length++;
        }

        var chain = new (int, int)[length];
        for (int k = length - 1, i = tails[length - 1]; k >= 0; k--, i = previous[i]) chain[k] = candidates[i];
        result.AddRange(chain);
        return result;
    }

    /// <summary>Walks the characters of paragraphs [start, end) joined with '\n', forwards or backwards.</summary>
    private struct Cursor
    {
        private readonly IReadOnlyList<string> _texts;
        private readonly int _start;
        private readonly int _end;
        private readonly bool _forward;
        private int _paragraph;
        private int _index;

        public Cursor(IReadOnlyList<string> texts, int start, int end, bool forward)
        {
            _texts = texts;
            _start = start;
            _end = end;
            _forward = forward;
            _paragraph = forward ? start : end - 1;
            _index = forward ? 0 : (end > start ? texts[end - 1].Length - 1 : -1);
        }

        /// <summary>The next character; '\n' between paragraphs; '\0' past the end (callers stop at the joined length).</summary>
        public char Next()
        {
            if (_forward)
            {
                if (_paragraph >= _end) return '\0';
                var text = _texts[_paragraph];
                if (_index < text.Length) return text[_index++];
                _paragraph++;
                _index = 0;
                return '\n';
            }

            if (_paragraph < _start) return '\0';
            if (_index >= 0) return _texts[_paragraph][_index--];
            _paragraph--;
            _index = _paragraph >= _start ? _texts[_paragraph].Length - 1 : -1;
            return '\n';
        }
    }
}

/// <summary>
/// What sits on the reading line, in layout-independent terms, and where that is in another layout (ARCHITECTURE §2.4
/// "Layout change"). The reading point is tied to the nearest text line: its start anchor plus either the fraction down
/// that line or, in a gap between lines, the signed distance from the line's edge. Mapping it through an
/// <see cref="AnchorMap"/> onto a new geometry keeps that text on the reading line whatever was edited or reflowed
/// elsewhere. Pure and allocation-free.
/// </summary>
public static class LayoutMapping
{
    /// <summary>The position (top of text area, document Y) in <paramref name="to"/> that shows what
    /// <paramref name="position"/> showed on the reading line in <paramref name="from"/>. NaN when either has no lines.</summary>
    public static double MapPosition(LayoutGeometry from, LayoutGeometry to, AnchorMap map, double position, double readingLineY)
    {
        if (from.LineCount == 0 || to.LineCount == 0) return double.NaN;
        var y = position + readingLineY;
        var i = from.FindLineAt(y);
        var top = from.LineTops[i];
        var height = from.LineHeights[i];
        double fraction, outside;
        if (y < top)
        {
            fraction = 0;
            outside = y - top;
        }
        else if (y < top + height || height <= 0)
        {
            fraction = height > 0 ? (y - top) / height : 0;
            outside = 0;
        }
        else
        {
            // In the gap below line i: tie to whichever line edge is nearer, so the nearest text stays put.
            var below = y - (top + height);
            if (i + 1 < from.LineCount && from.LineTops[i + 1] - y < below)
            {
                i++;
                fraction = 0;
                outside = y - from.LineTops[i];
            }
            else
            {
                fraction = 1;
                outside = below;
            }
        }

        var anchor = new DocumentAnchor(from.LineParagraphs[i], from.LineCharOffsets[i]);
        var k = Math.Max(0, to.FindLineForAnchor(map.Map(anchor)));
        return to.LineTops[k] + (fraction * to.LineHeights[k]) + outside - readingLineY;
    }
}
