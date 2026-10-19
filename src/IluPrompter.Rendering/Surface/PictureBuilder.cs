using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Theme;

namespace IluPrompter.Rendering;

/// <summary>What a picture is built from: read by the builder at build time, so intermediate versions are skipped.</summary>
internal readonly record struct PictureInputs(PromptDocumentSnapshot Snapshot, PrompterSettings Settings, OutputGeometry Output)
{
    public TextArea Area => TypographyMath.ComputeTextArea(Output, Settings.Margins, Settings.Text, Settings.Output);

    public string LayoutKey => SurfaceLayout.ComputeLayoutKey(Snapshot.Version, Area, Settings.Text, Settings.Output);

    public string ContentKey => SurfaceLayout.ComputeContentKey(LayoutKey, Settings.Text);

    /// <summary>Everything that styles a paragraph, but not the script itself: equal keys = an unchanged paragraph lays out
    /// and draws exactly the same.</summary>
    public string StyleKey =>
        SurfaceLayout.ComputeContentKey(SurfaceLayout.ComputeLayoutKey(0, Area, Settings.Text, Settings.Output), Settings.Text)
        + "|" + Snapshot.EditorBaseFontSize.ToString("R", CultureInfo.InvariantCulture) + "|" + Settings.Editor.SectionMarker;
}

/// <summary>
/// Lays a snapshot out and turns it into a frozen <see cref="PromptPicture"/>. Owns a read-only RichTextBox, so it lives on
/// one WPF thread (the layout thread in the app; the calling thread for an inline source). The FlowDocument, its layout and
/// the geometry tables are exactly what the surface used to build itself (factory, unconstrained height at the text width,
/// Ideal formatting, geometry builder); the laid-out visuals' drawings are then frozen and grouped per paragraph.
/// <para>
/// <b>Script edits are patched.</b> When only the script changed (same style), the snapshot XAML is split into paragraphs
/// (<see cref="XamlParagraphs"/>) and compared with the document on show: only the changed paragraphs are built (through
/// the factory, from just their XAML) and swapped into the live FlowDocument, WPF relays out from there, and every
/// unchanged paragraph keeps its frozen drawing, so the surfaces keep its visual too. A new style, a new size, or anything
/// unexpected lays the whole script out again.
/// </para>
/// </summary>
internal sealed class PictureBuilder
{
    private const string FrameType = "FlowDocumentView";

    /// <summary>Above this many changed paragraphs a full layout is simpler and about as quick.</summary>
    private const int MaxPatchedParagraphs = 64;

    private readonly IOutputDocumentFactory _factory;
    private readonly ILayoutGeometryBuilder _geometryBuilder;
    private readonly DocumentNavigator _navigator = new();
    private readonly RichTextBox _host = new();
    private readonly List<(Drawing Drawing, double X, double Y, Matrix? Transform, int Bucket)> _captured = new(2048);
    private Live? _live;

    public PictureBuilder(IOutputDocumentFactory factory, ILayoutGeometryBuilder geometryBuilder)
    {
        _factory = factory;
        _geometryBuilder = geometryBuilder;
        ConfigureHost(_host);
    }

    /// <summary>The host of the last build (tests inspect its layout).</summary>
    internal RichTextBox Host => _host;

    /// <summary>The document on show and what it was built from, for the next patch.</summary>
    private sealed class Live
    {
        public required FlowDocument Document { get; init; }
        public required string StyleKey { get; init; }
        public required string RootStart { get; init; }
        public required string[] Paragraphs { get; set; }
        public required string?[] SectionNames { get; set; }
        public PictureBlock?[] Blocks { get; set; } = [];   // by paragraph index; [count] = END
    }

    public PromptPicture Build(PictureInputs inputs, PromptPicture? previous)
    {
        try
        {
            return BuildCore(inputs, previous);
        }
        catch
        {
            _live = null; // whatever state the live document is in, the next build starts from scratch
            throw;
        }
    }

    private PromptPicture BuildCore(PictureInputs inputs, PromptPicture? previous)
    {
        var total = Stopwatch.StartNew();
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var area = inputs.Area;
        var snapshot = inputs.Snapshot;
        var style = BuildStyle(area, inputs.Settings);
        var styleKey = inputs.StyleKey;

        var step = Stopwatch.StartNew();
        var split = XamlParagraphs.TrySplit(snapshot.FlowDocumentXaml, out var rootStart, out var paragraphs) && paragraphs.Length == snapshot.ParagraphCount;
        var names = split ? SectionNames(snapshot, paragraphs.Length) : [];

        // Patch the live document if only the script changed; otherwise build a new one.
        var patched = -1;
        Func<int, PictureBlock?>? reuse = null;
        if (split && _live is { } live && string.Equals(live.StyleKey, styleKey, StringComparison.Ordinal)
            && TryPatch(live, rootStart, paragraphs, names, snapshot, style, out patched, out reuse))
        {
            live.Paragraphs = paragraphs;
            live.SectionNames = names;
        }
        else
        {
            patched = -1;
            reuse = null;
            var document = _factory.Create(snapshot, style);
            _host.Document = document;
            _live = split ? new Live { Document = document, StyleKey = styleKey, RootStart = rootStart, Paragraphs = paragraphs, SectionNames = names } : null;
        }

        var tDocument = step.Elapsed.TotalMilliseconds;
        var aDocument = GC.GetAllocatedBytesForCurrentThread() - bytes;

        step.Restart();
        _host.Width = area.Width;
        _host.Measure(new Size(area.Width, double.PositiveInfinity));
        _host.Arrange(new Rect(0, 0, area.Width, _host.DesiredSize.Height));
        _host.UpdateLayout();
        var tLayout = step.Elapsed.TotalMilliseconds;
        var aLayout = GC.GetAllocatedBytesForCurrentThread() - bytes - aDocument;

        step.Restart();
        var geometry = _geometryBuilder.Build(_host.Document, new GeometryBuildOptions(inputs.LayoutKey, snapshot, area.BaseLineHeight));
        var tGeometry = step.Elapsed.TotalMilliseconds;
        var aGeometry = GC.GetAllocatedBytesForCurrentThread() - bytes - aDocument - aLayout;

        step.Restart();
        var blocks = Capture(geometry, reuse);
        if (_live is not null) _live.Blocks = ByParagraph(blocks, geometry.ParagraphCount);
        var tPicture = step.Elapsed.TotalMilliseconds;
        var aPicture = GC.GetAllocatedBytesForCurrentThread() - bytes - aDocument - aLayout - aGeometry;

        var map = previous is null ? null : AnchorMap.Between(geometry, previous.Geometry);
        return new PromptPicture(geometry, inputs.ContentKey, area, inputs.Output, blocks)
        {
            PreviousGeometry = previous?.Geometry,
            MapToPrevious = map,
            PatchedParagraphs = patched,
            Timings = new PictureTimings(tDocument, tLayout, tGeometry, tPicture, total.Elapsed.TotalMilliseconds),
            Allocated = new PictureTimings(aDocument / 1048576.0, aLayout / 1048576.0, aGeometry / 1048576.0, aPicture / 1048576.0,
                (GC.GetAllocatedBytesForCurrentThread() - bytes) / 1048576.0),
        };
    }

    // ---- patching the live document ----------------------------------------------------------------------------------

    private bool TryPatch(Live live, string rootStart, string[] paragraphs, string?[] names, PromptDocumentSnapshot snapshot,
        OutputTextStyle style, out int changed, out Func<int, PictureBlock?>? reuse)
    {
        changed = 0;
        reuse = null;
        if (!string.Equals(rootStart, live.RootStart, StringComparison.Ordinal)) return false;

        var old = live.Paragraphs;
        var oldNames = live.SectionNames;
        var nOld = old.Length;
        var nNew = paragraphs.Length;
        var min = Math.Min(nOld, nNew);
        var prefix = 0;
        while (prefix < min && Same(prefix, prefix)) prefix++;
        var suffix = 0;
        while (suffix < min - prefix && Same(nOld - 1 - suffix, nNew - 1 - suffix)) suffix++;
        // Which old paragraph each new one is (-1: changed): the equal start and end, and inside, paragraphs that are
        // unique in both versions and in the same order (so edits in two places patch two small regions, not all between).
        var from = new int[nNew];
        Array.Fill(from, -1);
        for (var i = 0; i < prefix; i++) from[i] = i;
        for (var k = 0; k < suffix; k++) from[nNew - 1 - k] = nOld - 1 - k;
        foreach (var (o, n) in MatchUnique(old, oldNames, prefix, nOld - suffix, paragraphs, names, prefix, nNew - suffix)) from[n] = o;

        var kept = new bool[nOld];
        var changedCount = 0;
        for (var j = 0; j < nNew; j++)
        {
            if (from[j] >= 0) kept[from[j]] = true;
            else changedCount++;
        }

        var removedCount = 0;
        for (var i = 0; i < nOld; i++)
        {
            if (!kept[i]) removedCount++;
        }

        if (changedCount + removedCount > MaxPatchedParagraphs) return false;

        var oldParagraphs = _navigator.GetParagraphs(live.Document);
        if (oldParagraphs.Count != nOld || live.Document.Blocks.LastBlock is not { } end || !DocumentNavigator.IsGenerated(end)) return false;

        var created = new List<Paragraph>(changedCount);
        if (changedCount > 0)
        {
            // Build just the changed paragraphs, with the factory, from their own XAML under the same root.
            var name = RootName(rootStart);
            var xaml = new StringBuilder(rootStart.Length + name.Length + 3 + (changedCount * 256)).Append(rootStart);
            var sections = new List<SectionInfo>();
            var position = 0;
            for (var j = 0; j < nNew; j++)
            {
                if (from[j] >= 0) continue;
                if (names[j] is { } section) sections.Add(new SectionInfo(sections.Count, section, position));
                xaml.Append(paragraphs[j]);
                position++;
            }

            xaml.Append("</").Append(name).Append('>');
            var part = new PromptDocumentSnapshot(snapshot.Version, xaml.ToString(), snapshot.EditorBaseFontSize, changedCount, sections, 0);
            var built = _factory.Create(part, style);
            var builtParagraphs = _navigator.GetParagraphs(built);
            if (builtParagraphs.Count != changedCount) return false;
            foreach (var p in builtParagraphs)
            {
                if (p.Parent != built) return false;
                created.Add(p);
            }

            foreach (var p in created) built.Blocks.Remove(p);
        }

        // Kept paragraphs stay in order; drop the rest, then put each new paragraph after the one before it.
        for (var i = 0; i < nOld; i++)
        {
            if (!kept[i]) live.Document.Blocks.Remove(oldParagraphs[i]);
        }

        Block? previous = null;
        var next = 0;
        for (var j = 0; j < nNew; j++)
        {
            if (from[j] >= 0)
            {
                previous = oldParagraphs[from[j]];
                continue;
            }

            var p = created[next++];
            if (previous is not null) live.Document.Blocks.InsertAfter(previous, p);
            else live.Document.Blocks.InsertBefore(live.Document.Blocks.FirstBlock, p);
            previous = p;
        }

        changed = changedCount;
        var oldBlocks = live.Blocks;
        reuse = bucket =>
        {
            var o = bucket < 0 ? nOld : (bucket < nNew ? from[bucket] : -1);
            return o >= 0 && o < oldBlocks.Length ? oldBlocks[o] : null;
        };
        return true;

        bool Same(int a, int b) => string.Equals(old[a], paragraphs[b], StringComparison.Ordinal) && string.Equals(oldNames[a], names[b], StringComparison.Ordinal);
    }

    /// <summary>Pairs (old, new) of paragraphs inside the changed middle whose XAML (and section name) occurs exactly once
    /// in each version, kept only where both indices increase together (longest increasing subsequence).</summary>
    private static List<(int Old, int New)> MatchUnique(string[] old, string?[] oldNames, int os, int oe, string[] neu, string?[] newNames, int ns, int ne)
    {
        var result = new List<(int, int)>();
        if (oe <= os || ne <= ns) return result;
        static string Key(string xaml, string? name) => name is null ? xaml : xaml + "\u0001" + name;

        var counts = new Dictionary<string, (int OldCount, int OldIndex, int NewCount, int NewIndex)>(StringComparer.Ordinal);
        for (var i = os; i < oe; i++)
        {
            var key = Key(old[i], oldNames[i]);
            counts.TryGetValue(key, out var c);
            counts[key] = (c.OldCount + 1, i, c.NewCount, c.NewIndex);
        }

        for (var j = ns; j < ne; j++)
        {
            var key = Key(neu[j], newNames[j]);
            if (!counts.TryGetValue(key, out var c)) continue;
            counts[key] = (c.OldCount, c.OldIndex, c.NewCount + 1, j);
        }

        var candidates = new List<(int Old, int New)>();
        for (var i = os; i < oe; i++)
        {
            var c = counts[Key(old[i], oldNames[i])];
            if (c.OldCount == 1 && c.NewCount == 1) candidates.Add((i, c.NewIndex));
        }

        if (candidates.Count == 0) return result;
        var tails = new int[candidates.Count];
        var previous = new int[candidates.Count];
        var length = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            int lo = 0, hi = length;
            while (lo < hi)
            {
                var mid = (lo + hi) >>> 1;
                if (candidates[tails[mid]].New < candidates[i].New) lo = mid + 1; else hi = mid;
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

    private static string?[] SectionNames(PromptDocumentSnapshot snapshot, int count)
    {
        var names = new string?[count];
        foreach (var section in snapshot.Sections)
        {
            if (section.ParagraphIndex >= 0 && section.ParagraphIndex < count) names[section.ParagraphIndex] = section.Name;
        }

        return names;
    }

    private static string RootName(string rootStart)
    {
        var end = 1;
        while (end < rootStart.Length && !char.IsWhiteSpace(rootStart[end]) && rootStart[end] is not '>' and not '/') end++;
        return rootStart[1..end];
    }

    private static PictureBlock?[] ByParagraph(PictureBlock[] blocks, int paragraphCount)
    {
        var byParagraph = new PictureBlock?[paragraphCount + 1];
        foreach (var block in blocks)
        {
            var index = block.Paragraph < 0 ? paragraphCount : block.Paragraph;
            if (index <= paragraphCount) byParagraph[index] = block;
        }

        return byParagraph;
    }

    // ---- capture: laid-out visuals -> frozen drawings, one group per paragraph ----------------------------------------

    private PictureBlock[] Capture(LayoutGeometry geometry, Func<int, PictureBlock?>? reuse)
    {
        _captured.Clear();
        var tops = geometry.ParagraphTops;
        var count = tops.Count;
        var reused = new PictureBlock?[count + 1];
        if (reuse is not null)
        {
            for (var b = 0; b < count; b++) reused[b] = reuse(b);
            reused[count] = reuse(-1);
        }

        var frame = FindFrame(_host);
        if (frame is not null) Walk(frame, 0, 0, null, geometry, reused);

        // Every drawing went to the paragraph its origin falls in; everything at or below END's top is the END block.
        var buckets = new List<(Drawing, double, double, Matrix?)>?[count + 1];
        foreach (var (drawing, x, y, transform, bucket) in _captured)
        {
            (buckets[bucket] ??= new List<(Drawing, double, double, Matrix?)>(4)).Add((drawing, x, y, transform));
        }

        var blocks = new List<PictureBlock>(count + 1);
        for (var b = 0; b <= count; b++)
        {
            var top = b < count ? tops[b] : geometry.EndTop;
            var bottom = b + 1 < count ? Math.Max(top, tops[b + 1]) : Math.Max(top, geometry.DocumentHeight);
            var paragraph = b < count ? b : -1;
            if (reused[b] is { } kept)
            {
                blocks.Add(new PictureBlock(kept.Drawing, top, Math.Max(bottom, top + (kept.Bottom - kept.Top)), paragraph));
                continue;
            }

            var items = buckets[b];
            if (items is null) continue;
            var group = new DrawingGroup();
            foreach (var (drawing, x, y, transform) in items)
            {
                var child = new DrawingGroup();
                child.Children.Add(drawing);
                if (transform is { } m)
                {
                    m.Translate(0, -top);
                    child.Transform = new MatrixTransform(m);
                }
                else
                {
                    child.Transform = new TranslateTransform(x, y - top);
                }

                group.Children.Add(child);
                bottom = Math.Max(bottom, y + 1);
            }

            if (!group.CanFreeze) continue; // something live inside (never for the factory's documents): leave it out
            group.Freeze();
            blocks.Add(new PictureBlock(group, top, bottom, paragraph));
        }

        blocks.Sort(static (a, b) => a.Top.CompareTo(b.Top));
        return blocks.ToArray();
    }

    /// <summary>Depth-first: each visual's own drawing at its offset from the frame, unless its paragraph keeps its old
    /// drawing. Transforms other than offsets (none in practice) are carried as a matrix.</summary>
    private void Walk(Visual node, double x, double y, Matrix? matrix, LayoutGeometry geometry, PictureBlock?[] reused)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(node, i) is not Visual child) continue;
            var offset = VisualTreeHelper.GetOffset(child);
            var transform = VisualTreeHelper.GetTransform(child);
            Matrix? childMatrix = matrix;
            double cx = x + offset.X, cy = y + offset.Y;
            if (matrix is not null || (transform is not null && !transform.Value.IsIdentity))
            {
                // Visual space -> parent: transform, then offset; then the parent's own matrix.
                var m = transform?.Value ?? Matrix.Identity;
                m.Translate(offset.X, offset.Y);
                m.Append(matrix ?? new Matrix(1, 0, 0, 1, x, y));
                childMatrix = m;
                cx = m.OffsetX;
                cy = m.OffsetY;
            }

            var bucket = Bucket(geometry, cy);
            if (reused[bucket] is null && VisualTreeHelper.GetDrawing(child) is { Children.Count: > 0 } drawing)
            {
                Drawing content = drawing;
                var clip = VisualTreeHelper.GetClip(child);
                var opacity = VisualTreeHelper.GetOpacity(child);
                if (clip is not null || opacity < 1)
                {
                    var wrapped = new DrawingGroup { ClipGeometry = clip, Opacity = opacity };
                    wrapped.Children.Add(drawing);
                    content = wrapped;
                }

                _captured.Add((content, cx, cy, childMatrix, bucket));
            }

            Walk(child, cx, cy, childMatrix, geometry, reused);
        }
    }

    /// <summary>The paragraph a visual at <paramref name="y"/> belongs to; the paragraph count for the END block.</summary>
    private static int Bucket(LayoutGeometry geometry, double y)
    {
        var count = geometry.ParagraphCount;
        if (geometry.EndTop > 0 && y >= geometry.EndTop - 0.5) return count;
        if (count == 0) return 0;
        return Math.Max(0, LastAtOrBefore(geometry.ParagraphTops, y + 0.5));
    }

    private static Visual? FindFrame(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (string.Equals(child.GetType().Name, FrameType, StringComparison.Ordinal)) return child as Visual;
            var found = FindFrame(child);
            if (found is not null) return found;
        }

        return null;
    }

    private static int LastAtOrBefore(IReadOnlyList<double> sorted, double value)
    {
        int lo = 0, hi = sorted.Count - 1, best = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid] <= value) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
        }

        return best;
    }

    // ---- host and style (unchanged from the surface's own build) --------------------------------------------------

    private static void ConfigureHost(RichTextBox host)
    {
        host.IsReadOnly = true;
        host.IsReadOnlyCaretVisible = false;
        host.Focusable = false;
        host.IsHitTestVisible = false;
        host.IsTabStop = false;
        host.IsUndoEnabled = false;
        host.IsDocumentEnabled = false;
        host.AllowDrop = false;
        host.BorderThickness = new Thickness(0);
        host.Padding = new Thickness(0);
        host.Margin = new Thickness(0);
        host.Background = Brushes.Transparent;
        host.FocusVisualStyle = null;
        host.IsInactiveSelectionHighlightEnabled = false;
        host.SelectionOpacity = 0;
        host.CaretBrush = Brushes.Transparent;
        host.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        host.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        host.HorizontalAlignment = HorizontalAlignment.Left;
        host.VerticalAlignment = VerticalAlignment.Top;
        host.UndoLimit = 0;
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(host, TextRenderingMode.Grayscale);
        SpellCheck.SetIsEnabled(host, false);
    }

    internal static OutputTextStyle BuildStyle(TextArea area, PrompterSettings settings)
    {
        var text = settings.Text;
        var highlights = new Dictionary<Color, Color>();
        foreach (var option in ThemeResources.Highlights)
        {
            if (option.InkToken is not null && ThemeResources.TryGetColor(option.Token, out var bg) && ThemeResources.TryGetColor(option.InkToken, out var ink))
            {
                highlights[bg] = ink;
            }
        }

        return new OutputTextStyle(
            ThemeResources.ScriptFont(text.FontFace),
            FontWeight.FromOpenTypeWeight(Math.Clamp(text.FontWeight, 1, 999)),
            area.BaseFontSize,
            text.LineSpacing,
            text.ParagraphGapLines,
            settings.Output.Alignment == TextAlignmentSetting.Centre ? TextAlignment.Center : TextAlignment.Left,
            ThemeResources.TryGetColor(text.DefaultInk, out var defaultInk) ? defaultInk : ThemeResources.GetColor("ink-white"),
            ThemeResources.GetColor("ink-grey"),
            ThemeResources.GetColor("end-marker"),
            ThemeResources.GetColor("line-strong"),
            settings.Output.ShowSectionNames,
            settings.Editor.SectionMarker,
            highlights);
    }
}
