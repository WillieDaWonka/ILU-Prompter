using CommunityToolkit.Mvvm.ComponentModel;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// The margin editor (DESIGN.md D7 and Settings › Output): one number box per edge in pixels of the output display,
/// and the link toggle. All arithmetic (clamping, linking, fractions) is <see cref="MarginMath"/> in Core; this
/// class only shows its result. Settings store fractions, so values survive a change of output resolution.
/// </summary>
public sealed class MarginEditorViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IOutputSurface _output;
    private readonly IUiThread _ui;
    private bool _refreshing;
    private double _left;
    private double _top;
    private double _right;
    private double _bottom;
    private bool _isLinked;
    private PixelSize _outputSize = new(1920, 1080);
    private MarginSettings? _lastMargins;

    public MarginEditorViewModel(ISettingsService settings, IOutputSurface output, IUiThread ui)
    {
        _settings = settings;
        _output = output;
        _ui = ui;
        settings.Changed += (_, e) =>
        {
            if (!ReferenceEquals(e.Old.Margins, e.New.Margins) || !ReferenceEquals(e.Old.Output, e.New.Output))
                _ui.Post(Refresh);
        };
        output.StateChanged += (_, _) => _ui.Post(Refresh);
        Refresh();
    }

    public double Left { get => _left; set => SetEdge(MarginEdge.Left, value, ref _left); }

    public double Top { get => _top; set => SetEdge(MarginEdge.Top, value, ref _top); }

    public double Right { get => _right; set => SetEdge(MarginEdge.Right, value, ref _right); }

    public double Bottom { get => _bottom; set => SetEdge(MarginEdge.Bottom, value, ref _bottom); }

    public bool IsLinked
    {
        get => _isLinked;
        set
        {
            if (_isLinked == value) return;
            _isLinked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinkCaption));
            if (_refreshing) return;
            var size = _outputSize;
            _settings.Update(s => s with { Margins = MarginMath.SetLinked(s.Margins, value, size) });
            Refresh();
        }
    }

    /// <summary>"Link" when off, "Linked" when on (DESIGN.md).</summary>
    public string LinkCaption => _isLinked ? "Linked" : "Link";

    public string LinkTooltip => _isLinked
        ? "Margins are linked. Changing one changes all four."
        : "Link the margins so that changing one changes all four.";

    /// <summary>Largest value a side box accepts, in pixels (MarginMath.MaxFraction of that dimension).</summary>
    public double MaxHorizontal => Math.Floor(_outputSize.Width * MarginMath.MaxFraction);

    public double MaxVertical => Math.Floor(_outputSize.Height * MarginMath.MaxFraction);

    public string OutputSizeText => $"{_outputSize.Width} × {_outputSize.Height}";

    /// <summary>Fractions 0..1 of the output for the diagram (left, top, right, bottom).</summary>
    public (double Left, double Top, double Right, double Bottom) Fractions
    {
        get
        {
            var m = _settings.Current.Margins;
            return (m.Left, m.Top, m.Right, m.Bottom);
        }
    }

    /// <summary>Re-read settings and the output size. Safe to call on the UI thread at any time.</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            var g = _output.State.Geometry;
            _outputSize = g.SizePx.Width > 0 && g.SizePx.Height > 0 ? g.SizePx : new PixelSize(1920, 1080);
            var m = _settings.Current.Margins;
            var px = MarginMath.ToPixels(m, _outputSize);
            Assign(ref _left, Math.Round(px.Left), nameof(Left));
            Assign(ref _top, Math.Round(px.Top), nameof(Top));
            Assign(ref _right, Math.Round(px.Right), nameof(Right));
            Assign(ref _bottom, Math.Round(px.Bottom), nameof(Bottom));
            if (_isLinked != m.IsLinked)
            {
                _isLinked = m.IsLinked;
                OnPropertyChanged(nameof(IsLinked));
                OnPropertyChanged(nameof(LinkCaption));
                OnPropertyChanged(nameof(LinkTooltip));
            }

            OnPropertyChanged(nameof(MaxHorizontal));
            OnPropertyChanged(nameof(MaxVertical));
            OnPropertyChanged(nameof(OutputSizeText));
            if (!ReferenceEquals(_lastMargins, m))
            {
                _lastMargins = m;
                OnPropertyChanged(nameof(Fractions));
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SetEdge(MarginEdge edge, double pixels, ref double field)
    {
        if (_refreshing)
        {
            field = pixels;
            return;
        }

        if (double.IsNaN(pixels) || Math.Abs(field - pixels) < 0.0001) return;
        var size = _outputSize;
        _settings.Update(s => s with { Margins = MarginMath.SetEdgePixels(s.Margins, edge, pixels, size) });
        Refresh();
        OnPropertyChanged(edge.ToString());
    }

    private void Assign(ref double field, double value, string name)
    {
        if (Math.Abs(field - value) < 0.0001) return;
        field = value;
        OnPropertyChanged(name);
    }
}
