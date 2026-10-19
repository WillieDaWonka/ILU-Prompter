using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.Rendering;

/// <summary>
/// The preview's content: a backdrop-coloured panel that letterboxes the prompter surface (laid out at the output's own
/// DIP size) with a <see cref="ScaleTransform"/> render transform, never by laying out smaller, so line breaks equal the
/// output's (ARCHITECTURE §3 rule 3). The overlay covers the whole panel.
/// </summary>
internal sealed class PreviewScene : Panel
{
    private readonly PreviewOverlay _overlay = new();
    private readonly ScaleTransform _scale = new(1, 1);
    private PrompterSurface? _surface;
    private PreviewLayout _layout = PreviewLayout.Empty;

    public PreviewScene()
    {
        Background = ThemeResources.GetBrush("backdrop");
        ClipToBounds = true;
        Children.Add(_overlay);
    }

    public PreviewOverlay Overlay => _overlay;

    public PreviewLayout Layout => _layout;

    /// <summary>Raised after an arrange that changed the letterbox rectangle or scale.</summary>
    public event EventHandler? LayoutChanged;

    public void SetSurface(PrompterSurface? surface)
    {
        if (_surface is not null) Children.Remove(_surface);
        _surface = surface;
        if (surface is not null)
        {
            surface.HorizontalAlignment = HorizontalAlignment.Left;
            surface.VerticalAlignment = VerticalAlignment.Top;
            surface.RenderTransformOrigin = new Point(0, 0);
            surface.RenderTransform = _scale;
            Children.Insert(0, surface);
        }

        InvalidateMeasure();
    }

    private (double W, double H) OutputSize =>
        _surface is { } s ? (s.Output.WidthDip, s.Output.HeightDip) : (1920, 1080);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (w, h) = OutputSize;
        _surface?.Measure(new Size(w, h));
        _overlay.Measure(availableSize);
        return PreviewMath.ComputeDesiredSize(availableSize, w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (w, h) = OutputSize;
        var layout = PreviewMath.ComputeLayout(finalSize, w, h);
        if (_surface is not null)
        {
            if (layout.Scale > 0)
            {
                _scale.ScaleX = layout.Scale;
                _scale.ScaleY = layout.Scale;
                _surface.Visibility = Visibility.Visible;
                _surface.Arrange(new Rect(layout.Stage.X, layout.Stage.Y, w, h));
            }
            else
            {
                _surface.Visibility = Visibility.Collapsed;
            }
        }

        _overlay.Arrange(new Rect(finalSize));
        var changed = layout != _layout;
        _layout = layout;
        if (changed) LayoutChanged?.Invoke(this, EventArgs.Empty);
        return finalSize;
    }
}
