using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace IluPrompter.Rendering;

/// <summary>
/// Shows a <see cref="PromptPicture"/> on the surface's thread: one <see cref="DrawingVisual"/> per paragraph block, in
/// document coordinates (the surface moves the whole presenter with its translate transform). Only the blocks from two
/// screens above to three below the text area are in the visual tree, so installing a new picture sends the render
/// thread a few dozen paragraphs, not the script; a paragraph whose frozen drawing is unchanged keeps its visual (only its
/// offset moves). The steady per-frame check is two comparisons and allocates nothing; visuals for the rest of the
/// script are made in the background right after a picture is installed.
/// </summary>
internal sealed class PicturePresenter : FrameworkElement
{
    private readonly VisualCollection _children;
    private readonly Action _prepareAction;
    private PromptPicture? _picture;
    private BlockVisual?[] _visuals = [];
    private int _start;
    private int _end;
    private bool _preparePending;
    private TextHintingMode _hinting = TextHintingMode.Animated;
    private double _keepTop = double.NaN;
    private double _keepBottom = double.NaN;

    public PicturePresenter()
    {
        _children = new VisualCollection(this);
        _prepareAction = PrepareRest;
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(this, TextHintingMode.Animated);
    }

    public PromptPicture? Picture => _picture;

    /// <summary>Blocks currently in the visual tree (tests, diagnostics).</summary>
    public int AttachedCount => _children.Count;

    /// <summary>Visuals carried over from the previous picture by the last <see cref="SetPicture"/> (tests, diagnostics).</summary>
    public int ReusedCount { get; private set; }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index) => _children[index];

    /// <summary>Install a picture and attach the blocks around <paramref name="position"/>.</summary>
    public void SetPicture(PromptPicture? picture, double position, double viewportHeight)
    {
        if (ReferenceEquals(picture, _picture)) return;

        // Carry over visuals whose drawing is unchanged (the same frozen object).
        var old = _visuals;
        Dictionary<Drawing, BlockVisual>? reusable = null;
        foreach (var v in old)
        {
            if (v is null) continue;
            v.Index = -1;
            if (picture is not null) (reusable ??= new Dictionary<Drawing, BlockVisual>(old.Length, ReferenceEqualityComparer.Instance))[v.Drawing] = v;
        }

        _picture = picture;
        _visuals = picture is null ? [] : new BlockVisual?[picture.Blocks.Count];
        ReusedCount = 0;
        if (reusable is not null)
        {
            var blocks = picture!.Blocks;
            for (var i = 0; i < blocks.Count; i++)
            {
                if (!reusable.Remove(blocks[i].Drawing, out var v)) continue;
                v.Index = i;
                var offset = new Vector(0, blocks[i].Top);
                if (v.Offset != offset) v.Offset = offset;
                _visuals[i] = v;
                ReusedCount++;
            }
        }

        // Detach what is not in the new picture's band; keep carried-over visuals that stay attached where they are.
        int start = 0, end = 0;
        if (picture is not null) Band(picture, position, viewportHeight, out start, out end);
        for (var c = _children.Count - 1; c >= 0; c--)
        {
            var v = (BlockVisual)_children[c];
            if (v.Index < start || v.Index >= end)
            {
                _children.RemoveAt(c);
                v.Attached = false;
            }
        }

        for (var i = start; i < end; i++) Attach(i);
        _start = start;
        _end = end;

        if (picture is not null && !_preparePending)
        {
            _preparePending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, _prepareAction);
        }

        InvalidateMeasure();
    }

    /// <summary>Per frame: keep the blocks from one screen above to two below the text area attached (attaching a wider
    /// band, so this does work about once per screen of scrolling). Allocation-free once the visuals are prepared.</summary>
    public void UpdateViewport(double position, double viewportHeight)
    {
        var picture = _picture;
        if (picture is null) return;
        var h = Math.Max(1, viewportHeight);
        if (position - h >= _keepTop && position + (2 * h) <= _keepBottom) return;

        Band(picture, position, viewportHeight, out var start, out var end);
        for (var i = _start; i < _end; i++)
        {
            if (i >= start && i < end) continue;
            var v = _visuals[i]!;
            _children.Remove(v);
            v.Attached = false;
        }

        for (var i = start; i < end; i++) Attach(i);
        _start = start;
        _end = end;
    }

    /// <summary>Animated while moving, Fixed when still (the surface snaps to whole pixels then).</summary>
    public void SetHinting(TextHintingMode mode)
    {
        if (mode == _hinting) return;
        _hinting = mode;
        TextOptions.SetTextHintingMode(this, mode);
        for (var i = _start; i < _end; i++) _visuals[i]?.SetHinting(mode);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var picture = _picture;
        return picture is null ? default : new Size(picture.Area.Width, Math.Max(0, picture.Geometry.DocumentHeight));
    }

    private void Band(PromptPicture picture, double position, double viewportHeight, out int start, out int end)
    {
        var h = Math.Max(1, viewportHeight);
        _keepTop = position - (2 * h);
        _keepBottom = position + (3 * h);
        picture.FindBlocks(_keepTop, _keepBottom, out start, out end);
    }

    private void Attach(int index)
    {
        var visual = _visuals[index];
        if (visual is null)
        {
            var block = _picture!.Blocks[index];
            visual = new BlockVisual(block.Drawing, block.Top) { Index = index };
            _visuals[index] = visual;
        }

        visual.SetHinting(_hinting);
        if (visual.Attached) return;
        _children.Add(visual);
        visual.Attached = true;
    }

    private void PrepareRest()
    {
        _preparePending = false;
        var picture = _picture;
        if (picture is null) return;
        for (var i = 0; i < _visuals.Length; i++)
        {
            if (_visuals[i] is not null) continue;
            var block = picture.Blocks[i];
            _visuals[i] = new BlockVisual(block.Drawing, block.Top) { Index = i };
        }
    }

    /// <summary>A paragraph's frozen drawing at its top. Text options are set on the visual itself (a plain visual does
    /// not inherit them from the element).</summary>
    private sealed class BlockVisual : DrawingVisual
    {
        public BlockVisual(Drawing drawing, double top)
        {
            Drawing = drawing;
            Offset = new Vector(0, top);
            VisualTextRenderingMode = TextRenderingMode.Grayscale;
            VisualTextHintingMode = TextHintingMode.Animated;
            using var context = RenderOpen();
            context.DrawDrawing(drawing);
        }

        public new Drawing Drawing { get; }

        public int Index { get; set; }

        public bool Attached { get; set; }

        public void SetHinting(TextHintingMode mode)
        {
            if (VisualTextHintingMode != mode) VisualTextHintingMode = mode;
        }
    }
}
