using System.Windows;
using System.Windows.Controls;

namespace IluPrompter.App.Controls;

/// <summary>
/// Gives its child the full available width and the height that matches <see cref="Aspect"/> (width / height),
/// so the live preview frame hugs the output's shape (16:9, 16:10, 4:3...) and the letterbox stays thin.
/// If the height would not fit, the width shrinks instead; the frame is centred.
/// </summary>
public sealed class AspectRatioDecorator : Decorator
{
    public static readonly DependencyProperty AspectProperty = DependencyProperty.Register(
        nameof(Aspect), typeof(double), typeof(AspectRatioDecorator),
        new FrameworkPropertyMetadata(16.0 / 9.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
        v => v is double d && d > 0.1 && !double.IsInfinity(d));

    public double Aspect { get => (double)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    protected override Size MeasureOverride(Size constraint)
    {
        var size = Fit(constraint);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = Fit(arrangeSize);
        Child?.Arrange(new Rect(new Point((arrangeSize.Width - size.Width) / 2, 0), size));
        return arrangeSize.Width > 0 ? new Size(arrangeSize.Width, size.Height) : size;
    }

    private Size Fit(Size available)
    {
        var width = double.IsInfinity(available.Width) ? 400 : available.Width;
        var height = width / Aspect;
        if (!double.IsInfinity(available.Height) && height > available.Height)
        {
            height = available.Height;
            width = height * Aspect;
        }

        return new Size(Math.Max(0, width), Math.Max(0, height));
    }
}
